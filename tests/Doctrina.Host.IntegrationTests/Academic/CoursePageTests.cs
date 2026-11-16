using System.Net;
using Doctrina.Host.IntegrationTests.HostUi;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Modules;
using Doctrina.TestKit.Modules.Pages;
using Microsoft.EntityFrameworkCore;
using static Doctrina.Host.IntegrationTests.Academic.AcademicHost;

namespace Doctrina.Host.IntegrationTests.Academic;

internal static class Offerings
{
    /// <summary>BIO101-2026-P1 with Sanne Student (I6000001) as student; returns the offering id.</summary>
    public static async Task<Guid> WithStudentAsync(this AcademicHost academic, string role = "student", string status = "active")
    {
        await academic.OfferingAsync();
        await academic.SeedAsync("persons", Seeds.Person("I6000001", "Sanne", "Student"));
        await academic.SeedAsync("associations", Seeds.Enrolment("A1", "I6000001", "BIO101-2026-P1", role, status));
        return await academic.QueryAsync(db => db.CourseOfferings.Where(o => o.ExternalId == "BIO101-2026-P1").Select(o => o.Id).SingleAsync());
    }
}

// Spec course-page, requirement "Elke uitvoering heeft een pagina voor haar deelnemers".
public sealed class ElkeUitvoeringHeeftEenPaginaVoorHaarDeelnemers(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StudentOpentDePaginaVanZijnUitvoering()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        var id = await academic.WithStudentAsync();

        var (status, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{id}", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var page = Html.Parse(html);
        Assert.Equal("BIO101 course", page.QuerySelector("main h1")?.TextContent);
        Assert.Contains("BIO101-2026-P1", page.QuerySelector("main")!.TextContent, StringComparison.Ordinal);
        Assert.Contains("2026-P1", page.QuerySelector("[data-testid=offering-details]")!.TextContent, StringComparison.Ordinal);
        Assert.Equal("Student", page.QuerySelector("[data-testid=my-role]")?.TextContent);
    }

    [Fact]
    public async Task NietIngeschreven()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        var id = await academic.WithStudentAsync();

        var (status, html) = await academic.GetAsync(factory, StubUser.Student with { PersonId = "I6999999", Subject = "sub-other" }, $"/courses/{id}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains(">Access denied</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UitvoeringOpTeVerwijderen()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        var id = await academic.WithStudentAsync();
        await academic.SeedAsync("course-offerings", Record(
            ("offeringId", "BIO101-2026-P1"), ("name", "BIO101-2026-P1"), ("courseId", "BIO101"), ("academicSessionId", "2026-P1"), ("status", "toBeDeleted")));

        var (status, _) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{id}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task BeheerderZonderInschrijving()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        var id = await academic.WithStudentAsync();

        var (status, html) = await academic.GetAsync(factory, StubUser.Beheerder, $"/courses/{id}", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("–", Html.Parse(html).QuerySelector("[data-testid=my-role]")?.TextContent);
    }
}

// Spec course-page, requirement "Modules voegen secties toe aan de pagina van een uitvoering".
public sealed class ModulesVoegenSectiesToeAanDePaginaVanEenUitvoering(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SectieVanEenIngeschakeldeModule()
    {
        var sections = new TestModule("Sections", "Academic") { Services = s => s.AddOfferingPageSection<TestOfferingSection>(50) };
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, sections);
        var id = await academic.WithStudentAsync();

        var (_, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{id}", Ct);

        Assert.Equal($"Test section {id}", Html.Parse(html).QuerySelector("[data-testid=test-section]")?.TextContent);
    }

    [Fact]
    public async Task SectieVanEenUitgeschakeldeModuleOntbreekt()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        var id = await academic.WithStudentAsync();

        var (_, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{id}", Ct);

        Assert.Null(Html.Parse(html).QuerySelector("[data-testid=test-section]"));
    }
}
