using Doctrina.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Doctrina.Infrastructure.Persistence;

/// <summary>
/// Basis voor de DbContext van een module (design D3): alle tabellen in het eigen schema van de module en een eigen
/// migratiegeschiedenis in dat schema. Registreer via
/// <see cref="ModuleDatabaseServiceCollectionExtensions.AddModuleDbContext{TContext}"/>; het schema komt uit die registratie.
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

    /// <summary>SQL-schema van de module: de modulenaam in kleine letters.</summary>
    public string Schema { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
        ApplyEntityConventions(modelBuilder);
    }

    /// <summary>
    /// Conventies voor <see cref="Entity"/> en <see cref="ExchangeableEntity"/> (design D4): sequentiële sleutel door
    /// EF Core, kolomlengtes, status als tekst en een unieke index op (<c>Source</c>, <c>ExternalId</c>).
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
