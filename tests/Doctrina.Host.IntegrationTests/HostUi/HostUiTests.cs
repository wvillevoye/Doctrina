using System.Net;
using System.Security.Claims;
using Bunit;
using Doctrina.Host.Components.Layout;
using Doctrina.Host.IntegrationTests.DataPersistence;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Modules;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Taak 10.1: kale host-layout (semantische HTML), inloggen/uitloggen, foutpagina en "Geen toegang" in de router.
namespace Doctrina.Host.IntegrationTests.HostUi;

public sealed class DeHostLayoutIsToegankelijkEnKaal(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnoniemeBezoekerZietInloggenEnEenSkiplink()
    {
        using var browser = factory.CreateBrowser();

        var html = await browser.GetStringAsync(new Uri("/", UriKind.Relative), Ct);

        Assert.Contains("<html lang=\"nl\">", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#inhoud\"", html, StringComparison.Ordinal);
        Assert.Contains("<main id=\"inhoud\" tabindex=\"-1\">", html, StringComparison.Ordinal);
        Assert.Contains("<nav aria-label=\"Hoofdmenu\">", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/inloggen?returnUrl=%2F\">Inloggen<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"btn", html, StringComparison.Ordinal); // nog geen Bootstrap
    }

    [Fact]
    public async Task IngelogdeGebruikerZietNaamEnUitloggen()
    {
        using var browser = factory.CreateBrowser();

        var html = await browser.SignInAndGetAsync("/profiel", factory.Idp, StubUser.Student, Ct);

        Assert.Contains("data-testid=\"account-name\">Sanne Student<", html, StringComparison.Ordinal);
        Assert.Contains(">Uitloggen</button>", html, StringComparison.Ordinal);
        Assert.Contains(">Mijn profiel<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FoutInEenPaginaToontDeFoutpaginaZonderDetails()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName).InEnvironment(Environments.Production);
        using var browser = host.CreateBrowser();

        using var response = await browser.GetAsync(new Uri("/sample/fout", UriKind.Relative), Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("<h1>Er ging iets mis</h1>", html, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"trace-id\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Geheime paginafout", html, StringComparison.Ordinal);
        Assert.DoesNotContain("I6123456", html, StringComparison.Ordinal);
    }
}

public sealed class NietToegestaanInDeRouter : IDisposable
{
    private readonly BunitContext _context = new();

    [Fact]
    public void IngelogdeGebruikerZietGeenToegang()
    {
        _context.AddAuthorization().SetAuthorized("Sanne Student");

        var view = _context.Render<NietToegestaan>();

        Assert.Equal("Geen toegang", view.Find("h1").TextContent);
    }

    [Fact]
    public void AnoniemeGebruikerWordtNaarInloggenGestuurd()
    {
        _context.AddAuthorization().SetNotAuthorized();
        var navigation = _context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/sample/beheer");

        var view = _context.Render<NietToegestaan>();

        Assert.Empty(view.FindAll("h1"));
        Assert.EndsWith("/inloggen?returnUrl=%2Fsample%2Fbeheer", navigation.Uri, StringComparison.Ordinal);
    }

    public void Dispose() => _context.Dispose();
}

public sealed class ProfielToontDeHerkomstVanPermissies(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task PermissiesPerRolStaanOpHetProfiel()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName);
        using var browser = host.CreateBrowser();

        var html = await browser.SignInAndGetAsync("/profiel", factory.Idp, StubUser.Beheerder, Ct);

        Assert.Contains("<li>admin: Sample.Manage, Sample.Read</li>", html, StringComparison.Ordinal);
        Assert.Contains("<li>employee: geen</li>", html, StringComparison.Ordinal);
    }
}
