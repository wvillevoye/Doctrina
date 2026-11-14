using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Content.Application;
using Doctrina.Modules.Content.Infrastructure;
using Doctrina.TestKit.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Content.Tests;

/// <summary>
/// An empty LocalDB database with the Content migrations applied, per test, and a temporary folder for files. The base
/// connection string comes from <c>src/Doctrina.Host/appsettings.Test.json</c>.
/// </summary>
internal sealed class ContentDatabase : IAsyncDisposable
{
    private static readonly Lazy<string> BaseConnectionString = new(() =>
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepoRoot(), "src", "Doctrina.Host", "appsettings.Test.json"), optional: false)
            .Build()
            .GetConnectionString(ModuleDatabase.ConnectionStringName)
        ?? throw new InvalidOperationException("ConnectionStrings:Doctrina is missing in appsettings.Test.json."));

    private ContentDatabase(LocalDbTestDatabase database) => Database = database;

    public LocalDbTestDatabase Database { get; }

    public string FilesPath { get; } = Path.Combine(Path.GetTempPath(), "doctrina-content-tests", Guid.NewGuid().ToString("N"));

    public static async Task<ContentDatabase> CreateAsync(CancellationToken cancellationToken)
    {
        var database = new ContentDatabase(await LocalDbTestDatabase.CreateAsync(BaseConnectionString.Value, cancellationToken));
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
        return database;
    }

    /// <summary>A fresh context (own change tracker), like a new request.</summary>
    public ContentDbContext CreateContext() =>
        new(ModuleDatabase.CreateOptions<ContentDbContext>(Database.ConnectionString, ContentModule.ModuleName));

    public ContentFileStore CreateFileStore(int maxSizeMb = 100) =>
        new(Options.Create(new ContentFileOptions { Path = FilesPath, MaxSizeMb = maxSizeMb }), new TestHostEnvironment(), NullLogger<ContentFileStore>.Instance);

    /// <summary>The service over a fresh context, the given directory and a file store in the temporary folder.</summary>
    public ContentService CreateService(FakeDirectory directory, int maxSizeMb = 100) => new(CreateContext(), directory, CreateFileStore(maxSizeMb));

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
        if (Directory.Exists(FilesPath))
        {
            Directory.Delete(FilesPath, recursive: true);
        }
    }

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

internal sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "Doctrina.Modules.Content.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
}
