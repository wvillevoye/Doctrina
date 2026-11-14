using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Lti.Infrastructure;
using Doctrina.TestKit.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Doctrina.Modules.Lti.Tests;

/// <summary>
/// An empty LocalDB database with the Lti migrations applied, per test. The base connection string comes from
/// <c>src/Doctrina.Host/appsettings.Test.json</c>.
/// </summary>
internal sealed class LtiDatabase : IAsyncDisposable
{
    private static readonly Lazy<string> BaseConnectionString = new(() =>
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepoRoot(), "src", "Doctrina.Host", "appsettings.Test.json"), optional: false)
            .Build()
            .GetConnectionString(ModuleDatabase.ConnectionStringName)
        ?? throw new InvalidOperationException("ConnectionStrings:Doctrina is missing in appsettings.Test.json."));

    private LtiDatabase(LocalDbTestDatabase database) => Database = database;

    public LocalDbTestDatabase Database { get; }

    public static async Task<LtiDatabase> CreateAsync(CancellationToken cancellationToken)
    {
        var database = new LtiDatabase(await LocalDbTestDatabase.CreateAsync(BaseConnectionString.Value, cancellationToken));
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
        return database;
    }

    /// <summary>A fresh context (own change tracker), like a new request.</summary>
    public LtiDbContext CreateContext() =>
        new(ModuleDatabase.CreateOptions<LtiDbContext>(Database.ConnectionString, LtiModule.ModuleName));

    public ValueTask DisposeAsync() => Database.DisposeAsync();

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
