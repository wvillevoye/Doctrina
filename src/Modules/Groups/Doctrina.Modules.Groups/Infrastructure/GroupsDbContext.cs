using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Groups.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Doctrina.Modules.Groups.Infrastructure;

/// <summary>Database context of the Groups module (schema <c>groups</c>).</summary>
public sealed class GroupsDbContext(DbContextOptions<GroupsDbContext> options) : ModuleDbContext(options)
{
    public const int NameMaxLength = 200;

    public DbSet<GroupSet> GroupSets => Set<GroupSet>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<GroupSet>(b =>
        {
            b.Property(s => s.Name).HasMaxLength(NameMaxLength);
            b.HasIndex(s => s.OfferingId);
            b.HasMany(s => s.Groups).WithOne(g => g.GroupSet).HasForeignKey(g => g.GroupSetId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Group>(b =>
        {
            b.Property(g => g.Name).HasMaxLength(NameMaxLength);
            b.HasMany(g => g.Members).WithOne().HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<GroupMember>(b => b.HasIndex(m => new { m.GroupSetId, m.PersonId }).IsUnique());
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public sealed class GroupsDbContextFactory() : ModuleDesignTimeDbContextFactory<GroupsDbContext>(GroupsModule.ModuleName);

/// <summary>Healthy when the module's database can be reached.</summary>
internal sealed class GroupsHealthCheck(GroupsDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The Groups database can't be reached.");
}
