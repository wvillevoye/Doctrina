using Doctrina.Infrastructure.Events;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Doctrina.Infrastructure.Persistence;

/// <summary>Shared settings for the databases of modules (design D3/D13).</summary>
public static class ModuleDatabase
{
    /// <summary>Name of the connection string (<c>ConnectionStrings:Doctrina</c>).</summary>
    public const string ConnectionStringName = "Doctrina";

    /// <summary>Name of the migration history table; lives per module in its own schema.</summary>
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";

    /// <summary>Local default database for development and design time (<c>dotnet ef</c>), see design D13.</summary>
    public const string LocalDbConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=Doctrina;Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>SQL schema of a module: the module name in lower case.</summary>
    public static string SchemaFor(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        return moduleName.Replace(".", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    /// <summary>Configures SQL Server with the module schema for the migration history.</summary>
    public static DbContextOptionsBuilder UseModuleSqlServer(this DbContextOptionsBuilder options, string connectionString, string moduleName)
    {
        ArgumentNullException.ThrowIfNull(options);
        var schema = SchemaFor(moduleName);
        return options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(MigrationsHistoryTable, schema));
    }

    /// <summary>Options without DI, e.g. for <c>dotnet ef</c> or tests; without <paramref name="events"/> entity events are not delivered.</summary>
    public static DbContextOptions<TContext> CreateOptions<TContext>(
        string connectionString,
        string moduleName,
        TimeProvider? time = null,
        InProcessEventBus? events = null)
        where TContext : ModuleDbContext
    {
        var options = new DbContextOptionsBuilder<TContext>();
        options.UseModuleSqlServer(connectionString, moduleName).AddInterceptors(new DateLastModifiedInterceptor(time ?? TimeProvider.System));
        if (events is not null)
        {
            options.AddInterceptors(new DomainEventsInterceptor(events));
        }

        return options.Options;
    }

    /// <summary>
    /// Is the error a unique index violation (SQL Server 2601/2627), e.g. a duplicate <c>ExternalId</c> within a
    /// <c>Source</c>? The upsert services translate this into a <c>Conflict</c> error.
    /// </summary>
    public static bool IsUniqueConstraintViolation(this DbUpdateException exception) =>
        exception?.InnerException is SqlException { Number: 2601 or 2627 };
}

/// <summary>A DbContext of an enabled module, registered in dependency order (for migrations).</summary>
public sealed record ModuleDbContextRegistration(string ModuleName, Type ContextType);

/// <summary>Registration of module DbContexts.</summary>
public static class ModuleDatabaseServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContext"/> for module <paramref name="moduleName"/>: own schema, own
    /// migration history, <c>DateLastModified</c> interceptor and delivery of entity events after saving. Call from <c>IModule.ConfigureServices</c>, so
    /// only enabled modules get a database.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, IConfiguration configuration, string moduleName)
        where TContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ModuleDatabase.SchemaFor(moduleName);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<DateLastModifiedInterceptor>();
        services.AddDoctrinaEvents();
        services.TryAddScoped<DomainEventsInterceptor>();
        services.AddDbContext<TContext>((provider, options) =>
        {
            var connectionString = configuration.GetConnectionString(ModuleDatabase.ConnectionStringName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ModuleDatabase.ConnectionStringName} ontbreekt.");
            options.UseModuleSqlServer(connectionString, moduleName)
                .AddInterceptors(
                    provider.GetRequiredService<DateLastModifiedInterceptor>(),
                    provider.GetRequiredService<DomainEventsInterceptor>());
        });
        services.AddSingleton(new ModuleDbContextRegistration(moduleName, typeof(TContext)));
        return services;
    }
}

/// <summary>
/// Base for the design-time factory of a module, so <c>dotnet ef</c> works without starting the host
/// (see <c>docs/ontwikkelen/database.md</c>). Connection string: argument <c>--connection</c> after <c>--</c>, otherwise the
/// environment variable <c>ConnectionStrings__Doctrina</c>, otherwise LocalDB.
/// </summary>
public abstract class ModuleDesignTimeDbContextFactory<TContext>(string moduleName) : IDesignTimeDbContextFactory<TContext>
    where TContext : ModuleDbContext
{
    public TContext CreateDbContext(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var index = Array.IndexOf(args, "--connection");
        var connectionString = index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : Environment.GetEnvironmentVariable($"ConnectionStrings__{ModuleDatabase.ConnectionStringName}") ?? ModuleDatabase.LocalDbConnectionString;
        return (TContext)Activator.CreateInstance(typeof(TContext), ModuleDatabase.CreateOptions<TContext>(connectionString, moduleName))!;
    }
}
