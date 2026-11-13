using System.Net;
using Doctrina.Host.IntegrationTests.HostUi;
using Doctrina.TestKit.Authentication;
using Microsoft.EntityFrameworkCore;
using static Doctrina.Host.IntegrationTests.Academic.AcademicHost;

namespace Doctrina.Host.IntegrationTests.Academic;

internal static class MyCoursesPage
{
    public static async Task<AngleSharp.Dom.IDocument> OpenAsync(this AcademicHost academic, DoctrinaHostFactory factory, StubUser user, CancellationToken ct)
    {
        var (status, html) = await academic.GetAsync(factory, user, "/my-courses", ct);
        Assert.Equal(HttpStatusCode.OK, status);
        return Html.Parse(html);
    }

    public static IEnumerable<(string Course, string Offering, string Role)> Rows(this AngleSharp.Dom.IDocument page) =>
        page.QuerySelectorAll("[data-testid=my-courses] tbody tr").Select(r => (r.Children[0].TextContent, r.Children[1].TextContent, r.Children[3].TextContent));
}

// Spec person-linking, requirement "Een login wordt via personId aan een persoon gekoppeld".
public sealed class EenLoginWordtViaPersonIdAanEenPersoonGekoppeld(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EersteGebruikDoorEenGeimporteerdePersoon()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.SeedAsync("persons", Seeds.Person("I6000001", "Sanne", "Student"));

        var page = await academic.OpenAsync(factory, StubUser.Student, Ct);

        Assert.Null(page.QuerySelector("[data-testid=no-person]"));
        var login = await academic.QueryAsync(db => db.PersonLogins.Join(db.Persons, l => l.PersonId, p => p.Id, (l, p) => new { p.ExternalId, l.Issuer, l.Oid }).SingleAsync(Ct));
        Assert.Equal(("I6000001", StubOidcProvider.Issuer, StubUser.Student.Subject), (login.ExternalId, login.Issuer, login.Oid));
    }

    [Fact]
    public async Task VolgendeLoginZonderPersonId()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.OfferingAsync();
        await academic.SeedAsync("persons", Seeds.Person("I6000001", "Sanne", "Student"));
        await academic.SeedAsync("associations", Seeds.Enrolment("A1", "I6000001", "BIO101-2026-P1", "student"));
        await academic.OpenAsync(factory, StubUser.Student, Ct);

        var page = await academic.OpenAsync(factory, StubUser.Student with { PersonId = null }, Ct);

        Assert.Equal([("BIO101 course", "BIO101-2026-P1", "Student")], page.Rows());
    }
}

// Spec person-linking, requirement "Inloggen maakt nooit een persoon aan".
public sealed class InloggenMaaktNooitEenPersoonAan(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LoginZonderBijbehorendePersoon()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);

        var page = await academic.OpenAsync(factory, StubUser.Student with { PersonId = "I6999999" }, Ct);

        Assert.Contains("We can't find your details in Doctrina yet", page.QuerySelector("[data-testid=no-person]")!.TextContent, StringComparison.Ordinal);
        Assert.Equal(0, await academic.QueryAsync(db => db.Persons.CountAsync(Ct)));
        Assert.Equal(0, await academic.QueryAsync(db => db.PersonLogins.CountAsync(Ct)));
    }

    [Fact]
    public async Task PersoonOpTeVerwijderen()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        var person = Seeds.Person("I6000001", "Sanne", "Student");
        person["status"] = "toBeDeleted";
        await academic.SeedAsync("persons", person);

        var page = await academic.OpenAsync(factory, StubUser.Student, Ct);

        Assert.NotNull(page.QuerySelector("[data-testid=no-person]"));
        Assert.Equal(0, await academic.QueryAsync(db => db.PersonLogins.CountAsync(Ct)));
    }
}

// Spec my-courses, requirement ""My courses" toont de eigen actieve uitvoeringen".
public sealed class MyCoursesToontDeEigenActieveUitvoeringen(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StudentMetTweeUitvoeringen()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.OfferingAsync("BIO101", "BIO101-2026-P1");
        await academic.OfferingAsync("WIS201", "WIS201-2026-P1");
        await academic.SeedAsync("persons", Seeds.Person("I6000001", "Sanne", "Student"));
        await academic.SeedAsync("associations",
            Seeds.Enrolment("A1", "I6000001", "BIO101-2026-P1", "student"),
            Seeds.Enrolment("A2", "I6000001", "WIS201-2026-P1", "teaching-assistant"));

        var page = await academic.OpenAsync(factory, StubUser.Student, Ct);

        Assert.Equal("My courses · Doctrina", page.Title);
        Assert.Equal(
            [("BIO101 course", "BIO101-2026-P1", "Student"), ("WIS201 course", "WIS201-2026-P1", "Teaching assistant")],
            page.Rows());
    }

    [Fact]
    public async Task InschrijvingenEnUitvoeringenDieNietMeetellen()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.OfferingAsync("BIO101", "BIO101-2026-P1");
        await academic.OfferingAsync("WIS201", "WIS201-2026-P1");
        await academic.SeedAsync("course-offerings", Record(
            ("offeringId", "WIS201-2026-P1"), ("name", "WIS201-2026-P1"), ("courseId", "WIS201"), ("academicSessionId", "2026-P1"), ("status", "toBeDeleted")));
        await academic.SeedAsync("persons", Seeds.Person("I6000001", "Sanne", "Student"));
        await academic.SeedAsync("associations",
            Seeds.Enrolment("A1", "I6000001", "BIO101-2026-P1", "student", status: "toBeDeleted"),
            Seeds.Enrolment("A2", "I6000001", "WIS201-2026-P1", "student"));

        var page = await academic.OpenAsync(factory, StubUser.Student, Ct);

        Assert.Empty(page.Rows());
        Assert.NotNull(page.QuerySelector("[data-testid=no-courses]"));
    }

    [Fact]
    public async Task GeenInschrijvingen()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.SeedAsync("persons", Seeds.Person("I6000001", "Sanne", "Student"));

        var page = await academic.OpenAsync(factory, StubUser.Student, Ct);

        Assert.Equal("You are not enrolled in any courses.", page.QuerySelector("[data-testid=no-courses] p")!.TextContent.Trim());
    }

    [Fact]
    public async Task MenuItemVoorStudenten()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);

        var (_, html) = await academic.GetAsync(factory, StubUser.Student, "/profile", Ct);

        Assert.Equal("My courses", Html.Parse(html).QuerySelector("nav a[href='/my-courses']")?.TextContent);
    }

    [Fact]
    public async Task AnoniemeBezoeker()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var browser = academic.Host.CreateBrowser();

        using var anonymous = await browser.GetAsync(new Uri("/my-courses", UriKind.Relative), Ct);
        var page = Html.Parse(await browser.SignInAndGetAsync("/my-courses", factory.Idp, StubUser.Student, Ct));

        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Equal(factory.Idp.Configuration.AuthorizationEndpoint, anonymous.Headers.Location!.GetLeftPart(UriPartial.Path));
        Assert.Equal("My courses · Doctrina", page.Title); // back on /my-courses after signing in
    }
}
