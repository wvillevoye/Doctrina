using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Bunit;
using Doctrina.Host.Components.Layout;
using Doctrina.Host.IntegrationTests.DataPersistence;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Modules;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Spec app-shell (add-design-system, taak 6.2): de layout en pagina's van de host, opgebouwd uit Doctrina.UI.
// Klasse = requirement, methode = scenario.
namespace Doctrina.Host.IntegrationTests.HostUi;

internal static class Html
{
    private static readonly HtmlParser Parser = new();

    public static IDocument Parse(string html) => Parser.ParseDocument(html);

    /// <summary>Elementen die met Tab bereikbaar zijn, in documentvolgorde.</summary>
    public static IEnumerable<IElement> Focusable(this IDocument document) =>
        document.Body!.QuerySelectorAll("a[href], button, input:not([type=hidden]), select, textarea, [tabindex]")
            .Where(e => e.GetAttribute("tabindex") != "-1" && !e.HasAttribute("disabled"));
}

public sealed class ElkePaginaHeeftDezelfdeToegankelijkeOpbouw(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task OpbouwVanEenPagina()
    {
        using var browser = factory.CreateBrowser();

        var page = Html.Parse(await browser.GetStringAsync(new Uri("/", UriKind.Relative), Ct));
        var first = page.Focusable().First();

        Assert.Equal("nl", page.DocumentElement.GetAttribute("lang"));
        Assert.Equal("Naar de inhoud", first.TextContent);
        Assert.Equal("#inhoud", first.GetAttribute("href"));
        Assert.Single(page.QuerySelectorAll("body > header"));
        Assert.NotNull(page.QuerySelector("header nav[aria-label=Hoofdmenu]"));
        Assert.Equal("inhoud", page.QuerySelector("body > main")?.Id);
        Assert.Single(page.QuerySelectorAll("main h1"));
        Assert.Equal("Start · Doctrina", page.Title);
    }

    [Fact]
    public async Task Paginatitel()
    {
        using var browser = factory.CreateBrowser();

        var page = Html.Parse(await browser.SignInAndGetAsync("/profiel", factory.Idp, StubUser.Student, Ct));

        Assert.Single(page.QuerySelectorAll("title"));
        Assert.Equal("Mijn profiel · Doctrina", page.Title);
        Assert.Equal("Mijn profiel", page.QuerySelector("main h1")?.TextContent);
    }

    [Fact]
    public async Task FoutInEenPaginaToontDeFoutpaginaZonderDetails()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName).InEnvironment(Environments.Production);
        using var browser = host.CreateBrowser();

        using var response = await browser.GetAsync(new Uri("/sample/fout", UriKind.Relative), Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);
        var page = Html.Parse(html);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("Er ging iets mis", page.QuerySelector("main h1")?.TextContent);
        Assert.Equal("Er ging iets mis · Doctrina", page.Title);
        Assert.NotNull(page.QuerySelector("[data-testid=trace-id]"));
        Assert.DoesNotContain("Geheime paginafout", html, StringComparison.Ordinal);
        Assert.DoesNotContain("I6123456", html, StringComparison.Ordinal);
    }
}

public sealed class HetAccountgedeelteToontWieErIsIngelogd(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnoniemeBezoeker()
    {
        using var browser = factory.CreateBrowser();

        var page = Html.Parse(await browser.GetStringAsync(new Uri("/?tab=2", UriKind.Relative), Ct));
        var signIn = page.QuerySelector("header [data-testid=sign-in]");

        Assert.NotNull(signIn);
        Assert.Equal("Inloggen", signIn.TextContent.Trim());
        Assert.Equal("/inloggen?returnUrl=%2F%3Ftab%3D2", signIn.GetAttribute("href"));
        Assert.Null(page.QuerySelector("[data-testid=account-name]"));
    }

    [Fact]
    public async Task IngelogdeGebruiker()
    {
        using var browser = factory.CreateBrowser();

        var page = Html.Parse(await browser.SignInAndGetAsync("/profiel", factory.Idp, StubUser.Student, Ct));
        var toggle = page.QuerySelector("header button[popovertarget=dc-accountmenu]");
        var menu = page.QuerySelector("#dc-accountmenu[popover]");

        Assert.NotNull(toggle);
        Assert.NotNull(menu);
        Assert.Equal("Sanne Student", toggle.QuerySelector("[data-testid=account-name]")?.TextContent);
        Assert.Equal("Mijn profiel", menu.QuerySelector("a[href='/profiel']")?.TextContent.Trim());
        Assert.Equal("Uitloggen", menu.QuerySelector("form[action='/uitloggen'] button[type=submit]")?.TextContent.Trim());
        Assert.Null(page.QuerySelector("[data-testid=sign-in]"));
    }
}

public sealed class HetHoofdmenuWerktOpElkSchermformaat(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task HuidigePaginaInHetMenu()
    {
        using var browser = factory.CreateBrowser();

        var page = Html.Parse(await browser.SignInAndGetAsync("/profiel", factory.Idp, StubUser.Student, Ct));
        var current = page.QuerySelectorAll("nav[aria-label=Hoofdmenu] a[aria-current=page]");

        Assert.Equal("Mijn profiel", Assert.Single(current).TextContent);
        Assert.Equal("Menu", page.QuerySelector("nav[aria-label=Hoofdmenu] button[popovertarget]")?.TextContent.Trim());
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
