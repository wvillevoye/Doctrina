using Doctrina.Infrastructure.Persistence;
using Doctrina.TestKit.Database;
using Doctrina.TestKit.Modules;
using Doctrina.TestKit.Modules.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

// Tests volgen spec openspec/specs/data-persistence/spec.md:
// klasse = requirement, methode = scenario. Elke test krijgt een eigen, lege LocalDB-database.
namespace Doctrina.Host.IntegrationTests.DataPersistence;

public abstract class DatabaseTest : IAsyncLifetime
{
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected LocalDbTestDatabase Database { get; private set; } = null!;

    public async ValueTask InitializeAsync() => Database = await TestDatabases.CreateAsync(Ct);

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected SampleDbContext Sample(TimeProvider? time = null) =>
        new(ModuleDatabase.CreateOptions<SampleDbContext>(Database.ConnectionString, SampleModule.ModuleName, time));

    protected NotesDbContext Notes() =>
        new(ModuleDatabase.CreateOptions<NotesDbContext>(Database.ConnectionString, NotesModule.ModuleName));

    protected async Task<IReadOnlyList<string>> SchemasAsync() =>
        [.. (await Database.QueryAsync("SELECT name FROM sys.schemas WHERE name IN ('sample', 'notes') ORDER BY name", Ct)).Select(r => r[0])];

    protected static SampleItem Item(string externalId, string source = "SIS", string name = "Voorbeeld") =>
        new() { ExternalId = externalId, Source = source, Name = name };
}

public sealed class ElkeModuleHeeftEenEigenDatabaseschema : DatabaseTest
{
    [Fact]
    public async Task TabellenWordenInHetModuleschemaAangemaakt()
    {
        await using (var context = Sample())
        {
            await context.Database.MigrateAsync(Ct);
        }

        var tables = await Database.TablesAsync(Ct);

        Assert.Equal(["sample.__EFMigrationsHistory", "sample.Items"], tables);
    }
}

public sealed class ElkeModuleMigreertZelfstandig(DoctrinaHostFactory factory) : DatabaseTest, IClassFixture<DoctrinaHostFactory>
{
    [Fact]
    public async Task MigratiegeschiedenisPerModule()
    {
        await using var sample = Sample();
        await using var notes = Notes();
        await sample.Database.MigrateAsync(Ct);
        await notes.Database.MigrateAsync(Ct);

        var sampleHistory = await Database.QueryAsync("SELECT MigrationId FROM sample.__EFMigrationsHistory", Ct);
        var notesHistory = await Database.QueryAsync("SELECT MigrationId FROM notes.__EFMigrationsHistory", Ct);

        Assert.Equal(sample.Database.GetMigrations(), sampleHistory.Select(r => r[0]));
        Assert.Equal(notes.Database.GetMigrations(), notesHistory.Select(r => r[0]));
        Assert.Empty(sample.Database.GetMigrations().Intersect(notes.Database.GetMigrations(), StringComparer.Ordinal));
        Assert.DoesNotContain("dbo.__EFMigrationsHistory", await Database.TablesAsync(Ct));
    }

    [Fact]
    public async Task UitgeschakeldeModuleWordtNietGemigreerd()
    {
        using var host = factory
            .WithModules([new SampleModule(), new NotesModule()], NotesModule.ModuleName)
            .InEnvironment(Environments.Development, Database);

        host.CreateClient().Dispose();

        Assert.Equal(["notes"], await SchemasAsync());
    }
}

public sealed class AutomatischMigrerenIsInstelbaar(DoctrinaHostFactory factory) : DatabaseTest, IClassFixture<DoctrinaHostFactory>
{
    [Fact]
    public async Task OpenstaandeMigratiesToegepastInDevelopment()
    {
        using var host = factory
            .WithModules([new SampleModule()], SampleModule.ModuleName)
            .InEnvironment(Environments.Development, Database);

        host.CreateClient().Dispose();

        await using var context = Sample();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(Ct));
        Assert.Contains("sample.Items", await Database.TablesAsync(Ct));
    }

    [Fact]
    public async Task GeenAutomatischeMigratieBuitenDevelopment()
    {
        using var host = factory
            .WithModules([new SampleModule()], SampleModule.ModuleName)
            .InEnvironment(Environments.Production, Database);

        host.CreateClient().Dispose();

        Assert.Empty(await SchemasAsync());
        Assert.Empty(await Database.TablesAsync(Ct));
        // Ook zonder de expliciete waarde in appsettings.Production.json is de standaard buiten Development: niet migreren.
        Assert.False(ModuleMigrationService.IsEnabled(new ConfigurationBuilder().Build(), new TestEnvironment(Environments.Production)));
        Assert.True(ModuleMigrationService.IsEnabled(new ConfigurationBuilder().Build(), new TestEnvironment(Environments.Development)));
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Doctrina.Host";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

public sealed class EntiteitsleutelsZijnSequentieleGuids : DatabaseTest
{
    [Fact]
    public async Task BijInvoegenGegenereerdeSleutelsZijnOplopend()
    {
        await using var context = Sample();
        await context.Database.MigrateAsync(Ct);
        var items = Enumerable.Range(1, 1000).Select(n => Item($"i{n:D7}")).ToList();

        context.Items.AddRange(items);
        await context.SaveChangesAsync(Ct);

        Assert.All(items, i => Assert.NotEqual(Guid.Empty, i.Id));
        await using var reader = Sample();
        var sortedBySqlServer = await reader.Items.OrderBy(i => i.Id).Select(i => i.Id).ToListAsync(Ct);
        Assert.Equal(items.Select(i => i.Id), sortedBySqlServer);
    }
}

public sealed class UitwisselbareEntiteitenHebbenSynchronisatievelden : DatabaseTest
{
    [Fact]
    public async Task DateLastModifiedWordtAutomatischBijgehouden()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));
        await using var context = Sample(clock);
        await context.Database.MigrateAsync(Ct);
        var item = Item("i6123456");

        context.Items.Add(item);
        await context.SaveChangesAsync(Ct);
        var afterInsert = await ReadDateLastModifiedAsync(item.Id);

        clock.SetUtcNow(new DateTimeOffset(2026, 11, 12, 11, 0, 0, TimeSpan.Zero));
        item.Name = "Gewijzigd";
        await context.SaveChangesAsync(Ct);
        var afterUpdate = await ReadDateLastModifiedAsync(item.Id);

        Assert.Equal(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero), afterInsert);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 11, 0, 0, TimeSpan.Zero), afterUpdate);
        Assert.Equal(TimeSpan.Zero, afterUpdate.Offset);
    }

    [Fact]
    public async Task DubbeleExternalIdBinnenEenBronWordtGeweigerd()
    {
        await using (var context = Sample())
        {
            await context.Database.MigrateAsync(Ct);
            context.Items.Add(Item("i6123456", "SIS"));
            await context.SaveChangesAsync(Ct);
        }

        await using var second = Sample();
        second.Items.Add(Item("i6123456", "SIS"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync(Ct));

        Assert.True(exception.IsUniqueConstraintViolation());
        await using var reader = Sample();
        Assert.Equal(1, await reader.Items.CountAsync(i => i.ExternalId == "i6123456", Ct));
    }

    [Fact]
    public async Task ZelfdeExternalIdUitEenAndereBronIsToegestaan()
    {
        await using (var context = Sample())
        {
            await context.Database.MigrateAsync(Ct);
            context.Items.Add(Item("i6123456", "SIS"));
            await context.SaveChangesAsync(Ct);
            context.Items.Add(Item("i6123456", "Manual"));
            await context.SaveChangesAsync(Ct);
        }

        await using var reader = Sample();
        var sources = await reader.Items.Where(i => i.ExternalId == "i6123456").Select(i => i.Source).OrderBy(s => s).ToListAsync(Ct);
        Assert.Equal(["Manual", "SIS"], sources);
    }

    private async Task<DateTimeOffset> ReadDateLastModifiedAsync(Guid id)
    {
        await using var reader = Sample();
        return await reader.Items.Where(i => i.Id == id).Select(i => i.DateLastModified).SingleAsync(Ct);
    }
}
