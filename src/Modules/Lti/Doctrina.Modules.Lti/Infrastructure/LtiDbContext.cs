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
    public const int NonceMaxLength = 200;
    public const int CommentMaxLength = 1000;
    public const int RedirectUrisMaxLength = 8000;

    public DbSet<LtiTool> Tools => Set<LtiTool>();
    public DbSet<LtiKey> Keys => Set<LtiKey>();
    public DbSet<DeepLinkSelection> Selections => Set<DeepLinkSelection>();
    public DbSet<JwtNonce> Nonces => Set<JwtNonce>();
    public DbSet<LineItem> LineItems => Set<LineItem>();
    public DbSet<Score> Scores => Set<Score>();

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
            b.Property(t => t.DeepLinkingUrl).HasMaxLength(UrlMaxLength);
            b.Property(t => t.PublicKeysUrl).HasMaxLength(UrlMaxLength);
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
        modelBuilder.Entity<DeepLinkSelection>(b =>
        {
            b.ToTable("DeepLinkSelections");
            b.Property(x => x.Subject).HasMaxLength(IdMaxLength);
            b.HasIndex(x => x.Expires);
        });
        modelBuilder.Entity<JwtNonce>(b =>
        {
            b.ToTable("JwtNonces");
            b.Property(x => x.Purpose).HasMaxLength(20);
            b.Property(x => x.Nonce).HasMaxLength(NonceMaxLength);
            b.HasIndex(x => new { x.ToolId, x.Purpose, x.Nonce }).IsUnique();
            b.HasIndex(x => x.Expires);
        });
        modelBuilder.Entity<LineItem>(b =>
        {
            b.ToTable("LineItems");
            b.Property(x => x.Label).HasMaxLength(NameMaxLength);
            b.Property(x => x.ScoreMaximum).HasPrecision(18, 4);
            b.Property(x => x.ResourceId).HasMaxLength(IdMaxLength);
            b.Property(x => x.Tag).HasMaxLength(IdMaxLength);
            b.HasIndex(x => new { x.ToolId, x.OfferingId });
        });
        modelBuilder.Entity<Score>(b =>
        {
            b.ToTable("Scores");
            b.Property(x => x.ScoreGiven).HasPrecision(18, 4);
            b.Property(x => x.ScoreMaximum).HasPrecision(18, 4);
            b.Property(x => x.Comment).HasMaxLength(CommentMaxLength);
            b.Property(x => x.ActivityProgress).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.GradingProgress).HasConversion<string>().HasMaxLength(20);
            b.HasOne<LineItem>().WithMany().HasForeignKey(x => x.LineItemId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.LineItemId, x.PersonId }).IsUnique();
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
