using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Doctrina.Infrastructure.Persistence;

/// <summary>
/// Readiness-check van de database (<c>ConnectionStrings:Doctrina</c>): verbinden en <c>SELECT 1</c>. De uitkomst bevat
/// geen connection string of foutmelding, alleen de status.
/// </summary>
public sealed class DatabaseHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public const string Name = "database";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString(ModuleDatabase.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy("Geen connection string.");
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 5 };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = new SqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (SqlException)
        {
            return HealthCheckResult.Unhealthy("Database niet bereikbaar.");
        }
        catch (InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy("Database niet bereikbaar.");
        }
    }
}
