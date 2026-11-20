using Doctrina.Infrastructure.Authentication;
using Doctrina.Infrastructure.Persistence;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Doctrina.Host.IntegrationTests.DataPersistence;

/// <summary>Test databases on LocalDB, with the base connection string from <c>src/Doctrina.Host/appsettings.Test.json</c> (design D13).</summary>
internal static class TestDatabases
{
    private static readonly Lazy<string> BaseConnectionString = new(() =>
        new ConfigurationBuilder()
            .SetBasePath(HostProjectDirectory())
            .AddJsonFile("appsettings.Test.json", optional: false)
            .Build()
            .GetConnectionString(ModuleDatabase.ConnectionStringName)
        ?? throw new InvalidOperationException("ConnectionStrings:Doctrina ontbreekt in appsettings.Test.json."));

    public static Task<LocalDbTestDatabase> CreateAsync(CancellationToken cancellationToken) =>
        LocalDbTestDatabase.CreateAsync(BaseConnectionString.Value, cancellationToken);

    /// <summary>
    /// Host in environment <paramref name="environment"/> on <paramref name="database"/>. In Development and Production
    /// the test settings of the stubbed provider apply, so local files (Keycloak, secrets) don't count.
    /// </summary>
    public static WebApplicationFactory<Program> InEnvironment(
        this WebApplicationFactory<Program> host,
        string environment,
        LocalDbTestDatabase database,
        params (string Key, string? Value)[] settings)
    {
        ArgumentNullException.ThrowIfNull(database);
        return host.Configure(environment, database.ConnectionString, migrate: null, settings);
    }

    /// <summary>
    /// Host in environment <paramref name="environment"/> without its own test database. To be safe, the connection
    /// string points to the (non-existent) test database from <c>appsettings.Test.json</c> and migrating is off, so a test
    /// in Development never touches the real local database <c>Doctrina</c>.
    /// </summary>
    public static WebApplicationFactory<Program> InEnvironment(
        this WebApplicationFactory<Program> host,
        string environment,
        params (string Key, string? Value)[] settings) =>
        host.Configure(environment, BaseConnectionString.Value, migrate: false, settings);

    private static WebApplicationFactory<Program> Configure(
        this WebApplicationFactory<Program> host,
        string environment,
        string connectionString,
        bool? migrate,
        (string Key, string? Value)[] settings)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [$"ConnectionStrings:{ModuleDatabase.ConnectionStringName}"] = connectionString,
            [$"{OidcSettings.SectionName}:Authority"] = StubOidcProvider.Issuer,
            [$"{OidcSettings.SectionName}:ClientId"] = DoctrinaHostFactory.WebClientId,
            [$"{OidcSettings.SectionName}:ClientSecret"] = "test-secret",
            [$"{OidcSettings.SectionName}:RequireHttpsMetadata"] = "true",
            [$"{ApiAuthenticationSettings.SectionName}:Audience"] = DoctrinaHostFactory.ApiAudience,
            // No Hangfire server unless the test switches it on (it needs a database with the schema hangfire).
            ["Jobs:Enabled"] = "false",
        };
        if (migrate is { } value)
        {
            values[ModuleMigrationService.MigrateOnStartupKey] = value ? "true" : "false";
        }

        foreach (var (key, setting) in settings)
        {
            values[key] = setting; // last one wins
        }

        return host.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(values));
        });
    }

    private static string HostProjectDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Doctrina.Host");
            if (File.Exists(Path.Combine(candidate, "appsettings.Test.json")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("src/Doctrina.Host/appsettings.Test.json niet gevonden.");
    }
}
