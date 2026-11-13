using System.Net;
using Doctrina.Core.Results;
using System.Net.Http.Json;
using Doctrina.Host.IntegrationTests.ApiConventions;
using Doctrina.Host.IntegrationTests.HostUi;
using Doctrina.Modules.Groups.Application;
using Doctrina.Modules.Groups.Infrastructure;
using Doctrina.TestKit.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Host.IntegrationTests.Academic;

internal static class GroupsSeed
{
    public static StubUser Docent { get; } = new("sub-docent", "Dirk Docent", ["employee"], "docent@example.test", "P7000002");

    public static StubUser Student2 { get; } = new("sub-student2", "Sam Smit", ["student"], "sam@example.test", "I6000002");

    /// <summary>
    /// BIO101-2026-P1 with Dirk Docent (P7000002) as lecturer and the students I6000001, I6000002 and I6000003; also
    /// WIS201-2026-P1 with Wim Wiskunde (P7000003) as lecturer. Returns the id of BIO101-2026-P1.
    /// </summary>
    public static async Task<Guid> OfferingWithPeopleAsync(this AcademicHost academic)
    {
        await academic.OfferingAsync("BIO101", "BIO101-2026-P1");
        await academic.OfferingAsync("WIS201", "WIS201-2026-P1");
        await academic.SeedAsync("persons",
            Seeds.Person("I6000001", "Sanne", "Student"), Seeds.Person("I6000002", "Sam", "Smit"), Seeds.Person("I6000003", "Tess", "Bakker"),
            Seeds.Person("P7000002", "Dirk", "Docent"), Seeds.Person("P7000003", "Wim", "Wiskunde"));
        await academic.SeedAsync("associations",
            Seeds.Enrolment("A1", "I6000001", "BIO101-2026-P1", "student"),
            Seeds.Enrolment("A2", "I6000002", "BIO101-2026-P1", "student"),
            Seeds.Enrolment("A3", "I6000003", "BIO101-2026-P1", "student"),
            Seeds.Enrolment("A4", "P7000002", "BIO101-2026-P1", "lecturer"),
            Seeds.Enrolment("A5", "P7000003", "WIS201-2026-P1", "lecturer"));
        return await academic.QueryAsync(db => db.CourseOfferings.Where(o => o.ExternalId == "BIO101-2026-P1").Select(o => o.Id).SingleAsync());
    }

    public static async Task<Guid> PersonIdAsync(this AcademicHost academic, string externalId) =>
        await academic.QueryAsync(db => db.Persons.Where(p => p.ExternalId == externalId).Select(p => p.Id).SingleAsync());

    /// <summary>Runs a change through the Groups service, as the page would.</summary>
    public static async Task<T> GroupsAsync<T>(this AcademicHost academic, Func<GroupService, Task<Result<T>>> change)
    {
        using var scope = academic.Host.Services.CreateScope();
        var result = await change(scope.ServiceProvider.GetRequiredService<GroupService>());
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value!;
    }

    public static async Task GroupsAsync(this AcademicHost academic, Func<GroupService, Task<Result>> change)
    {
        using var scope = academic.Host.Services.CreateScope();
        var result = await change(scope.ServiceProvider.GetRequiredService<GroupService>());
        Assert.True(result.IsSuccess, result.Error?.ToString());
    }
}

// Spec groups, requirement "Studenten zien hun groepen op de pagina van de uitvoering".
public sealed class StudentenZienHunGroepenOpDePaginaVanDeUitvoering(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZichtbareEnVerborgenGroupSet()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, "Groups");
        var offering = await academic.OfferingWithPeopleAsync();
        var (sanne, sam, tess) = (await academic.PersonIdAsync("I6000001"), await academic.PersonIdAsync("I6000002"), await academic.PersonIdAsync("I6000003"));
        var visible = await academic.GroupsAsync(s => s.CreateSetAsync(offering, "Project teams", true, null, Ct));
        var teamA = await academic.GroupsAsync(s => s.CreateGroupAsync(visible.Id, "Team A", Ct));
        var hidden = await academic.GroupsAsync(s => s.CreateSetAsync(offering, "Lab groups", false, null, Ct));
        var lab2 = await academic.GroupsAsync(s => s.CreateGroupAsync(hidden.Id, "Lab 2", Ct));
        await academic.GroupsAsync(s => s.AssignAsync(teamA.Id, sanne, Ct));
        await academic.GroupsAsync(s => s.AssignAsync(teamA.Id, sam, Ct));
        await academic.GroupsAsync(s => s.AssignAsync(lab2.Id, sanne, Ct));
        await academic.GroupsAsync(s => s.AssignAsync(lab2.Id, tess, Ct));

        var (status, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var section = Html.Parse(html).QuerySelector("[data-testid=my-groups]")!;
        Assert.Equal(["Project teams"], section.QuerySelectorAll("dt").Select(d => d.TextContent));
        Assert.Equal("Team A", section.QuerySelector("dd strong")?.TextContent);
        Assert.Equal(["Sam Smit", "Sanne Student"], section.QuerySelectorAll("[data-testid=group-members] li").Select(l => l.TextContent));
        Assert.DoesNotContain("Lab", section.TextContent, StringComparison.Ordinal);
        Assert.Null(section.QuerySelector("[data-testid=manage-groups]"));
    }

    [Fact]
    public async Task DocentZietDeLinkNaarHetGroepsbeheer()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, "Groups");
        var offering = await academic.OfferingWithPeopleAsync();

        var (_, html) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{offering}", Ct);

        var link = Html.Parse(html).QuerySelector("[data-testid=manage-groups]");
        Assert.Equal($"/courses/{offering:D}/groups", link?.GetAttribute("href"));
        Assert.Equal("Manage groups", link?.TextContent.Trim());
    }

    [Fact]
    public async Task StudentZonderGroepen()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, "Groups");
        var offering = await academic.OfferingWithPeopleAsync();

        var (_, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}", Ct);

        Assert.Contains("You are not in any groups yet.", Html.Parse(html).QuerySelector("[data-testid=my-groups]")!.TextContent, StringComparison.Ordinal);
    }
}

// Spec groups, requirement "Alleen docenten van de uitvoering en beheerders beheren groepen".
public sealed class AlleenDocentenVanDeUitvoeringEnBeheerdersBeherenGroepen(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StudentMagGeenGroepenBeheren()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, "Groups");
        var offering = await academic.OfferingWithPeopleAsync();

        var (status, html) = await academic.GetAsync(factory, StubUser.Student, $"/courses/{offering}/groups", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains(">Access denied</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocentVanEenAndereUitvoering()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, "Groups");
        var offering = await academic.OfferingWithPeopleAsync();
        var wim = new StubUser("sub-wim", "Wim Wiskunde", ["employee"], PersonId: "P7000003");

        var (status, _) = await academic.GetAsync(factory, wim, $"/courses/{offering}/groups", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task BeheerderZonderInschrijving()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, "Groups");
        var offering = await academic.OfferingWithPeopleAsync();

        var (status, html) = await academic.GetAsync(factory, StubUser.Beheerder, $"/courses/{offering}/groups", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Groups · BIO101-2026-P1 · Doctrina", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocentVanDeUitvoering()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, "Groups");
        var offering = await academic.OfferingWithPeopleAsync();

        var (status, html) = await academic.GetAsync(factory, GroupsSeed.Docent, $"/courses/{offering}/groups", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("3 student(s)", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnbekendeUitvoering()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, "Groups");

        var (status, _) = await academic.GetAsync(factory, StubUser.Beheerder, $"/courses/{Guid.Empty}/groups", Ct);

        Assert.Equal(HttpStatusCode.NotFound, status);
    }
}

// Spec groups-api, requirement "Groepen zijn via de API te lezen".
public sealed class GroepenZijnViaDeApiTeLezen(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GroepenVanEenUitvoering()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, "Groups");
        var offering = await academic.OfferingWithPeopleAsync();
        var set = await academic.GroupsAsync(s => s.CreateSetAsync(offering, "Project teams", false, null, Ct));
        var teamA = await academic.GroupsAsync(s => s.CreateGroupAsync(set.Id, "Team A", Ct));
        var (sanne, sam) = (await academic.PersonIdAsync("I6000001"), await academic.PersonIdAsync("I6000002"));
        await academic.GroupsAsync(s => s.AssignAsync(teamA.Id, sam, Ct));
        await academic.GroupsAsync(s => s.AssignAsync(teamA.Id, sanne, Ct));
        using var api = academic.Api(factory, roles: "test.read");

        using var response = await api.GetAsync(Json.Url($"/api/v1/groups?offeringId={offering}"), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var group = Assert.Single((await response.ReadAsync(Ct)).EnumerateArray());
        Assert.Equal(("Team A", "Project teams", "BIO101-2026-P1"),
            (group.GetProperty("name").GetString(), group.GetProperty("groupSetName").GetString(), group.GetProperty("offeringId").GetString()));
        Assert.Equal(["I6000001", "I6000002"], group.GetProperty("members").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(teamA.Id.ToString("D"), group.GetProperty("groupId").GetString());
    }

    [Fact]
    public async Task VerwijderdeGroep()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, "Groups");
        var offering = await academic.OfferingWithPeopleAsync();
        var set = await academic.GroupsAsync(s => s.CreateSetAsync(offering, "Project teams", true, null, Ct));
        var teamA = await academic.GroupsAsync(s => s.CreateGroupAsync(set.Id, "Team A", Ct));
        await academic.GroupsAsync(s => s.DeleteGroupAsync(teamA.Id, Ct));
        using var api = academic.Api(factory, roles: "test.read");

        using var response = await api.GetAsync(Json.Url($"/api/v1/groups/{teamA.Id}"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Groups.Group.NotFound", (await response.ProblemAsync(Ct)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ZonderGroupsRead()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, "Groups");
        var offering = await academic.OfferingWithPeopleAsync();
        using var api = academic.Api(factory, roles: "test.write");

        using var response = await api.GetAsync(Json.Url($"/api/v1/groups?offeringId={offering}"), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ZonderOfferingId()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, "Groups");
        using var api = academic.Api(factory, roles: "test.read");

        using var missing = await api.GetAsync(Json.Url("/api/v1/groups"), Ct);
        using var unknown = await api.GetAsync(Json.Url($"/api/v1/groups?offeringId={Guid.Empty}"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("Groups.Api.OfferingRequired", (await missing.ProblemAsync(Ct)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task GeenSchrijvendeEndpoints()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct, "Groups");
        using var api = academic.Api(factory);

        using var post = await api.PostAsJsonAsync(Json.Url("/api/v1/groups"), new { name = "X" }, Ct);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }
}
