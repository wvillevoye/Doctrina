using System.Net;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.Host.IntegrationTests.HostUi;
using Doctrina.TestKit.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Doctrina.Modules.Lti.Application;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// Spec lti-deep-linking of change add-lti-deep-linking; task 4.2. The service checks are tested in the Lti tests
// (HetAntwoordVanDeToolControleren); here the endpoint: anonymous POST, redirect, error. Class = requirement, method = scenario.
namespace Doctrina.Host.IntegrationTests.Academic;

/// <summary>A deep linking tool with its own key pair; its JWKS is served to the host by a fake handler instead of the internet.</summary>
internal sealed class DeepLinkingTool : IDisposable
{
    private const string Lti = "https://purl.imsglobal.org/spec/lti/claim/";
    private const string DeepLinking = "https://purl.imsglobal.org/spec/lti-dl/claim/";

    private readonly RSA _key = RSA.Create(2048);

    public DeepLinkingTool(LaunchScenario scenario, Microsoft.Extensions.Logging.ILoggerProvider? logs = null)
    {
        Scenario = scenario;
        Host = scenario.Academic.Host.WithWebHostBuilder(b =>
        {
            if (logs is not null)
            {
                b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Trace" }));
                b.ConfigureServices(services => services.AddSingleton(logs));
            }

            // PostConfigure: the modules register their services after the ones of the test host, so only this runs last.
            b.ConfigureServices(services =>
                services.PostConfigure<HttpClientFactoryOptions>(nameof(JwksClient), o => o.HttpMessageHandlerBuilderActions.Add(h => h.PrimaryHandler = new JwksHandler(Jwks()))));
        });
    }

    /// <summary>What the last <see cref="SelectAsync"/> sent and received, to look for it in the logs.</summary>
    public List<string> Exchanged { get; } = [];

    public LaunchScenario Scenario { get; }

    /// <summary>The host that fetches the JWKS of this tool from the fake handler.</summary>
    public WebApplicationFactory<Program> Host { get; }

    /// <summary>A response a well-behaved tool sends, for a request that was started for the section of the scenario.</summary>
    public string Response(bool withItems = true, RSA? signer = null, string? data = null)
    {
        var now = DateTimeOffset.UtcNow;
        data ??= new DeepLinkData(Host.Services.GetRequiredService<IDataProtectionProvider>(), TimeProvider.System).Protect(Scenario.Section.Id, Scenario.Tool.Id, "subject-1");
        var claims = new Dictionary<string, object>
        {
            ["iss"] = Scenario.Tool.ClientId,
            ["aud"] = "https://localhost",
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
            ["nonce"] = Guid.NewGuid().ToString("N"),
            [Lti + "message_type"] = "LtiDeepLinkingResponse",
            [Lti + "version"] = "1.3.0",
            [Lti + "deployment_id"] = Scenario.Tool.DeploymentId,
            [DeepLinking + "data"] = data,
        };
        if (withItems)
        {
            claims[DeepLinking + "content_items"] = new[]
            {
                new { type = "ltiResourceLink", title = "Week 1 quiz", url = "https://tool.example.org/launch?quiz=1" },
                new { type = "ltiResourceLink", title = "Week 2 quiz", url = "https://tool.example.org/launch?quiz=2" },
            };
        }

        return new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(claims), new SigningCredentials(new RsaSecurityKey(signer ?? _key) { KeyId = "tool-key" }, SecurityAlgorithms.RsaSha256));
    }

    /// <summary>
    /// The whole flow of a teacher: starts the request, answers the authentication request, lets the tool choose two items and
    /// follows the redirect of the tool's response. Returns the teacher's browser and the address of the confirmation page.
    /// </summary>
    public async Task<(HttpClient Teacher, string SelectionUrl)> SelectAsync(DoctrinaHostFactory factory, Doctrina.TestKit.Authentication.StubUser teacher, CancellationToken ct, string nonce = "nonce-1", string state = "state-1")
    {
        var browser = Host.CreateBrowser();
        await browser.SignInAndGetAsync("/profile", factory.Idp, teacher, ct);
        using var start = await browser.GetAsync(new Uri($"/lti/deep-linking/{Scenario.Section.Id:D}/{Scenario.Tool.Id:D}", UriKind.Relative), ct);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var login = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(start.Headers.Location!.Query);
        var query = new Dictionary<string, string?>
        {
            ["scope"] = "openid", ["response_type"] = "id_token", ["response_mode"] = "form_post", ["prompt"] = "none",
            ["client_id"] = Scenario.Tool.ClientId, ["redirect_uri"] = "https://tool.example.org/launch",
            ["login_hint"] = login["login_hint"], ["lti_message_hint"] = login["lti_message_hint"], ["nonce"] = nonce, ["state"] = state,
        };
        var form = await browser.GetStringAsync(new Uri(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/lti/authorize", query), UriKind.Relative), ct);
        var request = new JsonWebToken(LaunchForms.Field(form, "id_token"));
        var data = request.GetPayloadValue<JsonElement>(DeepLinking + "deep_linking_settings").GetProperty("data").GetString();
        var jwt = Response(data: data);
        Exchanged.AddRange([login["login_hint"].ToString(), login["lti_message_hint"].ToString(), LaunchForms.Field(form, "id_token"), data!, jwt]);
        using var response = await PostAsync(jwt, ct);
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        return (browser, response.Headers.Location!.OriginalString);
    }

    /// <summary>Posts the response like the browser of a teacher would, without a session and without following the redirect.</summary>
    public async Task<HttpResponseMessage> PostAsync(string jwt, CancellationToken ct)
    {
        using var browser = Host.CreateBrowser();
        return await browser.PostAsync(new Uri("/lti/deep-linking/return", UriKind.Relative), new FormUrlEncodedContent(new Dictionary<string, string> { ["JWT"] = jwt }), ct);
    }

    public void Dispose()
    {
        Host.Dispose();
        _key.Dispose();
    }

    private string Jwks()
    {
        var p = _key.ExportParameters(false);
        return JsonSerializer.Serialize(new { keys = new[] { new { kty = "RSA", use = "sig", alg = "RS256", kid = "tool-key", n = Base64UrlEncoder.Encode(p.Modulus), e = Base64UrlEncoder.Encode(p.Exponent) } } });
    }

    private sealed class JwksHandler(string jwks) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(jwks, Encoding.UTF8, "application/json") });
    }
}

public sealed class HetAntwoordVanDeToolOpHetWeb(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GeldigAntwoordWordtBewaardAlsSelectie()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, deepLinking: true);
        using var tool = new DeepLinkingTool(s);

        using var response = await tool.PostAsync(tool.Response(), Ct);

        Assert.True(response.StatusCode == HttpStatusCode.SeeOther, await response.Content.ReadAsStringAsync(Ct));
        Assert.Matches("^/lti/deep-linking/selection/[0-9a-f-]{36}$", response.Headers.Location!.OriginalString);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task OnjuisteHandtekeningWordtGeweigerd()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, deepLinking: true);
        using var tool = new DeepLinkingTool(s);
        using var other = RSA.Create(2048);

        using var response = await tool.PostAsync(tool.Response(signer: other), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Lti.DeepLinking.InvalidToken", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task AntwoordZonderItemsGeeftEenLegeSelectie()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, deepLinking: true);
        using var tool = new DeepLinkingTool(s);

        using var response = await tool.PostAsync(tool.Response(withItems: false), Ct);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal($"/lti/deep-linking/back/{s.Section.Id:D}", response.Headers.Location!.OriginalString);
    }
}

public sealed class DeDocentBevestigtDeSelectieOpHetWeb(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeBevestigingspaginaToontDeTitels()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, deepLinking: true);
        using var tool = new DeepLinkingTool(s);
        var (teacher, url) = await tool.SelectAsync(factory, GroupsSeed.Docent, Ct);
        using var _ = teacher;

        var (status, html) = await AcademicHost.GetAsync(teacher, url, Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var titles = Html.Parse(html).QuerySelectorAll("[data-testid=selection-item]").Select(e => e.TextContent.Trim());
        Assert.Equal(["Week 1 quiz", "Week 2 quiz"], titles);
        using var scope = s.Academic.Host.Services.CreateScope();
        var section = Assert.Single(await scope.ServiceProvider.GetRequiredService<Doctrina.Modules.Content.Application.ContentService>().SectionsAsync(s.Offering, true, Ct));
        Assert.Equal(["Weekly quiz"], section.Items.Select(i => i.Title)); // nothing added before the teacher confirms
    }

    [Fact]
    public async Task DocentVoegtDeSelectieToeEnStudentenZienNiets()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, deepLinking: true);
        using var tool = new DeepLinkingTool(s);
        var (teacher, url) = await tool.SelectAsync(factory, GroupsSeed.Docent, Ct);
        teacher.Dispose();
        var admin = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(Doctrina.Infrastructure.Authentication.DoctrinaClaimTypes.Permission, "Content.Manage")], "test"));

        using (var scope = tool.Host.Services.CreateScope()) // what the confirmation page does when the teacher clicks "Add to section"
        {
            var deepLinking = scope.ServiceProvider.GetRequiredService<DeepLinkingService>();
            var found = await deepLinking.FindSelectionAsync(Guid.Parse(url[(url.LastIndexOf('/') + 1)..]), Ct);
            var links = found.Value.Items.Select(i => new Doctrina.Modules.Content.Contracts.NewToolLink(i.Title, i.Url)).ToList();
            var added = await scope.ServiceProvider.GetRequiredService<Doctrina.Modules.Content.Contracts.IContentDirectory>()
                .AddToolLinksAsync(admin, found.Value.Selection.SectionId, found.Value.Selection.ToolId, links, Ct);
            Assert.True(added.IsSuccess, added.Error?.ToString());
            await deepLinking.DiscardSelectionAsync(found.Value.Selection.Id, Ct);
        }

        using var content = tool.Host.Services.CreateScope();
        var section = Assert.Single(await content.ServiceProvider.GetRequiredService<Doctrina.Modules.Content.Application.ContentService>().SectionsAsync(s.Offering, true, Ct));
        Assert.Equal(["Weekly quiz", "Week 1 quiz", "Week 2 quiz"], section.Items.Select(i => i.Title));
        Assert.All(section.Items.Skip(1), i => Assert.False(i.Published));
        var (_, student) = await s.Academic.GetAsync(factory, StubUser.Student, $"/courses/{s.Offering}", Ct);
        Assert.DoesNotContain("Week 1 quiz", student, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AndereGebruikerMagDeSelectieNietBevestigen()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, deepLinking: true);
        using var tool = new DeepLinkingTool(s);
        var (teacher, url) = await tool.SelectAsync(factory, GroupsSeed.Docent, Ct);
        teacher.Dispose();
        using var student = tool.Host.CreateBrowser();
        await student.SignInAndGetAsync("/profile", factory.Idp, StubUser.Student, Ct);

        var (status, html) = await AcademicHost.GetAsync(student, url, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains("Access denied", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Week 1 quiz", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerlopenSelectieGeeft404()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, deepLinking: true);
        using var tool = new DeepLinkingTool(s);
        var (teacher, url) = await tool.SelectAsync(factory, GroupsSeed.Docent, Ct);
        using var _ = teacher;
        using (var scope = tool.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Doctrina.Modules.Lti.Infrastructure.LtiDbContext>();
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync(
                db.Selections, u => u.SetProperty(x => x.Expires, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct);
        }

        var (status, _) = await AcademicHost.GetAsync(teacher, url, Ct);

        Assert.Equal(HttpStatusCode.NotFound, status);
    }
}

// Spec lti-deep-linking of change add-lti-deep-linking; task 7.1.
public sealed class GeenPersoonsgegevensInLogsBijDeepLinking(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    [Fact]
    public async Task DeepLinkingLaatGeenPersoonsgegevensAchterInDeLogs()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = await LaunchScenario.CreateAsync(factory, ct, sendName: true, sendEmail: true, deepLinking: true);
        var logs = new Doctrina.TestKit.Modules.CapturingLoggerProvider();
        using var tool = new DeepLinkingTool(s, logs);

        var (teacher, url) = await tool.SelectAsync(factory, GroupsSeed.Docent, ct, nonce: "secret-nonce-123", state: "secret-state-456");
        using var _ = teacher;
        await AcademicHost.GetAsync(teacher, url, ct); // the confirmation page

        Assert.NotEmpty(logs.Logs); // the collector really sees the host
        var all = string.Join(Environment.NewLine, logs.Logs.Select(l => l.Message + l.Exception));
        foreach (var secret in (string[])["Dirk", "docent@example.test", "P7000002", "secret-nonce-123", "secret-state-456", "Week 1 quiz", "launch?quiz=1", .. tool.Exchanged])
        {
            Assert.DoesNotContain(secret, all, StringComparison.Ordinal);
        }
    }
}
