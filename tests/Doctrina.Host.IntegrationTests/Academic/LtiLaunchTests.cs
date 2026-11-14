using System.Net;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.Modules.Content.Domain;
using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Domain;
using Doctrina.TestKit.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

// Spec lti-launch of change add-lti-platform; tasks 4.1 to 4.4. Class = requirement, method = scenario.
namespace Doctrina.Host.IntegrationTests.Academic;

/// <summary>A host with the Lti stack, a course offering with people, a registered tool and a tool link in a published section.</summary>
internal sealed class LaunchScenario : IAsyncDisposable
{
    private LaunchScenario(AcademicHost academic) => Academic = academic;

    public AcademicHost Academic { get; }

    public Guid Offering { get; private set; }

    public LtiTool Tool { get; private set; } = null!;

    public ContentItem Link { get; private set; } = null!;

    public ContentSection Section { get; private set; } = null!;

    public static async Task<LaunchScenario> CreateAsync(DoctrinaHostFactory factory, CancellationToken ct, bool published = true, bool sendName = false, bool sendEmail = false)
    {
        var scenario = new LaunchScenario(await LtiSeed.HostAsync(factory, ct));
        var academic = scenario.Academic;
        scenario.Offering = await academic.OfferingWithPeopleAsync();
        scenario.Tool = await academic.ToolAsync(sendName: sendName, sendEmail: sendEmail);
        scenario.Section = await academic.PublishedSectionAsync(scenario.Offering, "Week 1");
        var link = await academic.ContentAsync(s => s.AddToolLinkAsync(scenario.Section.Id, "Weekly quiz", scenario.Tool.Id, CancellationToken.None));
        if (published)
        {
            await academic.ContentAsync(s => s.SetItemPublishedAsync(link.Id, true, CancellationToken.None));
        }

        scenario.Link = link;
        return scenario;
    }

    /// <summary>Signs in as <paramref name="user"/> (or not, for null) and opens <paramref name="path"/> without following redirects.</summary>
    public async Task<HttpResponseMessage> GetAsync(DoctrinaHostFactory factory, StubUser? user, string path, CancellationToken ct)
    {
        using var browser = Academic.Host.CreateBrowser();
        if (user is not null)
        {
            await browser.SignInAndGetAsync("/profile", factory.Idp, user, ct);
        }

        return await browser.GetAsync(new Uri(path, UriKind.Relative), ct);
    }

    public ValueTask DisposeAsync() => Academic.DisposeAsync();
}

public sealed class LaunchStartenMetLoginInitiation(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeelnemerStartEenLaunch()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        using var response = await s.GetAsync(factory, StubUser.Student, $"/lti/launch/{s.Link.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("tool.example.org", location.Host);
        Assert.Equal("/login", location.AbsolutePath);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("https://localhost", query["iss"]);
        Assert.Equal("https://tool.example.org/launch", query["target_link_uri"]);
        Assert.Equal(s.Tool.ClientId, query["client_id"]);
        Assert.Equal(s.Tool.DeploymentId, query["lti_deployment_id"]);
        Assert.False(string.IsNullOrWhiteSpace(query["login_hint"]));
        Assert.False(string.IsNullOrWhiteSpace(query["lti_message_hint"]));
    }

    [Fact]
    public async Task LoginHintIsOndoorzichtig()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        using var response = await s.GetAsync(factory, StubUser.Student, $"/lti/launch/{s.Link.Id:D}", Ct);

        var location = response.Headers.Location!.ToString();
        Assert.DoesNotContain("student@example.test", location, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("I6000001", location, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sanne", location, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("^[0-9a-f]{32}$", QueryHelpers.ParseQuery(response.Headers.Location!.Query)["login_hint"].ToString());
    }

    [Fact]
    public async Task BeheerderMetAlleenLtiManageHeeftGeenLeesrechtOpContent()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        using var response = await s.GetAsync(factory, LtiSeed.Admin, $"/lti/launch/{s.Link.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // Lti.Manage alone doesn't give read access to course content
    }

    [Fact]
    public async Task ConceptToolLinkStartNietVoorEenStudent()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct, published: false);

        using var student = await s.GetAsync(factory, StubUser.Student, $"/lti/launch/{s.Link.Id:D}", Ct);
        using var lecturer = await s.GetAsync(factory, GroupsSeed.Docent, $"/lti/launch/{s.Link.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, student.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, lecturer.StatusCode); // lecturers may try out their own drafts
    }

    [Fact]
    public async Task NietDeelnemerMagNietStarten()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        using var response = await s.GetAsync(factory, ContentSeed.Outsider, $"/lti/launch/{s.Link.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LaunchNaarEenVerwijderdeToolGeeft404()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        using (var scope = s.Academic.Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ToolService>().DeleteAsync(s.Tool.Id, Ct);
        }

        using var response = await s.GetAsync(factory, StubUser.Student, $"/lti/launch/{s.Link.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Lti.Tool.NotFound", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ItemDatGeenToolLinkIsGeeft404()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);
        var page = await s.Academic.PageAsync(s.Section.Id, "Introduction", "# Welcome");

        using var response = await s.GetAsync(factory, StubUser.Student, $"/lti/launch/{page.Id:D}", Ct);
        using var unknown = await s.GetAsync(factory, StubUser.Student, $"/lti/launch/{Guid.NewGuid():D}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task NietIngelogdeBezoekerMoetEerstInloggen()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        using var response = await s.GetAsync(factory, null, $"/lti/launch/{s.Link.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotEqual("tool.example.org", response.Headers.Location!.Host);
    }
}
