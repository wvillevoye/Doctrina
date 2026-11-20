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

/// <summary>Starts Doctrina.Host in memory with the stubbed OIDC provider instead of Keycloak.</summary>
public sealed class DoctrinaHostFactory : WebApplicationFactory<Program>
{
    public const string WebClientId = "doctrina-web";
    public const string ApiAudience = "doctrina-api";

    public StubOidcProvider Idp { get; } = new();

    /// <summary>Variant with extra configuration, e.g. <c>Authentication:Oidc:IdpHint</c>.</summary>
    public WebApplicationFactory<Program> WithSettings(params (string Key, string? Value)[] settings) =>
        WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))));

    /// <summary>
    /// Variant in which the host knows <paramref name="known"/> (besides the product modules) and <paramref name="enabled"/>
    /// in <c>Modules:Enabled</c> staan.
    /// </summary>
    public WebApplicationFactory<Program> WithModules(IEnumerable<IModule> known, params string[] enabled) =>
        WithWebHostBuilder(builder =>
        {
            // Configuration arrays merge per index: blank the remaining positions, so a longer list from
            // appsettings.{Environment}.json (e.g. [Academic, Groups] in Development) doesn't leak through.
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                Enumerable.Range(0, Math.Max(enabled.Length, 16)).Select(i =>
                    new KeyValuePair<string, string?>($"{ModuleCatalog.EnabledSection}:{i}", i < enabled.Length ? enabled[i] : string.Empty))));
            builder.ConfigureServices(services =>
            {
                foreach (var module in known)
                {
                    services.AddDoctrinaModule(module);
                }
            });
        });

    /// <summary>Bearer token of a system without roles, for calls to <c>/api/v1</c>.</summary>
    public string CreateApiToken() => Idp.CreateAccessToken(ApiAudience, "doctrina-test-client", []);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Environment "Test": settings from src/Doctrina.Host/appsettings.Test.json, no appsettings.Secrets.json (design D13).
        builder.UseEnvironment(DoctrinaConfigurationExtensions.TestEnvironment);
        // Static files of Razor Class Libraries (_content/Doctrina.UI/...) are loaded by ASP.NET Core outside Development
        // only from the publish output; in tests they come from the build manifest.
        builder.UseStaticWebAssets();

        builder.ConfigureTestServices(services =>
        {
            var configurationManager = Idp.CreateConfigurationManager();
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
    /// <summary>Starts the host and returns the exception that makes start-up fail (or <see langword="null"/>).</summary>
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

    /// <summary>A "browser": does not follow redirects automatically, keeps cookies, speaks https.</summary>
    public static HttpClient CreateBrowser(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost"),
        });
}
