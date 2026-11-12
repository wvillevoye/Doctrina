using Doctrina.Core.Modules;
using Doctrina.Infrastructure.Configuration;
using Doctrina.Infrastructure.Modules;
using Doctrina.TestKit.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Doctrina.Host.IntegrationTests;

/// <summary>Start Doctrina.Host in het geheugen met de nagebootste OIDC-provider in plaats van Keycloak.</summary>
public sealed class DoctrinaHostFactory : WebApplicationFactory<Program>
{
    public const string WebClientId = "doctrina-web";
    public const string ApiAudience = "doctrina-api";

    public StubOidcProvider Idp { get; } = new();

    /// <summary>Variant met extra configuratie, bv. <c>Authentication:Oidc:IdpHint</c>.</summary>
    public WebApplicationFactory<Program> WithSettings(params (string Key, string? Value)[] settings) =>
        WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))));

    /// <summary>
    /// Variant waarin de host <paramref name="known"/> kent (naast de productmodules) en <paramref name="enabled"/>
    /// in <c>Modules:Enabled</c> staan.
    /// </summary>
    public WebApplicationFactory<Program> WithModules(IEnumerable<IModule> known, params string[] enabled) =>
        WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                enabled.Select((name, i) => new KeyValuePair<string, string?>($"{ModuleCatalog.EnabledSection}:{i}", name))));
            builder.ConfigureServices(services =>
            {
                foreach (var module in known)
                {
                    services.AddDoctrinaModule(module);
                }
            });
        });

    /// <summary>Bearer-token van een systeem zonder rollen, voor aanroepen van <c>/api/v1</c>.</summary>
    public string CreateApiToken() => Idp.CreateAccessToken(ApiAudience, "doctrina-test-client", []);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Omgeving "Test": instellingen uit src/Doctrina.Host/appsettings.Test.json, geen appsettings.Secrets.json (design D13).
        builder.UseEnvironment(DoctrinaConfigurationExtensions.TestEnvironment);
        // Statische bestanden van Razor Class Libraries (_content/Doctrina.UI/...) laadt ASP.NET Core buiten Development
        // alleen uit de publish-uitvoer; in tests komen ze uit het manifest van de build.
        builder.UseStaticWebAssets();

        builder.ConfigureTestServices(services =>
        {
            var configurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(Idp.Configuration);
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.ConfigurationManager = configurationManager;
                options.Backchannel = Idp.CreateBackchannelClient();
            });
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.ConfigurationManager = configurationManager);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Idp.Dispose();
        }

        base.Dispose(disposing);
    }
}

public static class HostFactoryExtensions
{
    /// <summary>Start de host en geeft de exception terug waarmee het opstarten mislukt (of <see langword="null"/>).</summary>
    public static ModuleConfigurationException? StartupFailure(this WebApplicationFactory<Program> factory)
    {
        var exception = Record.Exception(() => factory.CreateClient().Dispose());
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is ModuleConfigurationException moduleException)
            {
                return moduleException;
            }
        }

        return exception is null ? null : throw new InvalidOperationException("Opstarten mislukte om een andere reden.", exception);
    }

    /// <summary>Een "browser": volgt geen redirects automatisch, bewaart cookies, praat https.</summary>
    public static HttpClient CreateBrowser(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost"),
        });
}
