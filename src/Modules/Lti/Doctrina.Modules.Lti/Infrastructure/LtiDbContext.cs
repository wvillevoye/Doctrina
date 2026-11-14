using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Lti.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Doctrina.Modules.Lti.Infrastructure;

/// <summary>Database context of the Lti module (schema <c>lti</c>).</summary>
public sealed class LtiDbContext(DbContextOptions<LtiDbContext> options) : ModuleDbContext(options)
{
    public const int NameMaxLength = 200;
    public const int IdMaxLength = 100;
    public const int UrlMaxLength = 2048;
    public const int RedirectUrisMaxLength = 8000;

    public DbSet<LtiTool> Tools => Set<LtiTool>();
    public DbSet<LtiKey> Keys => Set<LtiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<LtiTool>(b =>
        {
            b.ToTable("Tools");
            b.Property(t => t.Name).HasMaxLength(NameMaxLength);
            b.Property(t => t.ClientId).HasMaxLength(IdMaxLength);
            b.Property(t => t.DeploymentId).HasMaxLength(IdMaxLength);
            b.Property(t => t.LoginUrl).HasMaxLength(UrlMaxLength);
            b.Property(t => t.LaunchUrl).HasMaxLength(UrlMaxLength);
            b.Property(t => t.RedirectUris).HasMaxLength(RedirectUrisMaxLength);
            b.HasIndex(t => t.ClientId).IsUnique();
        });
        modelBuilder.Entity<LtiKey>(b =>
        {
            b.ToTable("Keys");
            b.Property(k => k.Kid).HasMaxLength(IdMaxLength);
            b.HasIndex(k => k.Kid).IsUnique();
            // At most one active key, also with simultaneous first requests (design D7).
            b.HasIndex(k => k.Active).IsUnique().HasFilter("[Active] = 1");
        });
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public sealed class LtiDbContextFactory() : ModuleDesignTimeDbContextFactory<LtiDbContext>(LtiModule.ModuleName);

/// <summary>Healthy when the module's database can be reached.</summary>
internal sealed class LtiHealthCheck(LtiDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The Lti database can't be reached.");
}
