using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.Host.IntegrationTests.HostUi;
using Doctrina.TestKit.Authentication;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Lti.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// Specs lti-grade-services of change add-lti-ags; tasks 4.2-4.4. The rules are tested in the Lti tests (GradeServiceTests);
// here the HTTP layer: statuses, media types and the scope per endpoint. Class = requirement, method = scenario.
namespace Doctrina.Host.IntegrationTests.Academic;

public sealed class LineItemsVanEenToolBerenOpHetWeb(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private const string Scopes = "https://purl.imsglobal.org/spec/lti-ags/scope/";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>An access token signed with the key of the host, as the token endpoint would issue it.</summary>
    private static async Task<string> TokenAsync(LaunchScenario s, params string[] scopes)
    {
        using var scope = s.Academic.Host.Services.CreateScope();
        var key = await scope.ServiceProvider.GetRequiredService<KeyService>().ActiveAsync(Ct);
        var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["iss"] = "https://localhost", ["sub"] = s.Tool.ClientId, ["aud"] = "https://localhost",
            ["iat"] = now.ToUnixTimeSeconds(), ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString("N"), ["scope"] = string.Join(' ', scopes.Select(x => Scopes + x)),
        });
        return new JsonWebTokenHandler().CreateToken(payload, new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
    }

    private static HttpClient Client(LaunchScenario s, string? token)
    {
        var client = s.Academic.Host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private static string Url(LaunchScenario s, string path = "") => $"/lti/ags/{s.Offering:D}/lineitems{path}";

    [Fact]
    public async Task AlleenLezen()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        using var client = Client(s, await TokenAsync(s, "lineitem.readonly"));

        using var create = await client.PostAsJsonAsync(Url(s), new { label = "Quiz", scoreMaximum = 10 }, Ct);
        using var list = await client.GetAsync(Url(s), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    [Fact]
    public async Task ZonderTokenIs401()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        using var client = Client(s, null);

        using var response = await client.GetAsync(Url(s), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task VolledigeStroomMetStatussenEnMediatypes()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        using var client = Client(s, await TokenAsync(s, "lineitem", "score", "result.readonly"));
        using var scope = s.Academic.Host.Services.CreateScope();
        var member = (await scope.ServiceProvider.GetRequiredService<IAcademicDirectory>().MembersAsync(s.Offering, Ct))[0];

        using var created = await client.PostAsJsonAsync(Url(s), new { label = "Quiz", scoreMaximum = 10, resourceLinkId = s.Link.Id.ToString("D") }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("application/vnd.ims.lis.v2.lineitem+json", created.Content.Headers.ContentType?.MediaType);
        var id = new Uri((await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString()!).AbsolutePath;

        using var score = await client.PostAsJsonAsync($"{id}/scores",
            new { userId = member.PersonId.ToString("N"), scoreGiven = 8, scoreMaximum = 10, activityProgress = "Completed", gradingProgress = "FullyGraded", timestamp = DateTimeOffset.UtcNow }, Ct);
        using var outdated = await client.PostAsJsonAsync($"{id}/scores",
            new { userId = member.PersonId.ToString("N"), scoreGiven = 9, scoreMaximum = 10, activityProgress = "Completed", gradingProgress = "FullyGraded", timestamp = DateTimeOffset.UtcNow.AddDays(-1) }, Ct);
        using var invalid = await client.PostAsJsonAsync($"{id}/scores", new { userId = member.PersonId.ToString("N"), scoreGiven = -1, scoreMaximum = 10 }, Ct);
        using var results = await client.GetAsync($"{id}/results", Ct);

        Assert.Equal(HttpStatusCode.NoContent, score.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, outdated.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, results.StatusCode);
        Assert.Equal("application/vnd.ims.lis.v2.resultcontainer+json", results.Content.Headers.ContentType?.MediaType);
        var row = Assert.Single((await results.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray());
        Assert.Equal((member.PersonId.ToString("N"), 8m), (row.GetProperty("userId").GetString(), row.GetProperty("resultScore").GetDecimal()));

        using var deleted = await client.DeleteAsync(id, Ct);
        using var gone = await client.GetAsync(id, Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }
}

public sealed class DeIdTokenVermeldtDeCijferservices(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private const string Endpoint = "https://purl.imsglobal.org/spec/lti-ags/claim/endpoint";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<JsonWebToken> TokenAsync(LaunchScenario s)
    {
        using var session = await (await ToolSession.SignInAsync(s, factory, StubUser.Student, Ct)).StartAsync(Ct);
        using var response = await session.AuthorizeAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = LaunchForms.Field(await response.Content.ReadAsStringAsync(Ct), "id_token");
        return await LaunchForms.VerifiedAsync(s.Academic, token, s.Tool.ClientId, Ct);
    }

    [Fact]
    public async Task LaunchVanEenToolMetCijferservices()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, gradeServices: true);

        var claim = (await TokenAsync(s)).GetPayloadValue<JsonElement>(Endpoint);

        Assert.Equal(4, claim.GetProperty("scope").GetArrayLength());
        Assert.Equal($"https://localhost/lti/ags/{s.Offering:D}/lineitems", claim.GetProperty("lineitems").GetString());
        Assert.False(claim.TryGetProperty("lineitem", out _));
    }

    [Fact]
    public async Task LaunchMetEenBestaandLineItem()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, gradeServices: true);
        using var scope = s.Academic.Host.Services.CreateScope();
        var created = await scope.ServiceProvider.GetRequiredService<GradeService>()
            .CreateLineItemAsync(s.Tool, s.Offering, new LineItemInput("Quiz", 10, null, null, s.Link.Id.ToString("D"), null, null), Ct);
        Assert.True(created.IsSuccess, created.Error?.ToString());

        var claim = (await TokenAsync(s)).GetPayloadValue<JsonElement>(Endpoint);

        Assert.Equal($"https://localhost/lti/ags/{s.Offering:D}/lineitems/{created.Value!.Id:D}", claim.GetProperty("lineitem").GetString());
    }

    [Fact]
    public async Task ToolZonderCijferservicesKrijgtGeenAgsClaim()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        var token = await TokenAsync(s);

        Assert.False(token.TryGetPayloadValue<JsonElement>(Endpoint, out _));
    }
}

public sealed class ScoresOpDePaginasVanDeOffering(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A line item on the tool link of the scenario with a score of 8 / 10 for the student of the stub provider.</summary>
    private static async Task SeedScoreAsync(LaunchScenario s)
    {
        using var scope = s.Academic.Host.Services.CreateScope();
        var grades = scope.ServiceProvider.GetRequiredService<GradeService>();
        var item = (await grades.CreateLineItemAsync(s.Tool, s.Offering, new LineItemInput("Quiz 1", 10, null, null, s.Link.Id.ToString("D"), null, null), Ct)).Value!;
        var student = (await scope.ServiceProvider.GetRequiredService<IAcademicDirectory>().MembersAsync(s.Offering, Ct)).First(m => m.ExternalId == StubUser.Student.PersonId);
        var posted = await grades.PostScoreAsync(s.Tool, s.Offering, item.Id, new ScoreInput(student.PersonId.ToString("N"), 8, 10, null, "Completed", "FullyGraded", DateTimeOffset.UtcNow), Ct);
        Assert.True(posted.IsSuccess, posted.Error?.ToString());
    }

    [Fact]
    public async Task StudentZietDeEigenScore()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, gradeServices: true);
        await SeedScoreAsync(s);

        var (status, html) = await s.Academic.GetAsync(factory, StubUser.Student, $"/courses/{s.Offering:D}", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Quiz 1: 8 / 10", System.Net.WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocentZietHetOverzicht()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, gradeServices: true);
        await SeedScoreAsync(s);

        var (status, html) = await s.Academic.GetAsync(factory, GroupsSeed.Docent, $"/lti/ags/results/{s.Link.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("8 / 10", System.Net.WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocentVindtDeKnopScoresBijDeToolLink()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, gradeServices: true);

        var (status, html) = await s.Academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{s.Offering:D}/content", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains($"/lti/ags/results/{s.Link.Id:D}", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StudentMagHetOverzichtNietOpenen()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, gradeServices: true);
        await SeedScoreAsync(s);

        var (status, html) = await s.Academic.GetAsync(factory, StubUser.Student, $"/lti/ags/results/{s.Link.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.DoesNotContain("8 / 10", html, StringComparison.Ordinal);
    }
}

/// <summary>A full AGS flow on a host that captures every log line: token, line item, score, results, and the teacher's overview.</summary>
internal static class AgsFlow
{
    public const string Comment = "secret-comment-789";

    public static async Task<(string Logs, string[] Secrets)> RunAsync(DoctrinaHostFactory factory, CancellationToken ct)
    {
        await using var s = await LaunchScenario.CreateAsync(factory, ct, sendName: true, sendEmail: true, gradeServices: true);
        var capture = new Doctrina.TestKit.Modules.CapturingLoggerProvider();
        using var tool = new DeepLinkingTool(s, capture);
        var assertion = tool.ClientAssertion();
        using var client = tool.Host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var tokenResponse = await client.PostAsync("/lti/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_assertion_type"] = ServiceAuthService.AssertionType,
            ["client_assertion"] = assertion,
            ["scope"] = "https://purl.imsglobal.org/spec/lti-ags/scope/lineitem https://purl.imsglobal.org/spec/lti-ags/scope/score https://purl.imsglobal.org/spec/lti-ags/scope/result.readonly",
        }), ct);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var accessToken = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("access_token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var scope = tool.Host.Services.CreateScope();
        var student = (await scope.ServiceProvider.GetRequiredService<IAcademicDirectory>().MembersAsync(s.Offering, ct)).First(m => m.ExternalId == StubUser.Student.PersonId);
        var lineItems = $"/lti/ags/{s.Offering:D}/lineitems";
        using var created = await client.PostAsJsonAsync(lineItems, new { label = "Quiz 1", scoreMaximum = 10, resourceLinkId = s.Link.Id.ToString("D") }, ct);
        var id = new Uri((await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetString()!).AbsolutePath;
        using var score = await client.PostAsJsonAsync($"{id}/scores",
            new { userId = student.PersonId.ToString("N"), scoreGiven = 8, scoreMaximum = 10, comment = Comment, activityProgress = "Completed", gradingProgress = "FullyGraded", timestamp = DateTimeOffset.UtcNow }, ct);
        Assert.Equal(HttpStatusCode.NoContent, score.StatusCode);
        using var results = await client.GetAsync($"{id}/results", ct);
        Assert.Equal(HttpStatusCode.OK, results.StatusCode);

        using var teacher = tool.Host.CreateBrowser();
        await teacher.SignInAndGetAsync("/profile", factory.Idp, GroupsSeed.Docent, ct);
        var (_, overview) = await AcademicHost.GetAsync(teacher, $"/lti/ags/results/{s.Link.Id:D}", ct);
        Assert.Contains("8 / 10", System.Net.WebUtility.HtmlDecode(overview), StringComparison.Ordinal);

        Assert.NotEmpty(capture.Logs); // the collector really sees the host
        var logs = string.Join(Environment.NewLine, capture.Logs.Select(l => l.Message + l.Exception));
        var jwt = new JsonWebToken(accessToken);
        return (logs, [assertion, new JsonWebToken(assertion).Id, accessToken, jwt.Id, student.PersonId.ToString("N"), student.PersonId.ToString("D"),
            student.Name, StubUser.Student.Name, StubUser.Student.Email!, StubUser.Student.PersonId!, Comment]);
    }
}

// Spec lti-service-auth of change add-lti-ags; task 5.3.
public sealed class GeenGeheimenOfPersoonsgegevensInLogsBijServiceAuth(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    [Fact]
    public async Task TokenOphalenEnGebruikenLaatGeenGeheimenAchterInDeLogs()
    {
        var (logs, secrets) = await AgsFlow.RunAsync(factory, TestContext.Current.CancellationToken);

        foreach (var secret in secrets.Take(4))
        {
            Assert.DoesNotContain(secret, logs, StringComparison.Ordinal); // assertion, its jti, access token and its jti
        }
    }
}

// Spec lti-grade-services of change add-lti-ags; task 5.3.
public sealed class GeenPersoonsgegevensInLogsBijAgs(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    [Fact]
    public async Task AgsLaatGeenPersoonsgegevensAchterInDeLogs()
    {
        var (logs, secrets) = await AgsFlow.RunAsync(factory, TestContext.Current.CancellationToken);

        foreach (var secret in secrets.Skip(4))
        {
            Assert.DoesNotContain(secret, logs, StringComparison.Ordinal); // user id, names, e-mail, personal number, comment
        }
    }
}
