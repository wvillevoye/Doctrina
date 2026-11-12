using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;

namespace Doctrina.Infrastructure.Configuration;

/// <summary>Configuratiebestanden per omgeving en het secrets-bestand (design D13).</summary>
public static class DoctrinaConfigurationExtensions
{
    /// <summary>Niet-gecommit bestand met secrets; voorbeeld in <c>appsettings.Secrets.example.json</c>.</summary>
    public const string SecretsFile = "appsettings.Secrets.json";

    /// <summary>Omgeving van de geautomatiseerde tests (<c>appsettings.Test.json</c>).</summary>
    public const string TestEnvironment = "Test";

    /// <summary>Of dit de omgeving van de geautomatiseerde tests is.</summary>
    public static bool IsTest(this IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return environment.IsEnvironment(TestEnvironment);
    }

    /// <summary>
    /// Voegt het optionele <see cref="SecretsFile"/> toe direct ná <c>appsettings.{omgeving}.json</c>, dus vóór user
    /// secrets, omgevingsvariabelen en command-line-argumenten. In de omgeving <see cref="TestEnvironment"/> wordt het
    /// bewust niet geladen, zodat lokale secrets nooit in de tests terechtkomen.
    /// </summary>
    public static IConfigurationBuilder AddDoctrinaSecrets(this IConfigurationBuilder configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsTest())
        {
            return configuration;
        }

        var source = new JsonConfigurationSource
        {
            Path = SecretsFile,
            Optional = true,
            ReloadOnChange = true,
            FileProvider = environment.ContentRootFileProvider,
        };

        var index = LastIndexOfAppSettings(configuration.Sources);
        if (index < 0)
        {
            configuration.Sources.Add(source);
        }
        else
        {
            configuration.Sources.Insert(index + 1, source);
        }

        return configuration;
    }

    private static int LastIndexOfAppSettings(IList<IConfigurationSource> sources)
    {
        for (var i = sources.Count - 1; i >= 0; i--)
        {
            if (sources[i] is JsonConfigurationSource { Path: { } path }
                && path.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
