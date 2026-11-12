using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Doctrina.Core.Modules;
using Doctrina.Infrastructure.Authorization;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

// Tests volgen spec openspec/changes/add-modular-foundation/specs/access-control/spec.md:
// klasse = requirement, methode = scenario.
namespace Doctrina.Host.IntegrationTests.AccessControl;

public static class AccessHosts
{
    public static WebApplicationFactory<Program> WithAppSettings(this WebApplicationFactory<Program> host, params (string Key, string? Value)[] settings) =>
        host.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))));

    public static string Mapping(string role, int index) => $"{PermissionRegistry.RolePermissionsSection}:{role}:{index}";

    /// <summary>Profielpagina van een ingelogde gebruiker, met gedecodeerde HTML.</summary>
    public static async Task<string> ProfileOfAsync(this WebApplicationFactory<Program> host, DoctrinaHostFactory root, StubUser user, CancellationToken ct)
    {
        using var browser = host.CreateBrowser();
        return WebUtility.HtmlDecode(await browser.SignInAndGetAsync("/profiel", root.Idp, user, ct));
    }

    public static HttpClient ApiClient(this WebApplicationFactory<Program> host, DoctrinaHostFactory root, params string[] roles)
    {
        var client = host.CreateBrowser();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            root.Idp.CreateAccessToken(DoctrinaHostFactory.ApiAudience, "doctrina-test-client", roles));
        return client;
    }
}

public sealed class ModulesDeclarerenPermissies(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GedeclareerdePermissiesZijnBekend()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName);

        var profile = await host.ProfileOfAsync(factory, StubUser.Beheerder, Ct);

        Assert.Contains("<code>Sample.Read</code> – Voorbeelden bekijken", profile, StringComparison.Ordinal);
        Assert.Contains("<code>Sample.Manage</code> – Voorbeelden beheren", profile, StringComparison.Ordinal);
    }

    [Fact]
    public void DubbelePermissienaamLaatHetOpstartenMislukken()
    {
        var other = new TestModule("Other") { DeclaredPermissions = { new Permission("Sample.Read", "Ook voorbeelden bekijken") } };
        using var host = factory.WithModules([new SampleModule(), other], SampleModule.ModuleName, "Other");

        var failure = host.StartupFailure();

        Assert.NotNull(failure);
        Assert.Contains("Permissie 'Sample.Read' wordt door meerdere modules gedeclareerd: Sample, Other.", failure.Errors);
    }
}

public sealed class RollenWordenViaConfiguratieAanPermissiesGekoppeld(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PermissiesUitMeerdereRollenWordenSamengevoegd()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName)
            .WithAppSettings((AccessHosts.Mapping("employee", 0), "Sample.Read"), (AccessHosts.Mapping("admin", 0), "Sample.Manage"));

        var profile = await host.ProfileOfAsync(factory, new StubUser("sub-mm", "Mia Medewerker", ["employee", "admin"]), Ct);

        Assert.Contains("<code>Sample.Read</code>", profile, StringComparison.Ordinal);
        Assert.Contains("<code>Sample.Manage</code>", profile, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JokertekenGeeftElkePermissie()
    {
        // Standaardkoppeling uit appsettings.json: admin → *.
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName);

        var profile = await host.ProfileOfAsync(factory, new StubUser("sub-ad", "Ada Admin", ["admin"]), Ct);

        Assert.Contains("<code>Sample.Read</code>", profile, StringComparison.Ordinal);
        Assert.Contains("<code>Sample.Manage</code>", profile, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NietGekoppeldeRolGeeftNiets()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName);

        var profile = await host.ProfileOfAsync(factory, new StubUser("sub-gg", "Gijs Gast", ["gast"]), Ct);

        Assert.Contains("data-testid=\"name\">Gijs Gast<", profile, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"permissions\">geen<", profile, StringComparison.Ordinal);
    }

    [Fact]
    public void KoppelingNaarEenOnbekendePermissieLaatHetOpstartenMislukken()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName)
            .WithAppSettings((AccessHosts.Mapping("admin", 1), "Unknown.Do"));

        var failure = host.StartupFailure();

        Assert.NotNull(failure);
        Assert.Contains($"Rol 'admin' in {PermissionRegistry.RolePermissionsSection} verwijst naar onbekende permissie 'Unknown.Do'.", failure.Errors);
    }
}

public sealed class SysteempermissiesGebruikenDezelfdeKoppeling(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ClientpermissieGeeftToegangTotDeApi()
    {
        var import = new TestModule("Import")
        {
            DeclaredPermissions = { new Permission("Import.Execute", "Imports uitvoeren") },
            Endpoints = api => api.MapGet("imports", () => "ok").RequirePermission("Import.Execute"),
        };
        using var host = factory.WithModules([import], "Import")
            .WithAppSettings((AccessHosts.Mapping("doctrina.import", 0), "Import.Execute"));
        using var system = host.ApiClient(factory, "doctrina.import");
        using var otherSystem = host.ApiClient(factory, "doctrina.persons.read");

        using var allowed = await system.GetAsync(new Uri("/api/v1/imports", UriKind.Relative), Ct);
        using var refused = await otherSystem.GetAsync(new Uri("/api/v1/imports", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }
}

public sealed class PaginasEnEndpointsVereisenPermissies(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PaginaGeweigerdZonderPermissie()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName);
        using var browser = host.CreateBrowser();
        await browser.SignInAndGetAsync("/profiel", factory.Idp, StubUser.Student, Ct);

        using var refused = await browser.GetAsync(new Uri("/sample/beheer", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        Assert.StartsWith("/geen-toegang", refused.Headers.Location!.PathAndQuery, StringComparison.Ordinal);
        using var page = await browser.GetAsync(new Uri(refused.Headers.Location.PathAndQuery, UriKind.Relative), Ct);
        using var stillSignedIn = await browser.GetAsync(new Uri("/profiel", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, page.StatusCode);
        Assert.Contains("<h1>Geen toegang</h1>", await page.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, stillSignedIn.StatusCode);
    }

    [Fact]
    public async Task ApiGeweigerdZonderPermissie()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName);
        using var client = host.ApiClient(factory, "employee");

        using var response = await client.GetAsync(new Uri("/api/v1/samples/beheer", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(403, problem.GetProperty("status").GetInt32());
        Assert.Equal("Auth.Forbidden", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PaginaToegestaanMetPermissie()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName);
        using var browser = host.CreateBrowser();

        var html = await browser.SignInAndGetAsync("/sample/beheer", factory.Idp, StubUser.Beheerder, Ct);

        Assert.Contains($"<h1>{SampleModule.ManagePageTitle}</h1>", html, StringComparison.Ordinal);
    }
}

public sealed class EndpointsZijnStandaardBeveiligd(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OngemarkeerdEndpointVereistAuthenticatie()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName);
        using var client = host.CreateBrowser();

        using var response = await client.GetAsync(new Uri("/api/v1/samples", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Auth.Unauthorized", problem.GetProperty("code").GetString());
    }
}
