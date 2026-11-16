using Bunit;
using Doctrina.Modules.Content.Components.Pages;
using Doctrina.Modules.Lti.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Modules.Content.Tests;

// Spec lti-deep-linking of change add-lti-deep-linking; task 6.2. The card on the management page; the flow itself is
// tested in the Lti tests and against the host. Class = requirement, method = scenario.

public sealed class EenDeepLinkingSelectieStartenOpDePagina
{
    private static readonly LtiToolSummary Quiz = new(Guid.NewGuid(), "Quiz tool", true);
    private static readonly LtiToolSummary Plain = new(Guid.NewGuid(), "Plain tool", false);

    private static async Task<(PageFixture Fixture, Guid Section)> SetUpAsync(params LtiToolSummary[] tools)
    {
        var f = await PageFixture.CreateAsync();
        f.Context.Services.AddSingleton<ILtiToolCatalog>(new FakeToolCatalog(tools));
        var section = await f.Service().SectionAsync(f.Directory, "Week 1");
        return (f, section.Id);
    }

    private static IRenderedComponent<ManageContent> Render(PageFixture f) =>
        f.Context.Render<ManageContent>(p => p.Add(c => c.OfferingId, f.Directory.OfferingId));

    [Fact]
    public async Task DeKaartToontAlleenToolsMetDeepLinking()
    {
        var (f, _) = await SetUpAsync(Quiz, Plain);
        await using var _ = f;

        var page = Render(f);
        page.WaitForElement("[data-testid=add-from-tool]", PageFixture.Wait);

        var tools = page.FindAll("[data-testid=add-from-tool] select")[1].QuerySelectorAll("option").Select(o => o.TextContent.Trim());
        Assert.Equal(["Choose a tool…", "Quiz tool"], tools);
    }

    [Fact]
    public async Task ZonderToolsMetDeepLinkingGeenKaart()
    {
        var (f, _) = await SetUpAsync(Plain);
        await using var _ = f;

        var page = Render(f);
        page.WaitForElement("[data-testid=add-tool-link]", PageFixture.Wait);

        Assert.Empty(page.FindAll("[data-testid=add-from-tool]"));
    }

    [Fact]
    public async Task DocentStuurtDeSelectieNaarDeTool()
    {
        var (f, section) = await SetUpAsync(Quiz);
        await using var _ = f;
        var page = Render(f);
        page.WaitForElement("[data-testid=add-from-tool]", PageFixture.Wait);

        await page.InvokeAsync(() => page.FindAll("[data-testid=add-from-tool] select")[0].ChangeAsync(new ChangeEventArgs { Value = section.ToString("D") }));
        await page.InvokeAsync(() => page.FindAll("[data-testid=add-from-tool] select")[1].ChangeAsync(new ChangeEventArgs { Value = Quiz.Id.ToString("D") }));
        await page.InvokeAsync(() => page.Find("[data-testid=add-from-tool]").SubmitAsync());

        Assert.EndsWith($"/lti/deep-linking/{section:D}/{Quiz.Id:D}", f.Context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }
}
