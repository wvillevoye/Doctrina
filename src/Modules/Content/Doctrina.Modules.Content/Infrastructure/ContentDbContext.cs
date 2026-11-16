using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Doctrina.Modules.Content.Infrastructure;

/// <summary>Database context of the Content module (schema <c>content</c>).</summary>
public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : ModuleDbContext(options)
{
    public const int TitleMaxLength = 200;
    public const int UrlMaxLength = 2048;
    public const int FileNameMaxLength = 255;

    public DbSet<ContentSection> Sections => Set<ContentSection>();
    public DbSet<ContentItem> Items => Set<ContentItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<ContentSection>(b =>
        {
            b.ToTable("Sections");
            b.Property(s => s.Title).HasMaxLength(TitleMaxLength);
            b.HasIndex(s => s.OfferingId);
            b.HasMany(s => s.Items).WithOne(i => i.Section).HasForeignKey(i => i.SectionId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ContentItem>(b =>
        {
            b.ToTable("Items");
            b.Property(i => i.Kind).HasConversion<string>().HasMaxLength(16);
            b.Property(i => i.Title).HasMaxLength(TitleMaxLength);
            b.Property(i => i.Url).HasMaxLength(UrlMaxLength);
            b.Property(i => i.FileName).HasMaxLength(FileNameMaxLength);
            b.Property(i => i.ContentType).HasMaxLength(FileNameMaxLength);
            b.Ignore(i => i.IsVisible);
        });
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public sealed class ContentDbContextFactory() : ModuleDesignTimeDbContextFactory<ContentDbContext>(ContentModule.ModuleName);

/// <summary>Healthy when the module's database can be reached.</summary>
internal sealed class ContentHealthCheck(ContentDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The Content database can't be reached.");
}
