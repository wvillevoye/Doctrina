using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.Host.IntegrationTests.HostUi;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Lti.Application;
using Doctrina.TestKit.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// Specs lti-memberships of change add-lti-nrps; tasks 3.1-3.3, 4.1 and 5.1. The paging of Academic is tested in the Academic
// tests; here the HTTP layer and the rules of MembershipService. Class = requirement, method = scenario.
namespace Doctrina.Host.IntegrationTests.Academic;

internal static class Nrps
{
    public const string Scope = "https://purl.imsglobal.org/spec/lti-nrps/scope/contextmembership.readonly";
    public const string AgsScope = "https://purl.imsglobal.org/spec/lti-ags/scope/score";
    public const string Learner = "http://purl.imsglobal.org/vocab/lis/v2/membership#Learner";
    public const string Instructor = "http://purl.imsglobal.org/vocab/lis/v2/membership#Instructor";
    public const string TeachingAssistant = "http://purl.imsglobal.org/vocab/lis/v2/membership/Instructor#TeachingAssistant";

    /// <summary>An access token signed with the key of the host, as the token endpoint would issue it.</summary>
    public static async Task<string> TokenAsync(LaunchScenario s, string scope, CancellationToken ct)
    {
        using var scoped = s.Academic.Host.Services.CreateScope();
        var key = await scoped.ServiceProvider.GetRequiredService<KeyService>().ActiveAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["iss"] = "https://localhost", ["sub"] = s.Tool.ClientId, ["aud"] = "https://localhost",
            ["iat"] = now.ToUnixTimeSeconds(), ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString("N"), ["scope"] = scope,
        });
        return new JsonWebTokenHandler().CreateToken(payload, new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
    }

    public static HttpClient Client(LaunchScenario s, string? token)
    {
        var client = s.Academic.Host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    public static string Url(Guid offering, string query = "") => $"/lti/nrps/{offering:D}/memberships{query}";

    /// <summary>A client of a tool with membership services and a valid token, for the offering of the scenario.</summary>
    public static async Task<HttpClient> ToolClientAsync(LaunchScenario s, CancellationToken ct) => Client(s, await TokenAsync(s, Scope, ct));

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response, CancellationToken ct) => await response.Content.ReadFromJsonAsync<JsonElement>(ct);

    public static JsonElement[] Members(JsonElement container) => [.. container.GetProperty("members").EnumerateArray()];

    public static string[] Roles(JsonElement member) => [.. member.GetProperty("roles").EnumerateArray().Select(r => r.GetString()!)];
}

public sealed class DeDeelnemersVanEenUitvoeringOphalen(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeelnemersVanEenUitvoering()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);
        using var client = await Nrps.ToolClientAsync(s, Ct);

        using var response = await client.GetAsync(Nrps.Url(s.Offering), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.ims.lti-nrps.v2.membershipcontainer+json", response.Content.Headers.ContentType?.MediaType);
        var container = await Nrps.JsonAsync(response, Ct);
        Assert.Equal($"https://localhost/lti/nrps/{s.Offering:D}/memberships", container.GetProperty("id").GetString());
        var context = container.GetProperty("context");
        Assert.Equal((s.Offering.ToString("D"), "BIO101-2026-P1"), (context.GetProperty("id").GetString(), context.GetProperty("label").GetString()));
        Assert.False(string.IsNullOrEmpty(context.GetProperty("title").GetString()));
        var members = Nrps.Members(container);
        Assert.Equal(4, members.Length); // three students and the lecturer
        Assert.All(members, m =>
        {
            Assert.Equal("Active", m.GetProperty("status").GetString());
            Assert.False(string.IsNullOrEmpty(m.GetProperty("user_id").GetString()));
            Assert.NotEmpty(Nrps.Roles(m));
        });
    }

    [Fact]
    public async Task UserIdIsHetzelfdeAlsSubInDeLaunch()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);
        using var session = await (await ToolSession.SignInAsync(s, factory, StubUser.Student, Ct)).StartAsync(Ct);
        using var launch = await session.AuthorizeAsync(Ct);
        var idToken = await LaunchForms.VerifiedAsync(s.Academic, LaunchForms.Field(await launch.Content.ReadAsStringAsync(Ct), "id_token"), s.Tool.ClientId, Ct);
        using var client = await Nrps.ToolClientAsync(s, Ct);

        var members = Nrps.Members(await Nrps.JsonAsync(await client.GetAsync(Nrps.Url(s.Offering), Ct), Ct));

        Assert.Contains(idToken.Subject, members.Select(m => m.GetProperty("user_id").GetString()));
    }

    [Fact]
    public async Task PersoonMetMeerRollenKomtEenmaalVoor()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);
        await s.Academic.SeedAsync("associations", Seeds.Enrolment("A9", "P7000002", "BIO101-2026-P1", "teaching-assistant"));
        using var client = await Nrps.ToolClientAsync(s, Ct);

        var members = Nrps.Members(await Nrps.JsonAsync(await client.GetAsync(Nrps.Url(s.Offering), Ct), Ct));

        var teacherId = (await s.Academic.PersonIdAsync("P7000002")).ToString("N");
        var teacher = Assert.Single(members, m => m.GetProperty("user_id").GetString() == teacherId);
        Assert.Equal([Nrps.Instructor, Nrps.TeachingAssistant], Nrps.Roles(teacher).Order(StringComparer.Ordinal));
        Assert.Equal(4, members.Length);
    }

    [Fact]
    public async Task UitvoeringZonderToolLinkVanDezeTool()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);
        using var client = await Nrps.ToolClientAsync(s, Ct);

        using var response = await client.GetAsync(Nrps.Url(Guid.NewGuid()), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task VerwijderdeInschrijvingOfPersoon()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);
        await s.Academic.SeedAsync("associations", Seeds.Enrolment("A3", "I6000003", "BIO101-2026-P1", "student", "toBeDeleted"));
        var gone = Seeds.Person("I6000002", "Sam", "Smit");
        gone["status"] = "toBeDeleted";
        await s.Academic.SeedAsync("persons", gone);
        using var client = await Nrps.ToolClientAsync(s, Ct);

        var members = Nrps.Members(await Nrps.JsonAsync(await client.GetAsync(Nrps.Url(s.Offering), Ct), Ct));

        Assert.Equal(2, members.Length); // Sanne and the lecturer
    }
}

public sealed class RollenVolgenDeLisRollenVanDeLaunch(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RollenVanEenStudentEnEenDocent()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);
        using var client = await Nrps.ToolClientAsync(s, Ct);

        var members = Nrps.Members(await Nrps.JsonAsync(await client.GetAsync(Nrps.Url(s.Offering), Ct), Ct));

        var student = (await s.Academic.PersonIdAsync("I6000001")).ToString("N");
        var teacher = (await s.Academic.PersonIdAsync("P7000002")).ToString("N");
        Assert.Equal([Nrps.Learner], Nrps.Roles(members.Single(m => m.GetProperty("user_id").GetString() == student)));
        Assert.Equal([Nrps.Instructor], Nrps.Roles(members.Single(m => m.GetProperty("user_id").GetString() == teacher)));
    }

    [Fact]
    public async Task FilterenOpRol()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);
        using var client = await Nrps.ToolClientAsync(s, Ct);

        var learners = Nrps.Members(await Nrps.JsonAsync(await client.GetAsync(Nrps.Url(s.Offering, "?role=Learner"), Ct), Ct));
        var instructors = Nrps.Members(await Nrps.JsonAsync(await client.GetAsync(Nrps.Url(s.Offering, $"?role={Uri.EscapeDataString(Nrps.Instructor)}"), Ct), Ct));
        using var unknown = await client.GetAsync(Nrps.Url(s.Offering, "?role=Nonsense"), Ct);

        Assert.Equal(3, learners.Length);
        Assert.All(learners, m => Assert.Equal([Nrps.Learner], Nrps.Roles(m)));
        Assert.Equal([Nrps.Instructor], Assert.Single(instructors).GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }
}

public sealed class NaamEnEmailAlleenAlsDeToolDaarvoorIsIngesteld(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> StudentAsync(LaunchScenario s)
    {
        var person = Seeds.Person("I6000001", "Sanne", "Student");
        person["mail"] = "sanne@example.test";
        await s.Academic.SeedAsync("persons", person);
        using var client = await Nrps.ToolClientAsync(s, Ct);
        var members = Nrps.Members(await Nrps.JsonAsync(await client.GetAsync(Nrps.Url(s.Offering), Ct), Ct));
        var id = (await s.Academic.PersonIdAsync("I6000001")).ToString("N");
        return members.Single(m => m.GetProperty("user_id").GetString() == id).Clone();
    }

    [Fact]
    public async Task ZonderInstellingGeenNamen()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);

        var student = await StudentAsync(s);

        Assert.Equal(["roles", "status", "user_id"], student.EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task MetInstellingNaamEnEmail()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, sendName: true, sendEmail: true, membershipServices: true);

        var student = await StudentAsync(s);

        Assert.Equal(
            ("Sanne Student", "Sanne", "Student", "sanne@example.test"),
            (student.GetProperty("name").GetString(), student.GetProperty("given_name").GetString(), student.GetProperty("family_name").GetString(), student.GetProperty("email").GetString()));
    }

    [Fact]
    public async Task AlleenNaamZonderEmail()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, sendName: true, membershipServices: true);

        var student = await StudentAsync(s);

        Assert.Equal("Sanne Student", student.GetProperty("name").GetString());
        Assert.False(student.TryGetProperty("email", out _));
    }
}

public sealed class DeLijstIsGepagineerdEnTeBeperkenTotEenResourceLink(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Paginering()
    {
        // Four participants and limit 3 (the paging itself is tested with more people in the Academic tests).
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);
        using var client = await Nrps.ToolClientAsync(s, Ct);

        using var first = await client.GetAsync(Nrps.Url(s.Offering, "?limit=3"), Ct);
        var next = Assert.Single(first.Headers.GetValues("Link"));
        Assert.EndsWith("; rel=\"next\"", next, StringComparison.Ordinal);
        using var second = await client.GetAsync(new Uri(next[1..next.IndexOf('>', StringComparison.Ordinal)]), Ct);

        var ids = Nrps.Members(await Nrps.JsonAsync(first, Ct)).Concat(Nrps.Members(await Nrps.JsonAsync(second, Ct))).Select(m => m.GetProperty("user_id").GetString()).ToList();
        Assert.Equal(4, ids.Count);
        Assert.Equal(4, ids.Distinct().Count());
        Assert.False(second.Headers.Contains("Link"));
    }

    [Fact]
    public async Task TeGroteLimit()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);
        using var client = await Nrps.ToolClientAsync(s, Ct);

        using var tooLarge = await client.GetAsync(Nrps.Url(s.Offering, "?limit=501"), Ct);
        using var zero = await client.GetAsync(Nrps.Url(s.Offering, "?limit=0"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal("Lti.Nrps.InvalidRequest", (await Nrps.JsonAsync(tooLarge, Ct)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task OnbekendeResourceLink()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);
        using var client = await Nrps.ToolClientAsync(s, Ct);

        using var known = await client.GetAsync(Nrps.Url(s.Offering, $"?rlid={s.Link.Id:D}"), Ct);
        using var unknown = await client.GetAsync(Nrps.Url(s.Offering, $"?rlid={Guid.NewGuid():D}"), Ct);
        using var invalid = await client.GetAsync(Nrps.Url(s.Offering, "?rlid=not-a-guid"), Ct);

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(4, Nrps.Members(await Nrps.JsonAsync(known, Ct)).Length); // the same list as without rlid
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}

public sealed class AlleenMetDeScopeEnMetLedenservices(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZonderToken()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);
        using var client = Nrps.Client(s, null);

        using var response = await client.GetAsync(Nrps.Url(s.Offering), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task TokenZonderNrpsScope()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);
        using var client = Nrps.Client(s, await Nrps.TokenAsync(s, Nrps.AgsScope, Ct));

        using var response = await client.GetAsync(Nrps.Url(s.Offering), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ToolZonderLedenservices()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, gradeServices: true);
        using var tool = new DeepLinkingTool(s);
        using var client = tool.Host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var response = await client.PostAsync("/lti/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_assertion_type"] = ServiceAuthService.AssertionType,
            ["client_assertion"] = tool.ClientAssertion(),
            ["scope"] = Nrps.Scope,
        }), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_scope", (await Nrps.JsonAsync(response, Ct)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task ToolMetLedenservicesKrijgtDeScope()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);
        using var tool = new DeepLinkingTool(s);
        using var client = tool.Host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var response = await client.PostAsync("/lti/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_assertion_type"] = ServiceAuthService.AssertionType,
            ["client_assertion"] = tool.ClientAssertion(),
            ["scope"] = $"{Nrps.Scope} {Nrps.AgsScope}",
        }), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Nrps.Scope, (await Nrps.JsonAsync(response, Ct)).GetProperty("scope").GetString()); // not the AGS scope
    }
}

public sealed class DeIdTokenVermeldtDeLedenservices(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private const string Claim = "https://purl.imsglobal.org/spec/lti-nrps/claim/namesroleservice";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<JsonWebToken> TokenAsync(LaunchScenario s)
    {
        using var session = await (await ToolSession.SignInAsync(s, factory, StubUser.Student, Ct)).StartAsync(Ct);
        using var response = await session.AuthorizeAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await LaunchForms.VerifiedAsync(s.Academic, LaunchForms.Field(await response.Content.ReadAsStringAsync(Ct), "id_token"), s.Tool.ClientId, Ct);
    }

    [Fact]
    public async Task LaunchVanEenToolMetLedenservices()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, membershipServices: true);

        var claim = (await TokenAsync(s)).GetPayloadValue<JsonElement>(Claim);

        Assert.Equal($"https://localhost/lti/nrps/{s.Offering:D}/memberships", claim.GetProperty("context_memberships_url").GetString());
        Assert.Equal(["2.0"], claim.GetProperty("service_versions").EnumerateArray().Select(v => v.GetString()));
    }

    [Fact]
    public async Task ToolZonderLedenservicesKrijgtGeenNrpsClaim()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        var token = await TokenAsync(s);

        Assert.False(token.TryGetPayloadValue<JsonElement>(Claim, out _));
    }
}

// Spec lti-memberships of change add-lti-nrps; task 5.1.
public sealed class GeenPersoonsgegevensInLogsBijNrps(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    [Fact]
    public async Task NrpsLaatGeenPersoonsgegevensAchterInDeLogs()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = await LaunchScenario.CreateAsync(factory, ct, sendName: true, sendEmail: true, membershipServices: true);
        var person = Seeds.Person("I6000001", "Sanne", "Student");
        person["mail"] = "sanne.secret@example.test";
        await s.Academic.SeedAsync("persons", person);
        var capture = new Doctrina.TestKit.Modules.CapturingLoggerProvider();
        using var tool = new DeepLinkingTool(s, capture);
        using var client = tool.Host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var tokenResponse = await client.PostAsync("/lti/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_assertion_type"] = ServiceAuthService.AssertionType,
            ["client_assertion"] = tool.ClientAssertion(),
            ["scope"] = Nrps.Scope,
        }), ct);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await Nrps.JsonAsync(tokenResponse, ct)).GetProperty("access_token").GetString());
        using var response = await client.GetAsync(Nrps.Url(s.Offering), ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("sanne.secret@example.test", body, StringComparison.Ordinal); // the tool did get them

        Assert.NotEmpty(capture.Logs); // the collector really sees the host
        var logs = string.Join(Environment.NewLine, capture.Logs.Select(l => l.Message + l.Exception));
        using var scope = tool.Host.Services.CreateScope();
        var members = await scope.ServiceProvider.GetRequiredService<IAcademicDirectory>().MembersAsync(s.Offering, ct);
        var secrets = members.SelectMany(m => new[] { m.PersonId.ToString("N"), m.PersonId.ToString("D"), m.Name, m.ExternalId })
            .Concat(["sanne.secret@example.test", StubUser.Student.Email!, StubUser.Student.PersonId!]);
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, logs, StringComparison.Ordinal);
        }
    }
}
