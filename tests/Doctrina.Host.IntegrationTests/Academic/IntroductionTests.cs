using System.Net;
using Doctrina.Host.IntegrationTests.HostUi;
using Doctrina.Modules.Content.Application;
using Doctrina.Modules.Content.Domain;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Doctrina.Host.IntegrationTests.Academic.AcademicHost;

namespace Doctrina.Host.IntegrationTests.Academic;

internal static class IntroductionSeed
{
    public static async Task IntroductionAsync(this AcademicHost academic, Guid offering, string body, bool published = true, Guid? templateId = null)
    {
        using var scope = academic.Host.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IntroductionService>().SaveAsync(offering, templateId, body, published, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.ToString());
    }

    public static async Task<Guid> TemplateAsync(this AcademicHost academic, string name, IntroductionLayout layout)
    {
        using var scope = academic.Host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IntroductionService>();
        Assert.True((await service.SaveTemplateAsync(null, name, layout, string.Empty, CancellationToken.None)).IsSuccess);
        return (await service.TemplatesAsync(CancellationToken.None)).Single(t => t.Name == name).Id;
    }

    public static async Task DeleteTemplateAsync(this AcademicHost academic, Guid id)
    {
        using var scope = academic.Host.Services.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<IntroductionService>().DeleteTemplateAsync(id, CancellationToken.None)).IsSuccess);
    }
}

// Spec course-introduction, requirement "De introductie staat boven de content van een uitvoering".
public sealed class DeIntroductieStaatBovenDeContentVanEenUitvoering(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task IntroductieBovenDeContent()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        await academic.PublishedSectionAsync(offering, "Week 1");
        await academic.IntroductionAsync(offering, "# Welcome");

        var (status, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var page = Html.Parse(html);
        Assert.Equal("Welcome", page.QuerySelector("[data-testid=introduction] h1")!.TextContent);
        Assert.True(html.IndexOf("data-testid=\"introduction\"", StringComparison.Ordinal) < html.IndexOf("data-testid=\"content\"", StringComparison.Ordinal));
        Assert.Contains(page.QuerySelectorAll("[data-testid=course-nav] a"), a => a.TextContent.Trim() == "Introduction");
    }

    [Fact]
    public async Task GeenIntroductieVoorDeelnemers()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();

        var (_, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}", Ct);

        var page = Html.Parse(html);
        Assert.Null(page.QuerySelector("[data-testid=introduction]"));
        Assert.DoesNotContain(page.QuerySelectorAll("[data-testid=course-nav] a"), a => a.TextContent.Trim() == "Introduction");
    }

    [Fact]
    public async Task ConceptAlleenVoorBeheerders()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        await academic.IntroductionAsync(offering, "# Welcome", published: false);

        var (_, studentHtml) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}", Ct);
        var (_, docentHtml) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{offering}", Ct);

        Assert.Null(Html.Parse(studentHtml).QuerySelector("[data-testid=introduction]"));
        var intro = Html.Parse(docentHtml).QuerySelector("[data-testid=introduction]")!;
        Assert.Contains("Draft", intro.TextContent, StringComparison.Ordinal);
        Assert.Contains("Welcome", intro.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocentVoegtEenIntroductieToe()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();

        var (_, html) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{offering}", Ct);

        var add = Html.Parse(html).QuerySelector("[data-testid=introduction] [data-testid=edit-introduction]")!;
        Assert.Equal("Add introduction", add.TextContent.Trim());
        Assert.Equal($"/courses/{offering:D}/introduction", add.GetAttribute("href"));
    }
}

// Spec course-introduction, requirement "Docenten bewerken de introductie in Markdown" (page access).
public sealed class DocentenBewerkenDeIntroductieInMarkdown(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task StudentBewerktNiet()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();

        var (status, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}/introduction", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains(">Access denied</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocentOpentDeBewerkpagina()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();

        var (status, html) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{offering}/introduction", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Introduction · BIO101-2026-P1 · Doctrina", html, StringComparison.Ordinal);
    }
}

// Spec course-introduction, requirement "De introductie wordt veilig weergegeven".
public sealed class DeIntroductieWordtVeiligWeergegeven(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task ScriptsWordenNietUitgevoerd()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        await academic.IntroductionAsync(offering, "<script>alert(1)</script>\n\n[x](javascript:alert(1))");

        // The shared helper decodes HTML entities (for text matching), which would turn escaped text back into
        // markup; this test needs the response exactly as the browser gets it.
        using var browser = academic.Host.CreateBrowser();
        await browser.SignInAndGetAsync("/profile", factory.Idp, StubUser.Student, Ct);
        var html = await browser.GetStringAsync(new Uri($"/courses/{offering}", UriKind.Relative), Ct);

        var intro = Html.Parse(html).QuerySelector("[data-testid=introduction]")!;
        Assert.Null(intro.QuerySelector("script"));
        Assert.DoesNotContain(intro.QuerySelectorAll("a"), a => (a.GetAttribute("href") ?? string.Empty).StartsWith("javascript:", StringComparison.OrdinalIgnoreCase));
    }
}

// Spec course-introduction, requirement "Beheerders beheren introductiesjablonen" (access and menu).
public sealed class BeheerdersBeherenIntroductiesjablonen(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task DocentBeheertGeenSjablonen()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        await academic.OfferingWithPeopleAsync();

        var (status, html) = await academic.GetAsync(factory, GroupsSeed.Docent, "/content/templates", Ct);
        var (_, home) = await academic.GetAsync(factory, GroupsSeed.Docent, "/", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains(">Access denied</h1>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Intro templates", home, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BeheerderZietHetMenuEnDePagina()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        await academic.OfferingWithPeopleAsync();

        var (status, html) = await academic.GetAsync(factory, StubUser.Beheerder, "/content/templates", Ct);
        var (_, home) = await academic.GetAsync(factory, StubUser.Beheerder, "/", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Intro templates", html, StringComparison.Ordinal);
        Assert.Contains("Intro templates", home, StringComparison.Ordinal);
    }
}

// Spec course-introduction, requirement "Een uitvoering kiest een sjabloon dat de opmaak bepaalt". The absence of literal
// colours in the layouts is guarded for all styles by the architecture test LiteralColors.
public sealed class EenUitvoeringKiestEenSjabloonDatDeOpmaakBepaalt(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task OpmaakVanHetSjabloon()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var first = await academic.OfferingWithPeopleAsync();
        var second = await academic.QueryAsync(db => db.CourseOfferings.Where(o => o.ExternalId == "WIS201-2026-P1").Select(o => o.Id).SingleAsync());
        var banner = await academic.TemplateAsync("Course with banner", IntroductionLayout.Banner);
        await academic.IntroductionAsync(first, "Hello", templateId: banner);
        await academic.IntroductionAsync(second, "Hello", templateId: banner);

        var (_, firstHtml) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{first}", Ct);
        var (_, secondHtml) = await academic.GetAsync(factory, StubUser.Beheerder, $"/courses/{second}", Ct);

        Assert.Equal("banner", Html.Parse(firstHtml).QuerySelector("[data-testid=introduction]")!.GetAttribute("data-layout"));
        Assert.Equal("banner", Html.Parse(secondHtml).QuerySelector("[data-testid=introduction]")!.GetAttribute("data-layout"));
    }

    [Fact]
    public async Task VerwijderdSjabloon()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var offering = await academic.OfferingWithPeopleAsync();
        var banner = await academic.TemplateAsync("Course with banner", IntroductionLayout.Banner);
        await academic.IntroductionAsync(offering, "Hello", templateId: banner);
        await academic.DeleteTemplateAsync(banner);

        var (_, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}", Ct);

        var intro = Html.Parse(html).QuerySelector("[data-testid=introduction]")!;
        Assert.Equal("plain", intro.GetAttribute("data-layout"));
        Assert.Contains("Hello", intro.TextContent, StringComparison.Ordinal);
    }
}
