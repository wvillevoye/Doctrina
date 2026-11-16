using System.Net;
using System.Text.Json;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.TestKit.Authentication;
using Microsoft.AspNetCore.WebUtilities;

// Spec lti-deep-linking of change add-lti-deep-linking; task 3.1. Class = requirement, method = scenario.
namespace Doctrina.Host.IntegrationTests.Academic;

public sealed class EenDeepLinkingSelectieStarten(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private const string DeepLinking = "https://purl.imsglobal.org/spec/lti-dl/claim/";
    private const string Lti = "https://purl.imsglobal.org/spec/lti/claim/";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DocentStartEenSelectie()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, deepLinking: true);

        using var response = await s.GetAsync(factory, GroupsSeed.Docent, $"/lti/deep-linking/{s.Section.Id:D}/{s.Tool.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("tool.example.org", location.Host);
        Assert.Equal("/login", location.AbsolutePath);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("https://localhost", query["iss"]);
        Assert.Equal("https://tool.example.org/deep-linking", query["target_link_uri"]);
        Assert.Equal(s.Tool.ClientId, query["client_id"]);
        Assert.Equal(s.Tool.DeploymentId, query["lti_deployment_id"]);
        Assert.False(string.IsNullOrWhiteSpace(query["login_hint"]));
        Assert.False(string.IsNullOrWhiteSpace(query["lti_message_hint"]));
    }

    [Fact]
    public async Task HetVerzoekIsEenLtiDeepLinkingRequest()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, deepLinking: true);
        using var session = await (await ToolSession.SignInAsync(s, factory, GroupsSeed.Docent, Ct)).StartDeepLinkingAsync(Ct);

        using var response = await session.AuthorizeAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await LaunchForms.VerifiedAsync(s.Academic, LaunchForms.Field(await response.Content.ReadAsStringAsync(Ct), "id_token"), s.Tool.ClientId, Ct);
        Assert.Equal("LtiDeepLinkingRequest", token.GetClaim(Lti + "message_type").Value);
        Assert.Equal("https://tool.example.org/deep-linking", token.GetClaim(Lti + "target_link_uri").Value);
        var settings = token.GetPayloadValue<JsonElement>(DeepLinking + "deep_linking_settings");
        Assert.Equal("https://localhost/lti/deep-linking/return", settings.GetProperty("deep_link_return_url").GetString());
        Assert.Equal(["ltiResourceLink"], settings.GetProperty("accept_types").EnumerateArray().Select(e => e.GetString()));
        Assert.True(settings.GetProperty("accept_multiple").GetBoolean());
        Assert.False(settings.GetProperty("auto_create").GetBoolean());
        Assert.False(string.IsNullOrEmpty(settings.GetProperty("data").GetString()));
        Assert.Equal(["http://purl.imsglobal.org/vocab/lis/v2/membership#Instructor"], token.GetPayloadValue<string[]>(Lti + "roles"));
        Assert.Equal(s.Offering.ToString("D"), token.GetPayloadValue<JsonElement>(Lti + "context").GetProperty("id").GetString());
        Assert.False(token.TryGetPayloadValue<JsonElement>(Lti + "resource_link", out _));
    }

    [Fact]
    public async Task StudentMagGeenSelectieStarten()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, deepLinking: true);

        using var response = await s.GetAsync(factory, StubUser.Student, $"/lti/deep-linking/{s.Section.Id:D}/{s.Tool.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ToolZonderDeepLinkingGeeft404()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        using var response = await s.GetAsync(factory, GroupsSeed.Docent, $"/lti/deep-linking/{s.Section.Id:D}/{s.Tool.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Lti.DeepLinking.NotSupported", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnbekendeSectieGeeft404()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, deepLinking: true);

        using var response = await s.GetAsync(factory, GroupsSeed.Docent, $"/lti/deep-linking/{Guid.NewGuid():D}/{s.Tool.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NietIngelogdeBezoekerMoetEerstInloggen()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, deepLinking: true);

        using var response = await s.GetAsync(factory, null, $"/lti/deep-linking/{s.Section.Id:D}/{s.Tool.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotEqual("tool.example.org", response.Headers.Location!.Host);
    }
}
