using Doctrina.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

// Taak 1.5 / design D13: configuratiebestanden per omgeving en appsettings.Secrets.json.
namespace Doctrina.Host.IntegrationTests.Configuration;

public sealed class ConfiguratiePerOmgeving : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("doctrina-config-").FullName;

    [Fact]
    public void SecretsBestandWintBovenHetOmgevingsbestand()
    {
        Write("appsettings.json", """{ "Waarde": "basis", "Alleen": "basis" }""");
        Write("appsettings.Development.json", """{ "Waarde": "development" }""");
        Write(DoctrinaConfigurationExtensions.SecretsFile, """{ "Waarde": "secret" }""");

        var configuration = Build(Environments.Development);

        Assert.Equal("secret", configuration["Waarde"]);
        Assert.Equal("basis", configuration["Alleen"]);
    }

    [Fact]
    public void OmgevingsvariabelenWinnenBovenHetSecretsBestand()
    {
        Write("appsettings.json", "{}");
        Write(DoctrinaConfigurationExtensions.SecretsFile, """{ "Waarde": "secret" }""");

        // De in-memory bron staat voor omgevingsvariabelen: die worden door ASP.NET Core ná de JSON-bestanden toegevoegd.
        var configuration = Build(Environments.Production, new Dictionary<string, string?>(StringComparer.Ordinal) { ["Waarde"] = "omgeving" });

        Assert.Equal("omgeving", configuration["Waarde"]);
    }

    [Fact]
    public void SecretsBestandWordtInDeTestomgevingNietGeladen()
    {
        Write("appsettings.json", """{ "Waarde": "basis" }""");
        Write(DoctrinaConfigurationExtensions.SecretsFile, """{ "Waarde": "secret" }""");

        var configuration = Build(DoctrinaConfigurationExtensions.TestEnvironment);

        Assert.Equal("basis", configuration["Waarde"]);
    }

    [Fact]
    public void ZonderSecretsBestandStartGewoon()
    {
        Write("appsettings.json", """{ "Waarde": "basis" }""");

        var configuration = Build(Environments.Development);

        Assert.Equal("basis", configuration["Waarde"]);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string file, string json) => File.WriteAllText(Path.Combine(_root, file), json);

    private IConfigurationRoot Build(string environmentName, IDictionary<string, string?>? environmentVariables = null)
    {
        using var files = new PhysicalFileProvider(_root);
        var environment = new TestHostEnvironment(environmentName, _root, files);
        var builder = new ConfigurationBuilder()
            .Add(new JsonConfigurationSource { Path = "appsettings.json", Optional = true, FileProvider = files })
            .Add(new JsonConfigurationSource { Path = $"appsettings.{environmentName}.json", Optional = true, FileProvider = files });
        if (environmentVariables is not null)
        {
            builder.AddInMemoryCollection(environmentVariables);
        }

        builder.AddDoctrinaSecrets(environment);
        foreach (var source in builder.Sources.OfType<JsonConfigurationSource>())
        {
            source.ReloadOnChange = false;
        }

        return builder.Build();
    }

    private sealed class TestHostEnvironment(string name, string root, IFileProvider files) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Doctrina.Host";

        public string ContentRootPath { get; set; } = root;

        public IFileProvider ContentRootFileProvider { get; set; } = files;
    }
}

public sealed class DeHostDraaitInDeTestomgeving(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    [Fact]
    public void TestinstellingenKomenUitAppsettingsTestJson()
    {
        var environment = factory.Services.GetRequiredService<IHostEnvironment>();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        Assert.True(environment.IsTest());
        Assert.Equal("test-secret", configuration["Authentication:Oidc:ClientSecret"]);
        Assert.Contains("(localdb)\\MSSQLLocalDB", configuration.GetConnectionString("Doctrina"), StringComparison.Ordinal);
        Assert.DoesNotContain(
            ((IConfigurationRoot)configuration).Providers.OfType<JsonConfigurationProvider>(),
            p => p.Source.Path == DoctrinaConfigurationExtensions.SecretsFile);
    }
}
