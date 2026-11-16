using System.Net;
using System.Text.RegularExpressions;
using Doctrina.Modules.Lti.Application;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Modules;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// Spec lti-launch of change add-lti-platform; tasks 4.2 and 4.3. Class = requirement, method = scenario.
namespace Doctrina.Host.IntegrationTests.Academic;

/// <summary>A user's browser session on a <see cref="LaunchScenario"/>: starts the launch, then plays the tool's authentication request.</summary>
internal sealed class ToolSession : IDisposable
{
    private readonly LaunchScenario _scenario;
    private readonly HttpClient _browser;

    private ToolSession(LaunchScenario scenario, HttpClient browser)
    {
        _scenario = scenario;
        _browser = browser;
    }

    /// <summary>The parameters of the login initiation, as the tool receives them.</summary>
    public Dictionary<string, string> Login { get; set; } = [];

    public static async Task<ToolSession> SignInAsync(LaunchScenario scenario, DoctrinaHostFactory factory, StubUser user, CancellationToken ct)
    {
        var browser = scenario.Academic.Host.CreateBrowser();
        await browser.SignInAndGetAsync("/profile", factory.Idp, user, ct);
        return new ToolSession(scenario, browser);
    }

    /// <summary>Starts the launch for the tool link and keeps the login initiation parameters.</summary>
    public async Task<ToolSession> StartAsync(CancellationToken ct)
    {
        using var start = await _browser.GetAsync(new Uri($"/lti/launch/{_scenario.Link.Id:D}", UriKind.Relative), ct);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        Login = QueryHelpers.ParseQuery(start.Headers.Location!.Query).ToDictionary(p => p.Key, p => p.Value.ToString());
        return this;
    }

    /// <summary>Starts a deep linking request for the section and keeps the login initiation parameters (change add-lti-deep-linking).</summary>
    public async Task<ToolSession> StartDeepLinkingAsync(CancellationToken ct)
    {
        using var start = await _browser.GetAsync(new Uri($"/lti/deep-linking/{_scenario.Section.Id:D}/{_scenario.Tool.Id:D}", UriKind.Relative), ct);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        Login = QueryHelpers.ParseQuery(start.Headers.Location!.Query).ToDictionary(p => p.Key, p => p.Value.ToString());
        return this;
    }

    /// <summary>The authentication request a well-behaved tool sends back; <paramref name="tweak"/> may change or remove (null) parameters.</summary>
    public Task<HttpResponseMessage> AuthorizeAsync(CancellationToken ct, Action<Dictionary<string, string?>>? tweak = null)
    {
        var query = new Dictionary<string, string?>
        {
            ["scope"] = "openid",
            ["response_type"] = "id_token",
            ["response_mode"] = "form_post",
            ["prompt"] = "none",
            ["client_id"] = _scenario.Tool.ClientId,
            ["redirect_uri"] = "https://tool.example.org/launch",
            ["login_hint"] = Login["login_hint"],
            ["lti_message_hint"] = Login["lti_message_hint"],
            ["nonce"] = "nonce-1",
            ["state"] = "state-1",
        };
        tweak?.Invoke(query);
        var url = QueryHelpers.AddQueryString("/lti/authorize", query.Where(p => p.Value is not null));
        return _browser.GetAsync(new Uri(url, UriKind.Relative), ct);
    }

    public void Dispose() => _browser.Dispose();
}

internal static class LaunchForms
{
    public static string Field(string html, string name) =>
        WebUtility.HtmlDecode(Regex.Match(html, $"name=\"{name}\" value=\"([^\"]*)\"").Groups[1].Value);

    /// <summary>Verifies the token against the published JWKS of the platform and returns its claims.</summary>
    public static async Task<JsonWebToken> VerifiedAsync(AcademicHost academic, string token, string clientId, CancellationToken ct)
    {
        using var anonymous = academic.Host.CreateBrowser();
        var jwks = new JsonWebKeySet(await anonymous.GetStringAsync(new Uri("/lti/jwks.json", UriKind.Relative), ct));
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidIssuer = "https://localhost",
            ValidAudience = clientId,
        });
        Assert.True(result.IsValid, result.Exception?.ToString());
        return (JsonWebToken)result.SecurityToken;
    }
}

public sealed class AuthenticatieverzoekVanDeTool(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GeldigVerzoekGeeftEenFormulierMetIdTokenEnState()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        using var session = await (await ToolSession.SignInAsync(s, factory, StubUser.Student, Ct)).StartAsync(Ct);

        using var response = await session.AuthorizeAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var html = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("method=\"post\" action=\"https://tool.example.org/launch\"", html, StringComparison.Ordinal);
        Assert.Equal("state-1", LaunchForms.Field(html, "state"));
        Assert.False(string.IsNullOrEmpty(LaunchForms.Field(html, "id_token")));
        Assert.Contains("<noscript>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnbekendeClientIdWordtGeweigerd()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        using var session = await (await ToolSession.SignInAsync(s, factory, StubUser.Student, Ct)).StartAsync(Ct);

        using var response = await session.AuthorizeAsync(Ct, q => q["client_id"] = "unknown");

        await AssertRefusedAsync(response, HttpStatusCode.BadRequest, "Lti.Launch.UnknownClient");
    }

    [Fact]
    public async Task NietGeregistreerdeRedirectUriWordtGeweigerd()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        using var session = await (await ToolSession.SignInAsync(s, factory, StubUser.Student, Ct)).StartAsync(Ct);

        using var response = await session.AuthorizeAsync(Ct, q => q["redirect_uri"] = "https://evil.example.org/steal");

        await AssertRefusedAsync(response, HttpStatusCode.BadRequest, "Lti.Launch.InvalidRedirectUri");
        Assert.Null(response.Headers.Location); // never a redirect to an unregistered address
        Assert.DoesNotContain("evil.example.org", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nonce")]
    [InlineData("state")]
    [InlineData("login_hint")]
    [InlineData("lti_message_hint")]
    public async Task OntbrekendeNonceOfStateWordtGeweigerd(string missing)
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        using var session = await (await ToolSession.SignInAsync(s, factory, StubUser.Student, Ct)).StartAsync(Ct);

        using var response = await session.AuthorizeAsync(Ct, q => q[missing] = null);

        await AssertRefusedAsync(response, HttpStatusCode.BadRequest, "Lti.Launch.InvalidRequest");
    }

    [Theory]
    [InlineData("response_type", "code")]
    [InlineData("response_mode", "query")]
    [InlineData("scope", "profile")]
    [InlineData("prompt", "login")]
    public async Task OnjuisteResponseTypeOfResponseModeWordtGeweigerd(string parameter, string value)
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        using var session = await (await ToolSession.SignInAsync(s, factory, StubUser.Student, Ct)).StartAsync(Ct);

        using var response = await session.AuthorizeAsync(Ct, q => q[parameter] = value);

        await AssertRefusedAsync(response, HttpStatusCode.BadRequest, "Lti.Launch.InvalidRequest");
    }

    [Fact]
    public async Task VerlopenLtiMessageHintWordtGeweigerd()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        using var session = await (await ToolSession.SignInAsync(s, factory, StubUser.Student, Ct)).StartAsync(Ct);
        var expired = ProtectedHint(s, $"{s.Link.Id:N}.{session.Login["login_hint"]}.{DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()}");

        using var response = await session.AuthorizeAsync(Ct, q => q["lti_message_hint"] = expired);

        await AssertRefusedAsync(response, HttpStatusCode.BadRequest, "Lti.Launch.InvalidHint");
    }

    [Fact]
    public async Task VervalsteLtiMessageHintWordtGeweigerd()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        using var session = await (await ToolSession.SignInAsync(s, factory, StubUser.Student, Ct)).StartAsync(Ct);

        using var response = await session.AuthorizeAsync(Ct, q => q["lti_message_hint"] = "forged-hint");

        await AssertRefusedAsync(response, HttpStatusCode.BadRequest, "Lti.Launch.InvalidHint");
    }

    [Fact]
    public async Task HintVanEenAndereGebruikerWordtGeweigerd()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        using var student = await (await ToolSession.SignInAsync(s, factory, StubUser.Student, Ct)).StartAsync(Ct);
        using var other = await ToolSession.SignInAsync(s, factory, GroupsSeed.Student2, Ct);
        other.Login = student.Login; // the other user got hold of the hints of the student

        using var response = await other.AuthorizeAsync(Ct);

        await AssertRefusedAsync(response, HttpStatusCode.Forbidden, "Lti.Launch.UserMismatch");
    }

    [Fact]
    public async Task ItemDatIntussenOnzichtbaarIsStartNiet()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        using var session = await (await ToolSession.SignInAsync(s, factory, StubUser.Student, Ct)).StartAsync(Ct);
        await s.Academic.ContentAsync(c => c.SetItemPublishedAsync(s.Link.Id, false, CancellationToken.None));

        using var response = await session.AuthorizeAsync(Ct);

        await AssertRefusedAsync(response, HttpStatusCode.Forbidden, "Lti.Launch.Forbidden");
    }

    private static string ProtectedHint(LaunchScenario s, string payload) =>
        s.Academic.Host.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(LaunchService.HintPurpose).Protect(payload);

    private static async Task AssertRefusedAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains(code, body, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"id_token\"", body, StringComparison.Ordinal);
    }
}

public sealed class InhoudVanDeIdToken(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private const string Claim = "https://purl.imsglobal.org/spec/lti/claim/";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<JsonWebToken> TokenAsync(LaunchScenario s, StubUser user)
    {
        using var session = await (await ToolSession.SignInAsync(s, factory, user, Ct)).StartAsync(Ct);
        using var response = await session.AuthorizeAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = LaunchForms.Field(await response.Content.ReadAsStringAsync(Ct), "id_token");
        return await LaunchForms.VerifiedAsync(s.Academic, token, s.Tool.ClientId, Ct);
    }

    [Fact]
    public async Task TokenIsOndertekendEnControleerbaar()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        var token = await TokenAsync(s, StubUser.Student);

        Assert.Equal("RS256", token.Alg);
        Assert.False(string.IsNullOrEmpty(token.Kid));
        Assert.True(token.ValidTo - token.IssuedAt <= TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TokenBevatDeVerplichteClaims()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        var token = await TokenAsync(s, StubUser.Student);

        Assert.Equal("https://localhost", token.Issuer);
        Assert.Equal([s.Tool.ClientId], token.Audiences);
        Assert.Equal("nonce-1", token.GetClaim("nonce").Value);
        Assert.Matches("^[0-9a-f]{32}$", token.Subject);
        Assert.Equal("LtiResourceLinkRequest", token.GetClaim(Claim + "message_type").Value);
        Assert.Equal("1.3.0", token.GetClaim(Claim + "version").Value);
        Assert.Equal(s.Tool.DeploymentId, token.GetClaim(Claim + "deployment_id").Value);
        Assert.Equal("https://tool.example.org/launch", token.GetClaim(Claim + "target_link_uri").Value);
    }

    [Fact]
    public async Task TokenBevatResourceLinkEnContext()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        var token = await TokenAsync(s, StubUser.Student);

        var link = token.GetPayloadValue<System.Text.Json.JsonElement>(Claim + "resource_link");
        Assert.Equal(s.Link.Id.ToString("D"), link.GetProperty("id").GetString());
        Assert.Equal("Weekly quiz", link.GetProperty("title").GetString());
        var context = token.GetPayloadValue<System.Text.Json.JsonElement>(Claim + "context");
        Assert.Equal(s.Offering.ToString("D"), context.GetProperty("id").GetString());
        Assert.Equal("BIO101-2026-P1", context.GetProperty("label").GetString());
        Assert.Equal("http://purl.imsglobal.org/vocab/lis/v2/course#CourseOffering", context.GetProperty("type")[0].GetString());
    }

    [Fact]
    public async Task StudentKrijgtDeRolLearner()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        var token = await TokenAsync(s, StubUser.Student);

        Assert.Equal(["http://purl.imsglobal.org/vocab/lis/v2/membership#Learner"], token.GetPayloadValue<string[]>(Claim + "roles"));
    }

    [Fact]
    public async Task DocentKrijgtDeRolInstructor()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        var token = await TokenAsync(s, GroupsSeed.Docent);

        Assert.Equal(["http://purl.imsglobal.org/vocab/lis/v2/membership#Instructor"], token.GetPayloadValue<string[]>(Claim + "roles"));
    }

    [Fact]
    public async Task ZonderInstellingGeenNaamEnGeenEmail()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        var token = await TokenAsync(s, StubUser.Student);

        Assert.False(token.TryGetPayloadValue<string>("name", out _));
        Assert.False(token.TryGetPayloadValue<string>("given_name", out _));
        Assert.False(token.TryGetPayloadValue<string>("family_name", out _));
        Assert.False(token.TryGetPayloadValue<string>("email", out _));
    }

    [Fact]
    public async Task NaamEnEmailGaanMeeAlsDeToolDatVraagt()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, sendName: true, sendEmail: true);

        var token = await TokenAsync(s, StubUser.Student);

        Assert.Equal("Sanne Student", token.GetClaim("name").Value);
        Assert.Equal("Sanne", token.GetClaim("given_name").Value);
        Assert.Equal("Student", token.GetClaim("family_name").Value);
        Assert.False(string.IsNullOrEmpty(token.GetClaim("email").Value));
    }
}

public sealed class GeenPersoonsgegevensInLogs(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    [Fact]
    public async Task LaunchLaatGeenPersoonsgegevensAchterInDeLogs()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = await LaunchScenario.CreateAsync(factory, ct, sendName: true, sendEmail: true);
        var logs = new CapturingLoggerProvider();
        using var logged = s.Academic.Host.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Trace" })).ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        using var browser = logged.CreateBrowser();
        await browser.SignInAndGetAsync("/profile", factory.Idp, StubUser.Student, ct);

        using var start = await browser.GetAsync(new Uri($"/lti/launch/{s.Link.Id:D}", UriKind.Relative), ct);
        var login = QueryHelpers.ParseQuery(start.Headers.Location!.Query);
        var query = QueryHelpers.AddQueryString("/lti/authorize", new Dictionary<string, string?>
        {
            ["scope"] = "openid", ["response_type"] = "id_token", ["response_mode"] = "form_post", ["prompt"] = "none",
            ["client_id"] = s.Tool.ClientId, ["redirect_uri"] = "https://tool.example.org/launch",
            ["login_hint"] = login["login_hint"], ["lti_message_hint"] = login["lti_message_hint"], ["nonce"] = "secret-nonce-123", ["state"] = "secret-state-456",
        });
        using var authorize = await browser.GetAsync(new Uri(query, UriKind.Relative), ct);
        var token = LaunchForms.Field(await authorize.Content.ReadAsStringAsync(ct), "id_token");
        Assert.False(string.IsNullOrEmpty(token));

        Assert.NotEmpty(logs.Logs); // the collector really sees the host
        var all = string.Join('\n', logs.Logs.Select(l => l.Message + l.Exception));
        foreach (var secret in new[] { "Sanne", "student@example.test", "I6000001", login["login_hint"].ToString(), login["lti_message_hint"].ToString(), "secret-nonce-123", "secret-state-456", token })
        {
            Assert.DoesNotContain(secret, all, StringComparison.Ordinal);
        }
    }
}
