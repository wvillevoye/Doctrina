using System.Net;
using Doctrina.Host.IntegrationTests.HostUi;
using Doctrina.TestKit.Authentication;
using Microsoft.EntityFrameworkCore;
using static Doctrina.Host.IntegrationTests.Academic.AcademicHost;

namespace Doctrina.Host.IntegrationTests.Academic;

internal static class Seeds
{
    public static Dictionary<string, string?> Person(string id, string given, string surname) =>
        Record(("personId", id), ("givenName", given), ("surname", surname));

    public static async Task OfferingAsync(this AcademicHost academic, string course = "BIO101", string offering = "BIO101-2026-P1", string session = "2026-P1")
    {
        await academic.SeedAsync("academic-sessions", Record(
            ("academicSessionId", session), ("name", session), ("academicSessionType", "period"), ("startDate", "2026-09-01"), ("endDate", "2026-10-31")));
        await academic.SeedAsync("courses", Record(("courseId", course), ("name", course + " course")));
        await academic.SeedAsync("course-offerings", Record(("offeringId", offering), ("name", offering), ("courseId", course), ("academicSessionId", session)));
    }

    public static Dictionary<string, string?> Enrolment(string id, string person, string offering, string role, string status = "active") =>
        Record(("associationId", id), ("personId", person), ("offeringId", offering), ("role", role), ("status", status));
}

// Spec academic-admin, requirement "Beheerders zien per type een lijst met zoeken".
public sealed class BeheerdersZienPerTypeEenLijstMetZoeken(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZoekenOpExternalId()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.SeedAsync("persons", Seeds.Person("I6000001", "Sanne", "Student"), Seeds.Person("P7000002", "Dirk", "Docent"));

        var (status, html) = await academic.GetAsync(factory, StubUser.Beheerder, "/academic/persons?q=I6000001", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var rows = Html.Parse(html).QuerySelectorAll("[data-testid=academic-list] tbody tr");
        Assert.Equal("I6000001", Assert.Single(rows).Children[1].TextContent);
        Assert.Contains("Active", rows[0].Children[3].TextContent, StringComparison.Ordinal);
        Assert.Equal("SIS", rows[0].Children[4].TextContent);
    }

    [Fact]
    public async Task Paginering()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.SeedAsync("courses", [.. Enumerable.Range(1, 60).Select(i => Record(("courseId", $"C{i:00}"), ("name", $"Course {i:00}")))]);

        var (_, first) = await academic.GetAsync(factory, StubUser.Beheerder, "/academic/courses", Ct);
        var (_, last) = await academic.GetAsync(factory, StubUser.Beheerder, "/academic/courses?page=3", Ct);

        var page1 = Html.Parse(first);
        Assert.Equal(25, page1.QuerySelectorAll("[data-testid=academic-list] tbody tr").Length);
        Assert.Contains("Page 1 of 3", page1.QuerySelector("[data-testid=pager]")!.TextContent, StringComparison.Ordinal);
        Assert.Equal("/academic/courses?page=2", page1.QuerySelector("[data-testid=pager] a")!.GetAttribute("href"));
        var page3 = Html.Parse(last);
        Assert.Equal(10, page3.QuerySelectorAll("[data-testid=academic-list] tbody tr").Length);
        Assert.Equal("Course 51", page3.QuerySelector("[data-testid=academic-list] tbody tr td")!.TextContent);
    }

    [Fact]
    public async Task GeenPermissie()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);

        var (status, html) = await academic.GetAsync(factory, StubUser.Student, "/academic/courses", Ct);
        var (_, profile) = await academic.GetAsync(factory, StubUser.Student, "/profile", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains(">Access denied</h1>", html, StringComparison.Ordinal);
        Assert.Null(Html.Parse(profile).QuerySelector("nav a[href='/academic']"));
    }

    [Fact]
    public async Task MenuItemVoorBeheerders()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);

        var (_, profile) = await academic.GetAsync(factory, StubUser.Beheerder, "/profile", Ct);

        Assert.Equal("Academic", Html.Parse(profile).QuerySelector("nav a[href='/academic']")?.TextContent);
    }
}

// Spec academic-admin, requirement "Een detailpagina toont een record met zijn verbanden".
public sealed class EenDetailpaginaToontEenRecordMetZijnVerbanden(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UitvoeringMetInschrijvingen()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.OfferingAsync();
        await academic.SeedAsync("persons", Seeds.Person("I6000001", "Sanne", "Student"), Seeds.Person("I6000002", "Sam", "Smit"), Seeds.Person("P7000002", "Dirk", "Docent"));
        await academic.SeedAsync("associations",
            Seeds.Enrolment("A1", "I6000001", "BIO101-2026-P1", "student"),
            Seeds.Enrolment("A2", "I6000002", "BIO101-2026-P1", "student"),
            Seeds.Enrolment("A3", "P7000002", "BIO101-2026-P1", "lecturer"));
        var id = await academic.QueryAsync(db => db.CourseOfferings.Select(o => o.Id).SingleAsync(Ct));

        var (status, html) = await academic.GetAsync(factory, StubUser.Beheerder, $"/academic/course-offerings/{id}", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var page = Html.Parse(html);
        var details = page.QuerySelector("[data-testid=details]")!;
        Assert.NotNull(details.QuerySelector("a[href^='/academic/courses/']"));
        Assert.NotNull(details.QuerySelector("a[href^='/academic/academic-sessions/']"));
        Assert.Equal(
            [("Dirk Docent", "Lecturer"), ("Sam Smit", "Student"), ("Sanne Student", "Student")],
            page.QuerySelectorAll("[data-testid=enrolments] tbody tr").Select(r => (r.Children[0].TextContent, r.Children[1].TextContent)).Order());
    }
}

// Spec academic-admin, requirement "De beheerschermen zijn alleen-lezen".
public sealed class DeBeheerschermenZijnAlleenLezen(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GeenBewerkknoppen()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.SeedAsync("persons", Seeds.Person("I6000001", "Sanne", "Student"));
        var id = await academic.QueryAsync(db => db.Persons.Select(p => p.Id).SingleAsync(Ct));

        var (status, html) = await academic.GetAsync(factory, StubUser.Beheerder, $"/academic/persons/{id}", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var main = Html.Parse(html).QuerySelector("main")!;
        Assert.Contains("Sanne Student", main.QuerySelector("h1")!.TextContent, StringComparison.Ordinal);
        Assert.Empty(main.QuerySelectorAll("form, button, input, select, textarea"));
    }
}
