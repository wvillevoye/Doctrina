using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Academic.Infrastructure;
using Doctrina.TestKit.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace Doctrina.Modules.Academic.Tests;

/// <summary>
/// An empty LocalDB database with the Academic migrations applied, per test. The base connection string comes from
/// <c>src/Doctrina.Host/appsettings.Test.json</c>, like the host integration tests.
/// </summary>
internal sealed class AcademicDatabase : IAsyncDisposable
{
    private static readonly Lazy<string> BaseConnectionString = new(() =>
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepoRoot(), "src", "Doctrina.Host", "appsettings.Test.json"), optional: false)
            .Build()
            .GetConnectionString(ModuleDatabase.ConnectionStringName)
        ?? throw new InvalidOperationException("ConnectionStrings:Doctrina is missing in appsettings.Test.json."));

    private readonly LocalDbTestDatabase _database;

    private AcademicDatabase(LocalDbTestDatabase database) => _database = database;

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));

    public LocalDbTestDatabase Database => _database;

    public static async Task<AcademicDatabase> CreateAsync(CancellationToken cancellationToken)
    {
        var database = new AcademicDatabase(await LocalDbTestDatabase.CreateAsync(BaseConnectionString.Value, cancellationToken));
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
        return database;
    }

    /// <summary>A fresh context (own change tracker), like a new request.</summary>
    public AcademicDbContext CreateContext() =>
        new(ModuleDatabase.CreateOptions<AcademicDbContext>(_database.ConnectionString, AcademicModule.ModuleName, Time));

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    public static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Doctrina.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Doctrina.slnx not found above " + AppContext.BaseDirectory);
    }
}
