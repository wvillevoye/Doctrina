using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Doctrina.Infrastructure.Persistence;

/// <summary>
/// Applies, at start-up, the pending migrations of the enabled modules, in dependency order,
/// before the host accepts requests — only if <c>Database:MigrateOnStartup</c> is true (by default only in
/// Development, design D3). Outside Development the deployment pipeline runs EF Core migration bundles.
/// </summary>
public sealed partial class ModuleMigrationService(
    IServiceProvider services,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<ModuleMigrationService> logger) : IHostedService
{
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    /// <summary>Whether migrations are applied at start-up.</summary>
    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        return bool.TryParse(configuration[MigrateOnStartupKey], out var configured) ? configured : environment.IsDevelopment();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled(configuration, environment))
        {
            return;
        }

        foreach (var registration in services.GetServices<ModuleDbContextRegistration>())
        {
            await using var scope = services.CreateAsyncScope();
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType);
            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Count();
            if (pending == 0)
            {
                continue;
            }

            LogMigrating(logger, registration.ModuleName, pending);
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Module {Module}: {Count} openstaande migratie(s) toepassen.")]
    private static partial void LogMigrating(ILogger logger, string module, int count);
}
