using System.Net;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;

// Tests follow the delta spec of change add-lti-platform (module-system):
// class = requirement, method = scenario.
namespace Doctrina.Host.IntegrationTests.ModuleSystem;

public sealed class ModulesKunnenBrowserEndpointsLeveren(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SampleModule Module() => new()
    {
        BrowserEndpoints = app =>
        {
            app.MapGet("ping", () => "pong");
            app.MapGet("open", () => "open").AllowAnonymous();
        },
    };

    private async Task<HttpStatusCode> SignedInStatusAsync(WebApplicationFactory<Program> host, string path)
    {
        using var browser = host.CreateBrowser();
        await browser.SignInAndGetAsync("/profile", factory.Idp, StubUser.Student, Ct);
        using var response = await browser.GetAsync(new Uri(path, UriKind.Relative), Ct);
        return response.StatusCode;
    }

    [Fact]
    public async Task BrowserEndpointVanEenIngeschakeldeModuleIsBereikbaar()
    {
        using var host = factory.WithModules([Module()], SampleModule.ModuleName);

        Assert.Equal(HttpStatusCode.OK, await SignedInStatusAsync(host, "/sample/ping"));
    }

    [Fact]
    public async Task BrowserEndpointVanEenUitgeschakeldeModuleGeeft404()
    {
        using var host = factory.WithModules([Module()]);

        Assert.Equal(HttpStatusCode.NotFound, await SignedInStatusAsync(host, "/sample/ping"));
    }

    [Fact]
    public async Task BrowserEndpointVereistStandaardInloggen()
    {
        using var host = factory.WithModules([Module()], SampleModule.ModuleName);
        using var browser = host.CreateBrowser();

        using var response = await browser.GetAsync(new Uri("/sample/ping", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("pong", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BrowserEndpointKanExpliciotAnoniemZijn()
    {
        using var host = factory.WithModules([Module()], SampleModule.ModuleName);
        using var browser = host.CreateBrowser();

        using var response = await browser.GetAsync(new Uri("/sample/open", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("open", await response.Content.ReadAsStringAsync(Ct));
    }
}
