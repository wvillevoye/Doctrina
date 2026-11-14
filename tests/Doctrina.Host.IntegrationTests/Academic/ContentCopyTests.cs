using System.Net;
using System.Security.Claims;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.Host.IntegrationTests.HostUi;
using Doctrina.Infrastructure.Authentication;
using Doctrina.Modules.Content.Application;
using Doctrina.TestKit.Authentication;
using Microsoft.Extensions.DependencyInjection;

// Spec course-content of change add-course-templates; task 3.1, against the host with Academic and Content.
namespace Doctrina.Host.IntegrationTests.Academic;

public sealed class ContentKopierenNaarEenLegeUitvoeringOpHetWeb(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The 2026 offering (empty, the target) and an earlier offering of the same course with a page and a link; the lecturer teaches both.</summary>
    private static async Task<(Guid Target, Guid Source)> OfferingsAsync(AcademicHost academic)
    {
        var target = await academic.OfferingWithPeopleAsync();
        await academic.OfferingAsync("BIO101", "BIO101-2025-P1", "2025-P1");
        await academic.SeedAsync("associations", Seeds.Enrolment("A9", "P7000002", "BIO101-2025-P1", "lecturer"));
        var source = await academic.QueryAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            db.CourseOfferings.Where(o => o.ExternalId == "BIO101-2025-P1").Select(o => o.Id), Ct));
        var week = await academic.PublishedSectionAsync(source, "Week 1");
        await academic.PageAsync(week.Id, "Introduction", "# Welcome");
        await academic.LinkAsync(week.Id, "Course guide", "https://example.test/guide");
        return (target, source);
    }

    [Fact]
    public async Task DeKaartVerschijntAlleenAlsKopierenKan()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var (target, source) = await OfferingsAsync(academic);

        var (status, html) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{target}/content", Ct);
        var (_, sourceHtml) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{source}/content", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var card = Html.Parse(html).QuerySelector("[data-testid=copy-content]");
        Assert.NotNull(card);
        Assert.Contains("BIO101-2025-P1", card.TextContent, StringComparison.Ordinal);
        Assert.Null(Html.Parse(sourceHtml).QuerySelector("[data-testid=copy-content]")); // the source has content
    }

    [Fact]
    public async Task NaHetKopierenZienStudentenNietsEnDeDocentConcepten()
    {
        await using var academic = await ContentSeed.HostAsync(factory, Ct);
        var (target, source) = await OfferingsAsync(academic);
        var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(DoctrinaClaimTypes.Permission, "Content.Manage")], "test"));
        using (var scope = academic.Host.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ContentService>().CopyFromAsync(admin, target, source, Ct);
            Assert.True(result.IsSuccess, result.Error?.ToString());
        }

        var (_, student) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{target}", Ct);
        var (_, lecturer) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{target}/content", Ct);

        Assert.DoesNotContain("Week 1", student, StringComparison.Ordinal);
        var page = Html.Parse(lecturer);
        Assert.Equal(["Week 1"], page.QuerySelectorAll("[data-testid=section] h2").Select(h => h.TextContent.Trim()));
        Assert.Equal(["Introduction", "Course guide"], page.QuerySelectorAll("[data-testid=item-title]").Select(e => e.TextContent.Replace("(opens in a new window)", string.Empty, StringComparison.Ordinal).Trim()));
        Assert.Null(page.QuerySelector("[data-testid=copy-content]"));
    }
}
