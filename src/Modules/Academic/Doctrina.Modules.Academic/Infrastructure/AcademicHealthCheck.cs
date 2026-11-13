using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Doctrina.Modules.Academic.Infrastructure;

/// <summary>Healthy when the module's database can be reached.</summary>
internal sealed class AcademicHealthCheck(AcademicDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The Academic database can't be reached.");
}
