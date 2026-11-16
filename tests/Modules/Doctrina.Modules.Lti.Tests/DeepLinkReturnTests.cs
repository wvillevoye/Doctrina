using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Doctrina.Modules.Lti.Tests;

// Spec lti-deep-linking of change add-lti-deep-linking; task 4.1. Class = requirement, method = scenario.

/// <summary>A tool with deep linking and its own key pair, served as JWKS by a fake HTTP handler, and the service under test.</summary>
internal sealed class DeepLinkScenario : IAsyncDisposable
{
    public const string Issuer = "https://lms.example.org";
    private const string Lti = "https://purl.imsglobal.org/spec/lti/claim/";
    private const string DeepLinking = "https://purl.imsglobal.org/spec/lti-dl/claim/";

    private readonly RSA _toolKey = RSA.Create(2048);
    private readonly HttpClient _http;

    private DeepLinkScenario(LtiDatabase db, LtiTool tool)
    {
        Db = db;
        Tool = tool;
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        DataProtection = DataProtectionProvider.Create("lti-deep-link-tests");
        _http = new HttpClient(new FakeJwksHandler(this));
        Service = new DeepLinkingService(db.CreateContext(), new ToolService(db.CreateContext()), new JwksClient(_http, new MemoryCache(new MemoryCacheOptions()), Time), DataProtection, Time);
    }

    public LtiDatabase Db { get; }
    public LtiTool Tool { get; }
    public FakeTimeProvider Time { get; }
    public IDataProtectionProvider DataProtection { get; }
    public DeepLinkingService Service { get; }
    public Guid SectionId { get; } = Guid.NewGuid();
    public string Subject { get; } = "subject-1";
    public int JwksFetches { get; private set; }

    public static async Task<DeepLinkScenario> CreateAsync()
    {
        var db = await LtiDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var input = ToolTestData.Quiz() with { DeepLinkingUrl = "https://tool.example.org/deep-linking", PublicKeysUrl = "https://tool.example.org/jwks" };
        var tool = (await db.Service().CreateAsync(input, TestContext.Current.CancellationToken)).Value!;
        return new DeepLinkScenario(db, tool);
    }

    public static IReadOnlyList<object> Items(params (string Type, string Title, string Url)[] items) =>
        [.. items.Select(i => new { type = i.Type, title = i.Title, url = i.Url })];

    public static IReadOnlyList<object> TwoQuizzes() =>
        Items(("ltiResourceLink", "Week 1 quiz", "https://tool.example.org/launch?quiz=1"), ("ltiResourceLink", "Week 2 quiz", "https://tool.example.org/launch?quiz=2"));

    /// <summary>A response a well-behaved tool sends; <paramref name="tweak"/> may change or remove (null) claims.</summary>
    public string Response(IReadOnlyList<object>? items = null, Action<Dictionary<string, object?>>? tweak = null, RSA? signer = null, string? data = null)
    {
        var now = Time.GetUtcNow();
        var claims = new Dictionary<string, object?>
        {
            ["iss"] = Tool.ClientId,
            ["aud"] = Issuer,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
            ["nonce"] = Guid.NewGuid().ToString("N"),
            [Lti + "message_type"] = "LtiDeepLinkingResponse",
            [Lti + "version"] = "1.3.0",
            [Lti + "deployment_id"] = Tool.DeploymentId,
            [DeepLinking + "data"] = data ?? new DeepLinkData(DataProtection, Time).Protect(SectionId, Tool.Id, Subject),
            [DeepLinking + "content_items"] = items ?? TwoQuizzes(),
        };
        tweak?.Invoke(claims);
        var key = new RsaSecurityKey(signer ?? _toolKey) { KeyId = "tool-key" };
        return new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(claims.Where(c => c.Value is not null).ToDictionary(c => c.Key, c => c.Value)), new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
    }

    public async Task<int> SelectionCountAsync() => await Db.CreateContext().Selections.CountAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        _toolKey.Dispose();
        await Db.DisposeAsync();
    }

    private string Jwks()
    {
        var p = _toolKey.ExportParameters(false);
        return JsonSerializer.Serialize(new
        {
            keys = new[] { new { kty = "RSA", use = "sig", alg = "RS256", kid = "tool-key", n = Base64UrlEncoder.Encode(p.Modulus), e = Base64UrlEncoder.Encode(p.Exponent) } },
        });
    }

    private sealed class FakeJwksHandler(DeepLinkScenario scenario) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            scenario.JwksFetches++;
            return Task.FromResult(request.RequestUri!.AbsoluteUri == "https://tool.example.org/jwks"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(scenario.Jwks(), Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}

public sealed class HetAntwoordVanDeToolControleren
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GeldigAntwoordWordtBewaardAlsSelectie()
    {
        await using var s = await DeepLinkScenario.CreateAsync();

        var result = await s.Service.ReturnAsync(s.Response(), DeepLinkScenario.Issuer, Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(s.SectionId, result.Value!.SectionId);
        var found = await s.Service.FindSelectionAsync(result.Value.SelectionId!.Value, Ct);
        Assert.Equal(["Week 1 quiz", "Week 2 quiz"], found.Value.Items.Select(i => i.Title));
        Assert.Equal(["https://tool.example.org/launch?quiz=1", "https://tool.example.org/launch?quiz=2"], found.Value.Items.Select(i => i.Url));
        Assert.Equal((s.Tool.Id, s.SectionId, s.Subject), (found.Value.Selection.ToolId, found.Value.Selection.SectionId, found.Value.Selection.Subject));
    }

    [Fact]
    public async Task OnjuisteHandtekeningWordtGeweigerd()
    {
        await using var s = await DeepLinkScenario.CreateAsync();
        using var other = RSA.Create(2048);

        var result = await s.Service.ReturnAsync(s.Response(signer: other), DeepLinkScenario.Issuer, Ct);

        Assert.Equal("Lti.DeepLinking.InvalidToken", result.Error!.Code);
        Assert.Equal(0, await s.SelectionCountAsync());
    }

    [Theory]
    [InlineData("iss")]
    [InlineData("aud")]
    public async Task VerkeerdeIssuerOfAudienceWordtGeweigerd(string claim)
    {
        await using var s = await DeepLinkScenario.CreateAsync();

        var result = await s.Service.ReturnAsync(s.Response(tweak: c => c[claim] = "https://other.example.org"), DeepLinkScenario.Issuer, Ct);

        Assert.Equal("Lti.DeepLinking.InvalidToken", result.Error!.Code);
        Assert.Equal(0, await s.SelectionCountAsync());
    }

    [Fact]
    public async Task VerlopenAntwoordWordtGeweigerd()
    {
        await using var s = await DeepLinkScenario.CreateAsync();
        var expired = s.Time.GetUtcNow().AddHours(-1).ToUnixTimeSeconds();

        var result = await s.Service.ReturnAsync(s.Response(tweak: c => c["exp"] = expired), DeepLinkScenario.Issuer, Ct);

        Assert.Equal("Lti.DeepLinking.InvalidToken", result.Error!.Code);
    }

    [Fact]
    public async Task HergebruiktAntwoordWordtGeweigerd()
    {
        await using var s = await DeepLinkScenario.CreateAsync();
        var response = s.Response();
        Assert.True((await s.Service.ReturnAsync(response, DeepLinkScenario.Issuer, Ct)).IsSuccess);

        var again = await s.Service.ReturnAsync(response, DeepLinkScenario.Issuer, Ct);

        Assert.Equal("Lti.DeepLinking.Replayed", again.Error!.Code);
        Assert.Equal(1, await s.SelectionCountAsync());
    }

    [Fact]
    public async Task VervalsteOfVerlopenDataWordtGeweigerd()
    {
        await using var s = await DeepLinkScenario.CreateAsync();
        var oldData = new DeepLinkData(s.DataProtection, s.Time).Protect(s.SectionId, s.Tool.Id, s.Subject);

        var forged = await s.Service.ReturnAsync(s.Response(data: "forged"), DeepLinkScenario.Issuer, Ct);
        s.Time.Advance(DeepLinkData.Lifetime + TimeSpan.FromMinutes(1)); // the response itself is still valid for a while: made after the advance
        var expired = await s.Service.ReturnAsync(s.Response(data: oldData), DeepLinkScenario.Issuer, Ct);

        Assert.Equal("Lti.DeepLinking.InvalidData", forged.Error!.Code);
        Assert.Equal("Lti.DeepLinking.InvalidData", expired.Error!.Code);
        Assert.Equal(0, await s.SelectionCountAsync());
    }

    [Fact]
    public async Task DataVanEenAndereToolWordtGeweigerd()
    {
        await using var s = await DeepLinkScenario.CreateAsync();
        var otherTool = new DeepLinkData(s.DataProtection, s.Time).Protect(s.SectionId, Guid.NewGuid(), s.Subject);

        var result = await s.Service.ReturnAsync(s.Response(data: otherTool), DeepLinkScenario.Issuer, Ct);

        Assert.Equal("Lti.DeepLinking.InvalidData", result.Error!.Code);
    }

    [Theory]
    [InlineData("https://evil.example.org/launch")]
    [InlineData("https://tool.example.org:8443/launch")]
    [InlineData("http://tool.example.org/launch")]
    public async Task ItemMetEenAnderAdresDanDeToolWordtGeweigerd(string url)
    {
        await using var s = await DeepLinkScenario.CreateAsync();
        var items = DeepLinkScenario.Items(("ltiResourceLink", "Week 1 quiz", "https://tool.example.org/launch?quiz=1"), ("ltiResourceLink", "Other", url));

        var result = await s.Service.ReturnAsync(s.Response(items), DeepLinkScenario.Issuer, Ct);

        Assert.Equal("Lti.DeepLinking.InvalidItem", result.Error!.Code);
        Assert.Equal(0, await s.SelectionCountAsync());
    }

    [Fact]
    public async Task AndereItemsoortenWordenGeweigerd()
    {
        await using var s = await DeepLinkScenario.CreateAsync();

        var result = await s.Service.ReturnAsync(s.Response(DeepLinkScenario.Items(("html", "Snippet", "https://tool.example.org/launch"))), DeepLinkScenario.Issuer, Ct);

        Assert.Equal("Lti.DeepLinking.InvalidItem", result.Error!.Code);
    }

    [Fact]
    public async Task AntwoordZonderItemsGeeftEenLegeSelectie()
    {
        await using var s = await DeepLinkScenario.CreateAsync();

        var result = await s.Service.ReturnAsync(s.Response(tweak: c => c.Remove(DeepLinkingClaim + "content_items")), DeepLinkScenario.Issuer, Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Null(result.Value!.SelectionId);
        Assert.Equal(s.SectionId, result.Value.SectionId);
        Assert.Equal(0, await s.SelectionCountAsync());
    }

    [Fact]
    public async Task DeSleutelsVanDeToolWordenInHetGeheugenBewaard()
    {
        await using var s = await DeepLinkScenario.CreateAsync();

        await s.Service.ReturnAsync(s.Response(), DeepLinkScenario.Issuer, Ct);
        await s.Service.ReturnAsync(s.Response(), DeepLinkScenario.Issuer, Ct);

        Assert.Equal(1, s.JwksFetches);
    }

    private const string DeepLinkingClaim = "https://purl.imsglobal.org/spec/lti-dl/claim/";
}
