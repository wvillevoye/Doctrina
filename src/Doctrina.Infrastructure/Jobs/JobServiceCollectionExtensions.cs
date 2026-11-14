using Doctrina.Infrastructure.Persistence;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Doctrina.Infrastructure.Jobs;

/// <summary>
/// Background jobs with Hangfire (change add-bulk-import, design D2): storage in schema <c>hangfire</c> of the Doctrina
/// database, a server in the host, and the dashboard on <c>/hangfire</c> for role <c>admin</c> only. Modules use
/// <see cref="IBackgroundJobClient"/> and <see cref="IRecurringJobManager"/> directly.
/// </summary>
public static class JobServiceCollectionExtensions
{
    public const string Schema = "hangfire";
    public const string DashboardPath = "/hangfire";
    public const string AdminRole = "admin";

    /// <summary>Hangfire with SQL Server storage. Call <see cref="AddDoctrinaJobServer"/> after the migration service.</summary>
    public static IServiceCollection AddDoctrinaJobs(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Everything per host in DI instead of Hangfire's static GlobalConfiguration (which AddHangfire fills): several
        // hosts in one process (tests) would otherwise share storage, activator and logging. Only the serializer
        // settings stay static; they are the same everywhere.
        ConfigureSerializerOnce();
        services.AddSingleton<JobStorage>(provider =>
        {
            // Read here, not at registration: configuration added later (tests) must count.
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ModuleDatabase.ConnectionStringName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ModuleDatabase.ConnectionStringName} is missing.");
            return new SqlServerStorage(
                () => new SqlConnection(connectionString),
                new SqlServerStorageOptions { SchemaName = Schema, PrepareSchemaIfNecessary = true, QueuePollInterval = TimeSpan.FromSeconds(1) });
        });
        services.AddSingleton<JobActivator>(provider => new Hangfire.AspNetCore.AspNetCoreJobActivator(provider.GetRequiredService<IServiceScopeFactory>()));
        services.AddSingleton<IBackgroundJobClient>(provider => new BackgroundJobClient(provider.GetRequiredService<JobStorage>()));
        services.AddSingleton<IRecurringJobManager>(provider => new RecurringJobManager(provider.GetRequiredService<JobStorage>()));
        // MapHangfireDashboard checks that Hangfire is configured.
        services.AddSingleton(GlobalConfiguration.Configuration);
        services.AddSingleton(DashboardRoutes.Routes);
        return services;
    }

    private static readonly Lazy<bool> SerializerConfigured = new(() =>
    {
        GlobalConfiguration.Configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings();
        return true;
    });

    private static void ConfigureSerializerOnce() => _ = SerializerConfigured.Value;

    /// <summary>
    /// The job server. Register after the module migrations, so the database exists first (hosted services start in
    /// order). It only runs when <c>Jobs:Enabled</c> is true (default), read at start-up.
    /// </summary>
    public static IServiceCollection AddDoctrinaJobServer(this IServiceCollection services) =>
        services.AddHostedService<DoctrinaJobServer>();

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue(EnabledKey, true);

    public const string EnabledKey = "Jobs:Enabled";

    /// <summary>The dashboard, when jobs are enabled.</summary>
    public static void MapDoctrinaJobsDashboard(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!IsEnabled(app.Configuration))
        {
            return;
        }

        app.MapHangfireDashboard(
            DashboardPath,
            new DashboardOptions
            {
                Authorization = [], // replaces the default "local requests only"
                AsyncAuthorization = [new AdminOnly()],
                DisplayStorageConnectionString = false,
            },
            app.Services.GetRequiredService<JobStorage>());
    }

    /// <summary>Runs a Hangfire <see cref="BackgroundJobServer"/> when <c>Jobs:Enabled</c>.</summary>
    private sealed class DoctrinaJobServer(IServiceProvider services, IConfiguration configuration, ILoggerFactory loggers) : Microsoft.Extensions.Hosting.IHostedService, IDisposable
    {
        private BackgroundJobServer? _server;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (IsEnabled(configuration))
            {
                // Hangfire's log provider is static; this one survives a host that has already been disposed (tests).
                Hangfire.Logging.LogProvider.SetCurrentLogProvider(new SafeLogProvider(loggers));
                _server = new BackgroundJobServer(
                    new BackgroundJobServerOptions { Activator = services.GetRequiredService<JobActivator>() },
                    services.GetRequiredService<JobStorage>());
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _server?.SendStop();
            return _server?.WaitForShutdownAsync(cancellationToken) ?? Task.CompletedTask;
        }

        public void Dispose() => _server?.Dispose();
    }

    /// <summary>Hangfire logging into ASP.NET Core logging; silent once that host's logger factory is disposed.</summary>
    private sealed class SafeLogProvider(ILoggerFactory loggers) : Hangfire.Logging.ILogProvider
    {
        private readonly Hangfire.AspNetCore.AspNetCoreLogProvider _inner = new(loggers);

        public Hangfire.Logging.ILog GetLogger(string name)
        {
            try
            {
                return _inner.GetLogger(name);
            }
            catch (ObjectDisposedException)
            {
                return Silent.Instance;
            }
        }

        private sealed class Silent : Hangfire.Logging.ILog
        {
            public static readonly Silent Instance = new();

            public bool Log(Hangfire.Logging.LogLevel logLevel, Func<string>? messageFunc, Exception? exception = null) => false;
        }
    }

    /// <summary>Signed in with role <c>admin</c>; otherwise 401 (anonymous) or 403.</summary>
    private sealed class AdminOnly : IDashboardAsyncAuthorizationFilter
    {
        public Task<bool> AuthorizeAsync(DashboardContext context)
        {
            var http = context.GetHttpContext();
            if (http.User.Identity?.IsAuthenticated != true)
            {
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.FromResult(false);
            }

            return Task.FromResult(http.User.IsInRole(AdminRole));
        }
    }
}
