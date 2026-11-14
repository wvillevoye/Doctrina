using System.Net;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.Host.IntegrationTests.HostUi;
using Doctrina.Modules.Content.Domain;
using Doctrina.Modules.Lti.Contracts;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Modules;
using Microsoft.Extensions.DependencyInjection;

// Spec lti-launch of change add-lti-platform; task 2.3.
namespace Doctrina.Host.IntegrationTests.Academic;

/// <summary>A stand-in for the Lti module's catalog, so Content can be tested before and without the real module.</summary>
internal sealed class StubToolCatalog(params LtiToolSummary[] tools) : ILtiToolCatalog
{
    public Task<IReadOnlyList<LtiToolSummary>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LtiToolSummary>>(tools);

    public Task<bool> ExistsAsync(Guid toolId, CancellationToken cancellationToken) => Task.FromResult(tools.Any(t => t.Id == toolId));
}

internal static class ToolLinkSeed
{
    public static readonly LtiToolSummary Quiz = new(new Guid("33333333-0000-0000-0000-000000000001"), "Quiz tool");

    public static Task<AcademicHost> HostWithToolsAsync(DoctrinaHostFactory factory, CancellationToken ct)
    {
        var stub = new TestModule("LtiStub") { Services = services => services.AddSingleton<ILtiToolCatalog>(new StubToolCatalog(Quiz)) };
        return AcademicHost.CreateAsync(factory, [(AccessHosts.Mapping("content.reader", 0), Doctrina.Modules.Content.ContentPermissions.Read)], ct, "Content", stub);
    }

    public static async Task<ContentItem> ToolLinkAsync(this AcademicHost academic, Guid sectionId, string title, bool published = true)
    {
        var item = await academic.ContentAsync(s => s.AddToolLinkAsync(sectionId, title, Quiz.Id, CancellationToken.None));
        if (published)
        {
            await academic.ContentAsync(s => s.SetItemPublishedAsync(item.Id, true, CancellationToken.None));
        }

        return item;
    }
}

public sealed class ToolLinksInEenSectie(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StudentZietEenGepubliceerdeToolLinkMetLaunch()
    {
        await using var academic = await ToolLinkSeed.HostWithToolsAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        var quiz = await academic.ToolLinkAsync(week1.Id, "Weekly quiz");

        var (status, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var section = Html.Parse(html).QuerySelector("[data-testid=content]")!;
        Assert.Contains("Weekly quiz", section.TextContent, StringComparison.Ordinal);
        var launch = section.QuerySelector("[data-testid=launch]")!;
        Assert.Equal($"/lti/launch/{quiz.Id:D}", launch.GetAttribute("href"));
        Assert.Equal("_blank", launch.GetAttribute("target"));
        Assert.Contains("Launch", launch.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConceptToolLinkIsOnzichtbaarVoorStudenten()
    {
        await using var academic = await ToolLinkSeed.HostWithToolsAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        await academic.ToolLinkAsync(week1.Id, "Weekly quiz", published: false);

        var (_, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}", Ct);

        Assert.DoesNotContain("Weekly quiz", html, StringComparison.Ordinal);
        Assert.Null(Html.Parse(html).QuerySelector("[data-testid=launch]"));
    }

    [Fact]
    public async Task DocentZietEenConceptToolLinkMetDraftEnLaunch()
    {
        await using var academic = await ToolLinkSeed.HostWithToolsAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        await academic.ToolLinkAsync(week1.Id, "Weekly quiz", published: false);

        var (_, html) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{offering}", Ct);

        var section = Html.Parse(html).QuerySelector("[data-testid=content]")!;
        Assert.Contains("Weekly quiz", section.TextContent, StringComparison.Ordinal);
        Assert.Contains("Draft", section.TextContent, StringComparison.Ordinal);
        Assert.NotNull(section.QuerySelector("[data-testid=launch]"));
    }

    [Fact]
    public async Task BeheerpaginaHeeftEenFormulierVoorToolLinksAlsDeLtiModuleAanStaat()
    {
        await using var academic = await ToolLinkSeed.HostWithToolsAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        await academic.PublishedSectionAsync(offering, "Week 1");

        var (status, html) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{offering}/content", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(Html.Parse(html).QuerySelector("[data-testid=add-tool-link]"));
        Assert.Contains("Quiz tool", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZonderLtiModuleGeenFormulierVoorToolLinks()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        await academic.PublishedSectionAsync(offering, "Week 1");

        var (status, html) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{offering}/content", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(Html.Parse(html).QuerySelector("[data-testid=add-link]"));
        Assert.Null(Html.Parse(html).QuerySelector("[data-testid=add-tool-link]"));
    }
}
