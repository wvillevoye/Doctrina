using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Doctrina.Modules.Lti.Tests;

// Specs lti-service-auth of change add-lti-ags; tasks 2.2 and 2.3. Class = requirement, method = scenario.

/// <summary>A tool with grade services and its own key pair (its JWKS is served by a fake handler) and the token service under test.</summary>
internal sealed class ServiceAuthScenario : IAsyncDisposable
{
    public const string Issuer = "https://lms.example.org";
    public const string ScoreScope = "https://purl.imsglobal.org/spec/lti-ags/scope/score";
    public const string ResultScope = "https://purl.imsglobal.org/spec/lti-ags/scope/result.readonly";

    private readonly RSA _toolKey = RSA.Create(2048);
    private readonly HttpClient _http;

    private ServiceAuthScenario(LtiDatabase db, LtiTool tool)
    {
        Db = db;
        Tool = tool;
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
        DataProtection = DataProtectionProvider.Create("lti-service-auth-tests");
        _http = new HttpClient(new FakeJwksHandler(this));
        Service = new ServiceAuthService(
            new ToolService(db.CreateContext()),
            new JwksClient(_http, new MemoryCache(new MemoryCacheOptions()), Time),
            new KeyService(db.CreateContext(), DataProtection, Time, Options.Create(new LtiOptions())),
            db.CreateContext(),
            Time);
        Validator = new ServiceTokenValidator(new KeyService(db.CreateContext(), DataProtection, Time, Options.Create(new LtiOptions())), new ToolService(db.CreateContext()), Time);
    }

    public LtiDatabase Db { get; }
    public LtiTool Tool { get; }
    public FakeTimeProvider Time { get; }
    public IDataProtectionProvider DataProtection { get; }
    public ServiceAuthService Service { get; }
    public ServiceTokenValidator Validator { get; }

    /// <summary>An access token for the given scope, as the tool would get it from the token endpoint.</summary>
    public async Task<string> TokenAsync(string scope = ScoreScope) =>
        (await Service.IssueAsync(Request(scope: scope), Issuer, TestContext.Current.CancellationToken)).Value!.AccessToken;

    public static async Task<ServiceAuthScenario> CreateAsync(bool gradeServices = true)
    {
        var db = await LtiDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var input = ToolTestData.Quiz() with { GradeServices = gradeServices, PublicKeysUrl = "https://tool.example.org/jwks" };
        var tool = (await db.Service().CreateAsync(input with { PublicKeysUrl = gradeServices ? input.PublicKeysUrl : null }, TestContext.Current.CancellationToken)).Value!;
        if (!gradeServices)
        {
            // A tool that has a public keys URL (deep linking) but no grade services.
            await db.Service().UpdateAsync(tool.Id, input with { GradeServices = false, DeepLinkingUrl = "https://tool.example.org/dl" }, TestContext.Current.CancellationToken);
            tool = (await db.Service().FindAsync(tool.Id, TestContext.Current.CancellationToken))!;
        }

        return new ServiceAuthScenario(db, tool);
    }

    /// <summary>A client assertion a well-behaved tool sends; <paramref name="tweak"/> may change or remove (null) claims.</summary>
    public string Assertion(Action<Dictionary<string, object?>>? tweak = null, RSA? signer = null)
    {
        var now = Time.GetUtcNow();
        var claims = new Dictionary<string, object?>
        {
            ["iss"] = Tool.ClientId,
            ["sub"] = Tool.ClientId,
            ["aud"] = ServiceAuthService.TokenEndpoint(Issuer),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString("N"),
        };
        tweak?.Invoke(claims);
        var key = new RsaSecurityKey(signer ?? _toolKey) { KeyId = "tool-key" };
        return new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(claims.Where(c => c.Value is not null).ToDictionary(c => c.Key, c => c.Value)), new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
    }

    public TokenRequest Request(string? assertion = null, string scope = ScoreScope) =>
        new("client_credentials", ServiceAuthService.AssertionType, assertion ?? Assertion(), scope);

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

    private sealed class FakeJwksHandler(ServiceAuthScenario scenario) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.AbsoluteUri == "https://tool.example.org/jwks"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(scenario.Jwks(), Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

public sealed class EenToolHaaltEenAccessTokenOpMetEenClientAssertion
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GeldigeAssertionGeeftEenToken()
    {
        await using var s = await ServiceAuthScenario.CreateAsync();

        var result = await s.Service.IssueAsync(s.Request(), ServiceAuthScenario.Issuer, Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(ServiceAuthScenario.ScoreScope, result.Value!.Scope);
        Assert.InRange(result.Value.ExpiresIn, 1, 3600);
        var token = new JsonWebToken(result.Value.AccessToken);
        Assert.Equal(s.Tool.ClientId, token.Subject);
        Assert.Equal(ServiceAuthScenario.ScoreScope, token.GetPayloadValue<string>("scope"));
    }

    [Fact]
    public async Task OnjuisteHandtekeningWordtGeweigerd()
    {
        await using var s = await ServiceAuthScenario.CreateAsync();
        using var other = RSA.Create(2048);

        var result = await s.Service.IssueAsync(s.Request(s.Assertion(signer: other)), ServiceAuthScenario.Issuer, Ct);

        Assert.Equal("Lti.ServiceAuth.InvalidClient", result.Error!.Code);
    }

    [Fact]
    public async Task VerkeerdeAudienceWordtGeweigerd()
    {
        await using var s = await ServiceAuthScenario.CreateAsync();

        var result = await s.Service.IssueAsync(s.Request(s.Assertion(c => c["aud"] = "https://other.example.org/lti/token")), ServiceAuthScenario.Issuer, Ct);

        Assert.Equal("Lti.ServiceAuth.InvalidClient", result.Error!.Code);
    }

    [Fact]
    public async Task VerlopenAssertionWordtGeweigerd()
    {
        await using var s = await ServiceAuthScenario.CreateAsync();
        var assertion = s.Assertion();

        s.Time.Advance(TimeSpan.FromMinutes(7)); // five minutes valid plus a minute of slack, and then some
        var result = await s.Service.IssueAsync(s.Request(assertion), ServiceAuthScenario.Issuer, Ct);

        Assert.Equal("Lti.ServiceAuth.InvalidClient", result.Error!.Code);
    }

    [Fact]
    public async Task HergebruikteAssertionWordtGeweigerd()
    {
        await using var s = await ServiceAuthScenario.CreateAsync();
        var assertion = s.Assertion();

        var first = await s.Service.IssueAsync(s.Request(assertion), ServiceAuthScenario.Issuer, Ct);
        var second = await s.Service.IssueAsync(s.Request(assertion), ServiceAuthScenario.Issuer, Ct);

        Assert.True(first.IsSuccess, first.Error?.ToString());
        Assert.Equal("Lti.ServiceAuth.InvalidClient", second.Error!.Code);
    }

    [Fact]
    public async Task OnbekendeOfUitgeschakeldeTool()
    {
        await using var s = await ServiceAuthScenario.CreateAsync();
        var unknown = s.Assertion(c => { c["iss"] = "unknown-client"; c["sub"] = "unknown-client"; });
        await s.Db.Service().DeleteAsync(s.Tool.Id, Ct);

        var disabled = await s.Service.IssueAsync(s.Request(), ServiceAuthScenario.Issuer, Ct);
        var notRegistered = await s.Service.IssueAsync(s.Request(unknown), ServiceAuthScenario.Issuer, Ct);

        Assert.Equal("Lti.ServiceAuth.InvalidClient", disabled.Error!.Code);
        Assert.Equal("Lti.ServiceAuth.InvalidClient", notRegistered.Error!.Code);
    }
}

public sealed class ScopesWordenBeperktTotWatDeToolMag
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ScopeDieNietIsToegestaan()
    {
        await using var s = await ServiceAuthScenario.CreateAsync(gradeServices: false);

        var result = await s.Service.IssueAsync(s.Request(), ServiceAuthScenario.Issuer, Ct);

        Assert.Equal("Lti.ServiceAuth.InvalidScope", result.Error!.Code);
    }

    [Fact]
    public async Task DeelsToegestaneScopes()
    {
        await using var s = await ServiceAuthScenario.CreateAsync();

        var result = await s.Service.IssueAsync(s.Request(scope: $"{ServiceAuthScenario.ScoreScope} https://example.org/unknown"), ServiceAuthScenario.Issuer, Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(ServiceAuthScenario.ScoreScope, result.Value!.Scope);
    }
}

public sealed class EenAccessTokenGeldtKortEnVoorEenTool
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZonderToken()
    {
        await using var s = await ServiceAuthScenario.CreateAsync();

        var result = await s.Validator.AuthorizeAsync(null, ServiceAuthScenario.Issuer, [ServiceAuthScenario.ScoreScope], Ct);

        Assert.Equal("Lti.ServiceAuth.Unauthorized", result.Error!.Code);
    }

    [Fact]
    public async Task GeldigTokenGeeftDeTool()
    {
        await using var s = await ServiceAuthScenario.CreateAsync();

        var result = await s.Validator.AuthorizeAsync("Bearer " + await s.TokenAsync(), ServiceAuthScenario.Issuer, [ServiceAuthScenario.ScoreScope], Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(s.Tool.Id, result.Value!.Tool.Id);
    }

    [Fact]
    public async Task VerlopenToken()
    {
        await using var s = await ServiceAuthScenario.CreateAsync();
        var token = await s.TokenAsync();

        s.Time.Advance(ServiceAuthService.TokenLifetime + TimeSpan.FromSeconds(1));
        var result = await s.Validator.AuthorizeAsync("Bearer " + token, ServiceAuthScenario.Issuer, [ServiceAuthScenario.ScoreScope], Ct);

        Assert.Equal("Lti.ServiceAuth.Unauthorized", result.Error!.Code);
    }

    [Fact]
    public async Task VervalstToken()
    {
        await using var s = await ServiceAuthScenario.CreateAsync();
        using var forger = RSA.Create(2048);
        var forged = new JsonWebTokenHandler().CreateToken(
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["iss"] = ServiceAuthScenario.Issuer, ["aud"] = ServiceAuthScenario.Issuer, ["sub"] = s.Tool.ClientId,
                ["exp"] = s.Time.GetUtcNow().AddHours(1).ToUnixTimeSeconds(), ["scope"] = ServiceAuthScenario.ScoreScope,
            }),
            new SigningCredentials(new RsaSecurityKey(forger), SecurityAlgorithms.RsaSha256));

        var result = await s.Validator.AuthorizeAsync("Bearer " + forged, ServiceAuthScenario.Issuer, [ServiceAuthScenario.ScoreScope], Ct);

        Assert.Equal("Lti.ServiceAuth.Unauthorized", result.Error!.Code);
    }

    [Fact]
    public async Task OnvoldoendeScope()
    {
        await using var s = await ServiceAuthScenario.CreateAsync();
        var token = await s.TokenAsync(ServiceAuthScenario.ResultScope);

        var result = await s.Validator.AuthorizeAsync("Bearer " + token, ServiceAuthScenario.Issuer, [ServiceAuthScenario.ScoreScope], Ct);

        Assert.Equal("Lti.ServiceAuth.Forbidden", result.Error!.Code);
    }
}
