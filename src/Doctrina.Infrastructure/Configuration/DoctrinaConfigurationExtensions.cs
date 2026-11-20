using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;

namespace Doctrina.Infrastructure.Configuration;

/// <summary>Configuration files per environment and the secrets file (design D13).</summary>
public static class DoctrinaConfigurationExtensions
{
    /// <summary>Uncommitted file with secrets; example in <c>appsettings.Secrets.example.json</c>.</summary>
    public const string SecretsFile = "appsettings.Secrets.json";

    /// <summary>Environment of the automated tests (<c>appsettings.Test.json</c>).</summary>
    public const string TestEnvironment = "Test";

    /// <summary>Whether this is the environment of the automated tests.</summary>
    public static bool IsTest(this IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return environment.IsEnvironment(TestEnvironment);
    }

    /// <summary>
    /// Adds the optional <see cref="SecretsFile"/> right after <c>appsettings.{environment}.json</c>, so before user
    /// secrets, environment variables and command-line arguments. In the <see cref="TestEnvironment"/> environment it is
    /// deliberately not loaded, so local secrets never end up in the tests.
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
