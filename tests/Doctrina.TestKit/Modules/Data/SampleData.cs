using Doctrina.Core.Entities;
using Doctrina.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.TestKit.Modules.Data;

/// <summary>Uitwisselbare testentiteit van module <c>Sample</c>.</summary>
public sealed class SampleItem : ExchangeableEntity
{
    public required string Name { get; set; }

    /// <summary>Maakt een item en laat <see cref="SampleCreated"/> ontstaan (afgeleverd na het opslaan).</summary>
    public static SampleItem Create(string externalId, string source = "SIS", string name = "Voorbeeld")
    {
        var item = new SampleItem { ExternalId = externalId, Source = source, Name = name };
        item.RaiseEvent(new SampleCreated(externalId));
        return item;
    }
}

/// <summary>DbContext van testmodule <c>Sample</c> (schema <c>sample</c>).</summary>
public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : ModuleDbContext(options)
{
    public DbSet<SampleItem> Items => Set<SampleItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<SampleItem>().Property(i => i.Name).HasMaxLength(200);
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Voor <c>dotnet ef</c> (zie <c>docs/ontwikkelen/database.md</c>).</summary>
public sealed class SampleDbContextFactory() : ModuleDesignTimeDbContextFactory<SampleDbContext>(SampleModule.ModuleName);

/// <summary>Entiteit van testmodule <c>Notes</c>: een gewone (niet-uitwisselbare) entiteit.</summary>
public sealed class Note : Entity
{
    public required string Text { get; set; }
}

/// <summary>DbContext van testmodule <c>Notes</c> (schema <c>notes</c>).</summary>
public sealed class NotesDbContext(DbContextOptions<NotesDbContext> options) : ModuleDbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<Note>().Property(n => n.Text).HasMaxLength(500);
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Voor <c>dotnet ef</c>.</summary>
public sealed class NotesDbContextFactory() : ModuleDesignTimeDbContextFactory<NotesDbContext>(NotesModule.ModuleName);
