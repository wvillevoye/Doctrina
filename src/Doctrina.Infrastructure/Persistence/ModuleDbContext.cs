using Doctrina.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Doctrina.Infrastructure.Persistence;

/// <summary>
/// Base for the DbContext of a module (design D3): all tables in the module's own schema and its own
/// migration history in that schema. Register via
/// <see cref="ModuleDatabaseServiceCollectionExtensions.AddModuleDbContext{TContext}"/>; the schema comes from that registration.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    protected ModuleDbContext(DbContextOptions options)
        : base(options)
    {
        Schema = options.Extensions.OfType<RelationalOptionsExtension>().FirstOrDefault()?.MigrationsHistoryTableSchema
            ?? throw new InvalidOperationException(
                $"{GetType().Name} heeft geen moduleschema; registreer de context met AddModuleDbContext of maak opties met ModuleDatabase.CreateOptions.");
    }

    /// <summary>SQL schema of the module: the module name in lower case.</summary>
    public string Schema { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
        ApplyEntityConventions(modelBuilder);
    }

    /// <summary>
    /// Conventions for <see cref="Entity"/> and <see cref="ExchangeableEntity"/> (design D4): sequential key by
    /// EF Core, column lengths, status as text and a unique index on (<c>Source</c>, <c>ExternalId</c>).
    /// </summary>
    private static void ApplyEntityConventions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            var entity = modelBuilder.Entity(entityType.ClrType);
            entity.HasKey(nameof(Entity.Id));
            entity.Property(nameof(Entity.Id)).ValueGeneratedOnAdd();
            entity.Ignore(nameof(Entity.DomainEvents));

            if (!typeof(ExchangeableEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            entity.Property(nameof(ExchangeableEntity.ExternalId)).HasMaxLength(ExchangeableEntity.ExternalIdMaxLength).IsRequired();
            entity.Property(nameof(ExchangeableEntity.Source)).HasMaxLength(ExchangeableEntity.SourceMaxLength).IsRequired();
            entity.Property(nameof(ExchangeableEntity.Status)).HasConversion<string>().HasMaxLength(16);
            entity.Property(nameof(ExchangeableEntity.DateLastModified));
            entity.HasIndex(nameof(ExchangeableEntity.Source), nameof(ExchangeableEntity.ExternalId)).IsUnique();
        }
    }
}
