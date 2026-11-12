using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Doctrina.TestKit.Authentication;
using Microsoft.AspNetCore.WebUtilities;

// Tests volgen spec openspec/specs/user-authentication/spec.md:
// klasse = requirement, methode = scenario.
namespace Doctrina.Host.IntegrationTests.UserAuthentication;

public sealed class GebruikersLoggenInViaOpenIdConnect(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnoniemeGebruikerWordtNaarDeProviderGestuurd()
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.GetAsync(new Uri("/profiel", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal(factory.Idp.Configuration.AuthorizationEndpoint, location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));
        Assert.Equal(DoctrinaHostFactory.WebClientId, query["client_id"]);
    }

    [Fact]
    public async Task NaSuccesvolInloggenTerugNaarDeGevraagdePagina()
    {
        using var browser = factory.CreateBrowser();
        using var challenge = await browser.GetAsync(new Uri("/profiel", UriKind.Relative), Ct);

        using var callback = await browser.CompleteSignInAsync(challenge, factory.Idp, StubUser.Student, Ct);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/profiel", callback.Headers.Location?.OriginalString);
        using var page = await browser.GetAsync(new Uri("/profiel", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("data-testid=\"name\">Sanne Student<", await page.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PaginasZonderInlogeisBlijvenOpenbaar()
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.GetAsync(new Uri("/", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("Welkom bij Doctrina", html, StringComparison.Ordinal);
        Assert.Contains(">Inloggen<", html, StringComparison.Ordinal);
    }
}

public sealed class DeIdentiteitGebruiktEenProviderNeutraalClaimcontract(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RollenEnNaamUitHetToken()
    {
        using var browser = factory.CreateBrowser();

        var html = await browser.SignInAndGetAsync("/profiel", factory.Idp, new StubUser("sub-1", "Sanne Student", ["student"]), Ct);

        Assert.Contains("data-testid=\"name\">Sanne Student<", html, StringComparison.Ordinal);
        Assert.Contains("<li>student</li>", html, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"is-admin\">nee<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MeerdereRollen()
    {
        using var browser = factory.CreateBrowser();

        var html = await browser.SignInAndGetAsync("/profiel", factory.Idp, new StubUser("sub-2", "Beheer Doctrina", ["admin", "employee"]), Ct);

        Assert.Contains("<li>admin</li>", html, StringComparison.Ordinal);
        Assert.Contains("<li>employee</li>", html, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"is-admin\">ja<", html, StringComparison.Ordinal);
    }
}

public sealed class HetInlogschermVanDeProviderKanWordenOvergeslagen(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HintWordtMeegestuurdAlsDieIsIngesteld()
    {
        using var host = factory.WithSettings(("Authentication:Oidc:IdpHint", "entra"));
        using var browser = host.CreateBrowser();

        using var response = await browser.GetAsync(new Uri("/inloggen?returnUrl=%2Fprofiel", UriKind.Relative), Ct);

        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal("entra", query["kc_idp_hint"]);
    }

    [Fact]
    public async Task GeenHintAlsDieNietIsIngesteld()
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.GetAsync(new Uri("/inloggen", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(QueryHelpers.ParseQuery(response.Headers.Location!.Query).ContainsKey("kc_idp_hint"));
    }
}

public sealed class GebruikersLoggenUitBijDoctrinaEnBijDeProvider(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UitloggenBeeindigtBeideSessies()
    {
        using var browser = factory.CreateBrowser();
        var start = await browser.SignInAndGetAsync("/inloggen?returnUrl=%2F", factory.Idp, StubUser.Student, Ct);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["__RequestVerificationToken"] = BrowserSignIn.ReadAntiforgeryToken(start),
            ["returnUrl"] = "/",
        });

        using var signOut = await browser.PostAsync(new Uri("/uitloggen", UriKind.Relative), form, Ct);

        // Doctrina-sessie weg en doorgestuurd naar het end-session-endpoint van de provider
        Assert.Equal(HttpStatusCode.Redirect, signOut.StatusCode);
        var endSession = signOut.Headers.Location!;
        Assert.Equal(factory.Idp.Configuration.EndSessionEndpoint, endSession.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(endSession.Query);
        Assert.Equal(DoctrinaHostFactory.WebClientId, query["client_id"]);
        Assert.False(string.IsNullOrEmpty(query["id_token_hint"])); // zonder hint vraagt Keycloak om bevestiging
        Assert.Contains(signOut.Headers.GetValues("Set-Cookie"), c => c.StartsWith("doctrina.session=;", StringComparison.Ordinal));

        // Provider stuurt terug naar post_logout_redirect_uri → Doctrina stuurt naar de startpagina
        var callback = new Uri(query["post_logout_redirect_uri"].ToString());
        using var back = await browser.GetAsync(new Uri($"{callback.AbsolutePath}?state={Uri.EscapeDataString(query["state"].ToString())}", UriKind.Relative), Ct);
        Assert.Equal("/", back.Headers.Location?.OriginalString);

        using var profile = await browser.GetAsync(new Uri("/profiel", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Redirect, profile.StatusCode);
        Assert.StartsWith(factory.Idp.Configuration.AuthorizationEndpoint, profile.Headers.Location!.ToString(), StringComparison.Ordinal);
    }
}

public sealed class DeApiAuthenticeertBearerTokens(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static readonly Uri Me = new("/api/v1/me", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GeldigTokenVanEenSysteem()
    {
        using var client = factory.CreateBrowser();
        var token = factory.Idp.CreateAccessToken(DoctrinaHostFactory.ApiAudience, "doctrina-sis-sync", ["doctrina.import"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(Me, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var caller = await response.Content.ReadFromJsonAsync<Caller>(Ct);
        Assert.NotNull(caller);
        Assert.Equal("doctrina-sis-sync", caller.Client);
        Assert.Equal(["doctrina.import"], caller.Roles);
    }

    [Fact]
    public async Task OntbrekendToken()
    {
        using var client = factory.CreateBrowser();

        using var response = await client.GetAsync(Me, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task TokenVoorEenAndereAudience()
    {
        using var client = factory.CreateBrowser();
        var token = factory.Idp.CreateAccessToken("another-api", "doctrina-sis-sync", ["doctrina.import"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(Me, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SessiecookieWordtNietDoorDeApiGeaccepteerd()
    {
        using var browser = factory.CreateBrowser();
        _ = await browser.SignInAndGetAsync("/profiel", factory.Idp, StubUser.Beheerder, Ct);

        using var response = await browser.GetAsync(Me, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    private sealed record Caller(string? Subject, string? Client, string? Name, string[] Roles);
}
