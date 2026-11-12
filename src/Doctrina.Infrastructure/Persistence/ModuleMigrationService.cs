using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Doctrina.Infrastructure.Persistence;

/// <summary>
/// Past bij het opstarten de openstaande migraties van de ingeschakelde modules toe, in afhankelijkheidsvolgorde,
/// vóórdat de host verzoeken accepteert — alleen als <c>Database:MigrateOnStartup</c> true is (standaard alleen in
/// Development, design D3). Buiten Development draait de deployment-pipeline EF Core-migratiebundels.
/// </summary>
public sealed partial class ModuleMigrationService(
    IServiceProvider services,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<ModuleMigrationService> logger) : IHostedService
{
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    /// <summary>Of migraties bij het opstarten worden toegepast.</summary>
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
