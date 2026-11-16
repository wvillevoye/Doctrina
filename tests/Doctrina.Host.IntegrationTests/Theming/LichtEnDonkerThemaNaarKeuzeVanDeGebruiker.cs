using System.Net;
using Doctrina.TestKit.Authentication;
using Doctrina.UI.Theming;

// Spec theming, requirement "Licht en donker thema naar keuze van de gebruiker"; taak 3.1, design D3.
namespace Doctrina.Host.IntegrationTests.Theming;

public sealed class LichtEnDonkerThemaNaarKeuzeVanDeGebruiker(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZonderKeuzeVolgtHetThemaHetSysteem()
    {
        using var browser = factory.CreateBrowser();

        var html = await browser.GetStringAsync(new Uri("/", UriKind.Relative), Ct);

        Assert.Contains("<html lang=\"en\" data-dc-theme=\"system\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-bs-theme", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GekozenDonkerThemaWordtOnthouden()
    {
        using var browser = factory.CreateBrowser();

        using var response = await ChooseAsync(browser, "dark", returnUrl: "/access-denied");
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(DcThemes.CookieName + "=", StringComparison.Ordinal));
        var html = await browser.GetStringAsync(new Uri("/", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/access-denied", response.Headers.Location?.OriginalString);
        Assert.StartsWith("doctrina.theme=dark;", cookie, StringComparison.Ordinal);
        Assert.Contains("max-age=31536000", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        // Eerste weergave meteen donker: het attribuut staat al in de HTML van de server, zonder script.
        Assert.Contains("<html lang=\"en\" data-dc-theme=\"dark\" data-bs-theme=\"dark\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TerugNaarDeSysteeminstelling()
    {
        using var browser = factory.CreateBrowser();
        (await ChooseAsync(browser, "dark")).Dispose();

        using var response = await ChooseAsync(browser, "system");
        var html = await browser.GetStringAsync(new Uri("/", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("<html lang=\"en\" data-dc-theme=\"system\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThemakeuzeWerktZonderScripts()
    {
        using var browser = factory.CreateBrowser();
        var page = await browser.GetStringAsync(new Uri("/?tab=2", UriKind.Relative), Ct);

        // Gewoon HTML-formulier met submitknoppen; de huidige pagina gaat mee als terugkeeradres.
        Assert.Contains("<form method=\"post\" action=\"/theme\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"returnUrl\" value=\"/?tab=2\"", page, StringComparison.Ordinal);
        Assert.Contains("type=\"submit\" name=\"theme\" value=\"light\"", page, StringComparison.Ordinal);

        using var response = await ChooseAsync(browser, "light", returnUrl: "/?tab=2", page: page);
        var html = await browser.GetStringAsync(new Uri("/", UriKind.Relative), Ct);

        Assert.Equal("/?tab=2", response.Headers.Location?.OriginalString);
        Assert.Contains("data-dc-theme=\"light\" data-bs-theme=\"light\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnbekendThemaOfExternTerugkeeradresWordtGeweigerd()
    {
        using var browser = factory.CreateBrowser();

        using var unknown = await ChooseAsync(browser, "paars");
        using var external = await ChooseAsync(browser, "dark", returnUrl: "https://elders.example/");

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal("/", external.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ZonderAntiforgeryTokenGeenKeuze()
    {
        using var browser = factory.CreateBrowser();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal) { ["theme"] = "dark" });

        using var response = await browser.PostAsync(new Uri(DcThemes.EndpointPath, UriKind.Relative), content, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie") && response.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith(DcThemes.CookieName, StringComparison.Ordinal)));
    }

    /// <summary>Verstuurt het themaformulier zoals een browser zonder scripts dat doet.</summary>
    private static async Task<HttpResponseMessage> ChooseAsync(HttpClient browser, string theme, string returnUrl = "/", string? page = null)
    {
        page ??= await browser.GetStringAsync(new Uri("/", UriKind.Relative), Ct);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["__RequestVerificationToken"] = BrowserSignIn.ReadAntiforgeryToken(page),
            ["theme"] = theme,
            ["returnUrl"] = returnUrl,
        });

        return await browser.PostAsync(new Uri(DcThemes.EndpointPath, UriKind.Relative), content, Ct);
    }
}

// Spec app-shell, requirement "De themakeuze staat in de header" (add-design-system, taak 6.1/6.2): de hele keten.
public sealed class DeThemakeuzeStaatInDeHeader(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task ActieveThemakeuze()
    {
        using var browser = factory.CreateBrowser();
        var start = await browser.GetStringAsync(new Uri("/", UriKind.Relative), Ct);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["__RequestVerificationToken"] = BrowserSignIn.ReadAntiforgeryToken(start),
            ["theme"] = "dark",
            ["returnUrl"] = "/",
        });
        (await browser.PostAsync(new Uri(DcThemes.EndpointPath, UriKind.Relative), content, Ct)).Dispose();

        var page = HostUi.Html.Parse(await browser.GetStringAsync(new Uri("/", UriKind.Relative), Ct));
        var options = page.QuerySelectorAll("header [data-testid=theme-switcher] button[name=theme]");

        Assert.Equal(["light", "dark", "system"], options.Select(b => b.GetAttribute("value")));
        Assert.Equal(["false", "true", "false"], options.Select(b => b.GetAttribute("aria-pressed")));
        Assert.Equal("Dark", options[1].TextContent.Trim());
    }
}
