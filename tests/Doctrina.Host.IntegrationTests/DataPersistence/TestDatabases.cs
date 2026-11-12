using Doctrina.Infrastructure.Authentication;
using Doctrina.Infrastructure.Persistence;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Doctrina.Host.IntegrationTests.DataPersistence;

/// <summary>Testdatabases op LocalDB, met de basis-connection string uit <c>src/Doctrina.Host/appsettings.Test.json</c> (design D13).</summary>
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
    /// Host in omgeving <paramref name="environment"/> op <paramref name="database"/>. In Development en Production
    /// gelden de testinstellingen van de nagebootste provider, zodat lokale bestanden (Keycloak, secrets) niet meetellen.
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
    /// Host in omgeving <paramref name="environment"/> zonder eigen testdatabase. Veiligheidshalve wijst de connection
    /// string naar de (niet-bestaande) testdatabase uit <c>appsettings.Test.json</c> en staat migreren uit, zodat een test
    /// in Development nooit de echte lokale database <c>Doctrina</c> aanraakt.
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
        };
        if (migrate is { } value)
        {
            values[ModuleMigrationService.MigrateOnStartupKey] = value ? "true" : "false";
        }

        foreach (var (key, setting) in settings)
        {
            values[key] = setting; // laatste wint
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
