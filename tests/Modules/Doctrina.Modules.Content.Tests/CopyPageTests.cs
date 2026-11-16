using Bunit;
using Doctrina.Modules.Content.Components.Pages;
using Microsoft.AspNetCore.Components;

namespace Doctrina.Modules.Content.Tests;

// Spec course-content of change add-course-templates; task 3.1. The card on the management page.

public sealed partial class ContentKopierenNaarEenLegeUitvoering
{
    private static IRenderedComponent<ManageContent> Render(PageFixture f) =>
        f.Context.Render<ManageContent>(p => p.Add(c => c.OfferingId, f.Directory.OfferingId));

    [Fact]
    public async Task DeKaartVerschijntAlleenAlsKopierenKan()
    {
        await using var f = await PageFixture.CreateAsync();

        // Empty offering with a sibling: the card lists the other offering.
        var empty = Render(f);
        empty.WaitForElement("[data-testid=copy-content]", PageFixture.Wait);
        Assert.Equal(["Choose an offering…", "Cell biology (2025-P1)"], empty.FindAll("[data-testid=copy-content] option").Select(o => o.TextContent.Trim()));

        // An offering with a section has no card.
        await f.Service().SectionAsync(f.Directory, "Week 1");
        var filled = Render(f);
        filled.WaitForElement("[data-testid=section]", PageFixture.Wait);
        Assert.Empty(filled.FindAll("[data-testid=copy-content]"));
    }

    [Fact]
    public async Task DeKaartVerschijntNietZonderAndereUitvoeringen()
    {
        await using var f = await PageFixture.CreateAsync();
        f.Directory.HasSiblings = false;

        var page = Render(f);
        page.WaitForElement("[data-testid=new-section]", PageFixture.Wait);

        Assert.Empty(page.FindAll("[data-testid=copy-content]"));
    }

    [Fact]
    public async Task DocentKopieertViaDePagina()
    {
        await using var f = await PageFixture.CreateAsync();
        f.Directory.SourceMembers.Add(f.Lecturer);
        var sourceSection = (await f.Service().CreateSectionAsync(f.Directory.SourceOfferingId, "Week 1", Xunit.TestContext.Current.CancellationToken)).Value!;
        await f.Service().AddLinkAsync(sourceSection.Id, "Syllabus", "https://example.test/s", Xunit.TestContext.Current.CancellationToken);
        var page = Render(f);
        page.WaitForElement("[data-testid=copy-content]", PageFixture.Wait);

        await page.InvokeAsync(() => page.Find("[data-testid=copy-content] select").ChangeAsync(new ChangeEventArgs { Value = f.Directory.SourceOfferingId.ToString("D") }));
        await page.InvokeAsync(() => page.Find("[data-testid=copy-content]").SubmitAsync());

        page.WaitForElement("[data-testid=content-message]", PageFixture.Wait);
        Assert.Equal("Copied 1 sections and 1 items as drafts.", page.Find("[data-testid=content-message] p").TextContent.Trim());
        Assert.Equal(["Week 1"], await f.Service().TitlesAsync(f.Directory.OfferingId, drafts: true));
        Assert.Empty(page.FindAll("[data-testid=copy-content]")); // the offering has content now
    }
}
