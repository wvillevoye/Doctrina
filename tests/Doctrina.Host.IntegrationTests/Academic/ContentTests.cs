using System.Net;
using Doctrina.Core.Results;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.Host.IntegrationTests.HostUi;
using Doctrina.Modules.Content.Application;
using Doctrina.Modules.Content.Domain;
using Doctrina.TestKit.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Host.IntegrationTests.Academic;

internal static class ContentSeed
{
    /// <summary>Runs a change through the Content service, as the page would.</summary>
    public static async Task<T> ContentAsync<T>(this AcademicHost academic, Func<ContentService, Task<Result<T>>> change)
    {
        using var scope = academic.Host.Services.CreateScope();
        var result = await change(scope.ServiceProvider.GetRequiredService<ContentService>());
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value!;
    }

    public static async Task ContentAsync(this AcademicHost academic, Func<ContentService, Task<Result>> change)
    {
        using var scope = academic.Host.Services.CreateScope();
        var result = await change(scope.ServiceProvider.GetRequiredService<ContentService>());
        Assert.True(result.IsSuccess, result.Error?.ToString());
    }

    public static async Task<ContentSection> PublishedSectionAsync(this AcademicHost academic, Guid offering, string title, bool published = true)
    {
        var section = await academic.ContentAsync(s => s.CreateSectionAsync(offering, title, CancellationToken.None));
        if (published)
        {
            await academic.ContentAsync(s => s.SetSectionPublishedAsync(section.Id, true, CancellationToken.None));
        }

        return section;
    }

    public static async Task<ContentItem> PageAsync(this AcademicHost academic, Guid sectionId, string title, string body, bool published = true)
    {
        var page = await academic.ContentAsync(s => s.AddPageAsync(sectionId, title, CancellationToken.None));
        await academic.ContentAsync(s => s.SavePageAsync(page.Id, title, body, CancellationToken.None));
        if (published)
        {
            await academic.ContentAsync(s => s.SetItemPublishedAsync(page.Id, true, CancellationToken.None));
        }

        return page;
    }

    public static async Task<ContentItem> LinkAsync(this AcademicHost academic, Guid sectionId, string title, string url, bool published = true)
    {
        var link = await academic.ContentAsync(s => s.AddLinkAsync(sectionId, title, url, CancellationToken.None));
        if (published)
        {
            await academic.ContentAsync(s => s.SetItemPublishedAsync(link.Id, true, CancellationToken.None));
        }

        return link;
    }

    public static StubUser Outsider { get; } = new("sub-outsider", "Xavier Buiten", ["student"], "xavier@example.test", "I6000099");

    public static StubUser Reader { get; } = new("sub-reader", "Rita Lezer", ["content.reader"], "rita@example.test", "P7000099");

    public static Task<AcademicHost> HostAsync(DoctrinaHostFactory factory, CancellationToken ct) =>
        AcademicHost.CreateAsync(factory, [(AccessHosts.Mapping("content.reader", 0), Doctrina.Modules.Content.ContentPermissions.Read)], ct, "Content");
}

// Spec course-content, requirement "Deelnemers zien de gepubliceerde content op de pagina van de uitvoering".
public sealed class DeelnemersZienDeGepubliceerdeContentOpDePaginaVanDeUitvoering(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StudentZietGepubliceerdeContent()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        var intro = await academic.PageAsync(week1.Id, "Introduction", "# Welcome");
        await academic.LinkAsync(week1.Id, "Course guide", "https://example.test/guide");

        var (status, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var section = Html.Parse(html).QuerySelector("[data-testid=content]")!;
        Assert.Equal(["Week 1"], section.QuerySelectorAll("[data-testid=content-section]").Select(h => h.TextContent.Trim()));
        var links = section.QuerySelectorAll("[data-testid=content-items] a").ToList();
        Assert.Equal("Introduction", links[0].TextContent.Trim());
        Assert.Equal($"/courses/{offering:D}/content/{intro.Id:D}", links[0].GetAttribute("href"));
        Assert.StartsWith("Course guide", links[1].TextContent.Trim(), StringComparison.Ordinal);
        Assert.Equal("https://example.test/guide", links[1].GetAttribute("href"));
        Assert.Equal("_blank", links[1].GetAttribute("target"));
        Assert.Null(section.QuerySelector("[data-testid=manage-content]"));
        Assert.DoesNotContain("Draft", section.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SectiesVanMeerdereModulesNaastElkaar()
    {
        // Regression: sections render at the same time and shared one AcademicDbContext through IAcademicDirectory
        // ("A second operation was started on this context instance"). Admins skipped the database, so only
        // students and lecturers ran into it.
        await using var academic = await AcademicHost.CreateAsync(
            factory, [(AccessHosts.Mapping("content.reader", 0), Doctrina.Modules.Content.ContentPermissions.Read)], Ct, "Content", "Groups");
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        await academic.PageAsync(week1.Id, "Introduction", "# Welcome");

        foreach (var user in new[] { StubUser.Student, GroupsSeed.Docent })
        {
            var (status, html) = await academic.GetAsync(factory, user, $"/courses/{offering}", Ct);

            Assert.Equal(HttpStatusCode.OK, status);
            var page = Html.Parse(html);
            Assert.NotNull(page.QuerySelector("[data-testid=content]"));
            Assert.NotNull(page.QuerySelector("[data-testid=my-groups]"));
        }
    }

    [Fact]
    public async Task UitvoeringZonderContent()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var hidden = await academic.PublishedSectionAsync(offering, "Week 2", published: false);
        await academic.PageAsync(hidden.Id, "Secret", "text");

        var (_, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}", Ct);

        Assert.Contains("No content yet.", Html.Parse(html).QuerySelector("[data-testid=content]")!.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocentZietConcepten()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        await academic.PublishedSectionAsync(offering, "Week 2", published: false);

        var (_, html) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{offering}", Ct);

        var section = Html.Parse(html).QuerySelector("[data-testid=content]")!;
        var heading = section.QuerySelector("[data-testid=content-section]")!.TextContent;
        Assert.Contains("Week 2", heading, StringComparison.Ordinal);
        Assert.Contains("Draft", heading, StringComparison.Ordinal);
        var button = section.QuerySelector("[data-testid=manage-content]");
        Assert.Equal($"/courses/{offering:D}/content", button?.GetAttribute("href"));
        Assert.Equal("Manage content", button?.TextContent.Trim());
    }

    [Fact]
    public async Task NietIngeschrevenStudentOpentEenPagina()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        var intro = await academic.PageAsync(week1.Id, "Introduction", "# Welcome");

        var (status, html) = await academic.GetAsync(factory, ContentSeed.Outsider, $"/courses/{offering}/content/{intro.Id}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains(">Access denied</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LezenMetContentReadZonderInschrijving()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        var intro = await academic.PageAsync(week1.Id, "Introduction", "# Welcome");

        var (status, html) = await academic.GetAsync(factory, ContentSeed.Reader, $"/courses/{offering}/content/{intro.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Welcome</h1>", html, StringComparison.Ordinal);
    }
}

// Spec course-content, requirement "Content is pas zichtbaar na publiceren" (pages) and "Pagina's worden in Markdown...".
public sealed class ContentIsPasZichtbaarNaPubliceren_Paginas(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StudentOpentEenVerborgenPaginaRechtstreeks()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        var draft = await academic.PageAsync(week1.Id, "Introduction", "# Welcome", published: false);

        var (status, _) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}/content/{draft.Id}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task OnbekendItem()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();

        var (status, _) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}/content/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task ItemVanEenAndereUitvoeringIsNietGevonden()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        var intro = await academic.PageAsync(week1.Id, "Introduction", "# Welcome");
        var other = await academic.QueryAsync(db => Task.FromResult(db.CourseOfferings.Where(o => o.ExternalId == "WIS201-2026-P1").Select(o => o.Id).Single()));
        var wim = new StubUser("sub-wim", "Wim Wiskunde", ["employee"], PersonId: "P7000003");

        var (status, _) = await academic.GetAsync(factory, wim, $"/courses/{other}/content/{intro.Id}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task DocentOpentEenConcept()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        var draft = await academic.PageAsync(week1.Id, "Introduction", "# Welcome", published: false);

        var (status, html) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{offering}/content/{draft.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var page = Html.Parse(html);
        Assert.Contains("Welcome</h1>", html, StringComparison.Ordinal);
        Assert.Equal("Draft", page.QuerySelector("[data-testid=draft]")?.TextContent.Trim());
        Assert.NotNull(page.QuerySelector("[data-testid=edit-page]"));
    }

    [Fact]
    public async Task OpmaakWordtWeergegeven()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        var intro = await academic.PageAsync(week1.Id, "Introduction", "# Welcome\n\n**Read this first**");

        var (status, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}/content/{intro.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var markdown = Html.Parse(html).QuerySelector("[data-testid=markdown]")!;
        Assert.Equal("Welcome", markdown.QuerySelector("h1")?.TextContent);
        Assert.Equal("Read this first", markdown.QuerySelector("strong")?.TextContent);
        Assert.Null(Html.Parse(html).QuerySelector("[data-testid=edit-page]"));
    }

    [Fact]
    public async Task HtmlEnScriptsWordenNietUitgevoerd()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        var intro = await academic.PageAsync(week1.Id, "Introduction", "<script>alert(1)</script>\n\n[x](javascript:alert(1))");

        // The raw response: AcademicHost.GetAsync decodes entities first, which would turn the escaped text back into markup.
        using var browser = academic.Host.CreateBrowser();
        await browser.SignInAndGetAsync("/profile", factory.Idp, StubUser.Student, Ct);
        var html = await browser.GetStringAsync(new Uri($"/courses/{offering}/content/{intro.Id}", UriKind.Relative), Ct);

        var markdown = Html.Parse(html).QuerySelector("[data-testid=markdown]")!;
        Assert.Null(markdown.QuerySelector("script"));
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", markdown.InnerHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", markdown.InnerHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SectieOmlaagVerplaatsenOokVoorStudenten()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        await academic.PublishedSectionAsync(offering, "Week 2");
        await academic.ContentAsync(s => s.MoveSectionAsync(week1.Id, 1, CancellationToken.None));

        var (_, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}", Ct);

        Assert.Equal(["Week 2", "Week 1"], Html.Parse(html).QuerySelectorAll("[data-testid=content-section]").Select(h => h.TextContent.Trim()));
    }
}

// Spec course-content, requirement "Alleen docenten van de uitvoering en beheerders beheren content".
public sealed class AlleenDocentenVanDeUitvoeringEnBeheerdersBeherenContent(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StudentMagGeenContentBeheren()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();

        var (status, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}/content", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains(">Access denied</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocentVanEenAndereUitvoering()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var wim = new StubUser("sub-wim", "Wim Wiskunde", ["employee"], PersonId: "P7000003");

        var (status, _) = await academic.GetAsync(factory, wim, $"/courses/{offering}/content", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task BeheerderZonderInschrijving()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();

        var (status, html) = await academic.GetAsync(factory, StubUser.Beheerder, $"/courses/{offering}/content", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Content · BIO101-2026-P1 · Doctrina", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocentVanDeUitvoering()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1", published: false);
        await academic.PageAsync(week1.Id, "Introduction", "# Welcome", published: false);

        var (status, html) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{offering}/content", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var card = Html.Parse(html).QuerySelector("[data-testid=section]")!;
        Assert.Contains("Week 1", card.TextContent, StringComparison.Ordinal);
        Assert.Equal(["Introduction"], card.QuerySelectorAll("[data-testid=item-title]").Select(t => t.TextContent.Trim()));
    }

    [Fact]
    public async Task OnbekendeUitvoering()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);

        var (status, _) = await academic.GetAsync(factory, StubUser.Beheerder, $"/courses/{Guid.Empty}/content", Ct);

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task DocentOpentDeBewerkpaginaVanEenPagina()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var week1 = await academic.PublishedSectionAsync(offering, "Week 1");
        var intro = await academic.PageAsync(week1.Id, "Introduction", "# Welcome");

        var (docent, html) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{offering}/content/{intro.Id}/edit", Ct);
        var (student, _) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}/content/{intro.Id}/edit", Ct);

        Assert.Equal(HttpStatusCode.OK, docent);
        Assert.Contains("Edit page", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, student);
    }
}
