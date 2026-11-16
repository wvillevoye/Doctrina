using System.Globalization;
using System.Resources;
using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Groups.Application;
using Doctrina.Modules.Groups.Contracts;
using Doctrina.Modules.Groups.Domain;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Groups.Tests;

/// <summary>A database, a fake directory and a service per test.</summary>
internal sealed class Fixture : IAsyncDisposable
{
    private Fixture(GroupsDatabase db) => Db = db;

    public GroupsDatabase Db { get; }

    public FakeDirectory Directory { get; } = new();

    public static async Task<Fixture> CreateAsync() => new(await GroupsDatabase.CreateAsync(TestContext.Current.CancellationToken));

    /// <summary>A service with a fresh context, like a new request.</summary>
    public GroupService Service() => new(Db.CreateContext(), Directory);

    public static async Task<T> Ok<T>(Task<Result<T>> call)
    {
        var result = await call;
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value!;
    }

    public static async Task Ok(Task<Result> call)
    {
        var result = await call;
        Assert.True(result.IsSuccess, result.Error?.ToString());
    }

    public async Task<GroupSet> SetAsync(string name = "Project teams", int? max = null, params string[] groups)
    {
        var ct = TestContext.Current.CancellationToken;
        var set = await Ok(Service().CreateSetAsync(Directory.OfferingId, name, visibleToStudents: true, max, ct));
        foreach (var group in groups)
        {
            await Ok(Service().CreateGroupAsync(set.Id, group, ct));
        }

        return set;
    }

    /// <summary>Group name → member person ids, for active groups of the set.</summary>
    public async Task<Dictionary<string, List<Guid>>> GroupsOfAsync(Guid setId) =>
        (await Service().SetsAsync(Directory.OfferingId, TestContext.Current.CancellationToken)).Single(s => s.Id == setId).Groups
            .ToDictionary(g => g.Name, g => g.Members.Select(m => m.PersonId).ToList());

    public async Task<Guid> GroupIdAsync(Guid setId, string name)
    {
        await using var context = Db.CreateContext();
        return await context.Groups.Where(g => g.GroupSetId == setId && g.Name == name).Select(g => g.Id).SingleAsync();
    }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}

// Spec groups, requirement "Docenten beheren GroupSets en groepen in hun uitvoering".
public sealed class DocentenBeherenGroupSetsEnGroepenInHunUitvoering
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DocentMaaktEenGroupSetMetGroepen()
    {
        await using var f = await Fixture.CreateAsync();

        var set = await f.SetAsync("Project teams", null, "Team A", "Team B");

        var groups = await f.GroupsOfAsync(set.Id);
        Assert.Equal(["Team A", "Team B"], groups.Keys);
        Assert.All(groups.Values, Assert.Empty);
        await using var context = f.Db.CreateContext();
        var stored = await context.GroupSets.SingleAsync(Ct);
        Assert.Equal((stored.Id.ToString("D"), "Doctrina"), (stored.ExternalId, stored.Source));
        Assert.All(await context.Groups.ToListAsync(Ct), g => Assert.Equal(g.Id.ToString("D"), g.ExternalId));
    }

    [Fact]
    public async Task GroepMetLedenVerwijderen()
    {
        await using var f = await Fixture.CreateAsync();
        var students = f.Directory.AddStudents(2);
        var set = await f.SetAsync("Project teams", null, "Team A", "Team B");
        var teamA = await f.GroupIdAsync(set.Id, "Team A");
        await Fixture.Ok(f.Service().AssignAsync(teamA, students[0].PersonId, Ct));
        await Fixture.Ok(f.Service().AssignAsync(teamA, students[1].PersonId, Ct));

        await Fixture.Ok(f.Service().DeleteGroupAsync(teamA, Ct));

        Assert.Equal(["Team B"], (await f.GroupsOfAsync(set.Id)).Keys);
        await using var context = f.Db.CreateContext();
        Assert.Equal(EntityStatus.ToBeDeleted, (await context.Groups.SingleAsync(g => g.Id == teamA, Ct)).Status);
        Assert.Equal(0, await context.GroupMembers.CountAsync(Ct));
    }

    [Fact]
    public async Task GroupSetVerwijderen()
    {
        await using var f = await Fixture.CreateAsync();
        var student = f.Directory.Add("I6000001");
        var set = await f.SetAsync("Project teams", null, "Team A");
        await Fixture.Ok(f.Service().AssignAsync(await f.GroupIdAsync(set.Id, "Team A"), student.PersonId, Ct));

        await Fixture.Ok(f.Service().DeleteSetAsync(set.Id, Ct));

        Assert.Empty(await f.Service().SetsAsync(f.Directory.OfferingId, Ct));
        await using var context = f.Db.CreateContext();
        Assert.Equal(0, await context.GroupMembers.CountAsync(Ct));
    }

    [Fact]
    public async Task OngeldigeInvoer()
    {
        await using var f = await Fixture.CreateAsync();

        var noName = await f.Service().CreateSetAsync(f.Directory.OfferingId, " ", true, null, Ct);
        var zeroMax = await f.Service().CreateSetAsync(f.Directory.OfferingId, "Teams", true, 0, Ct);
        var unknownOffering = await f.Service().CreateSetAsync(Guid.Empty, "Teams", true, null, Ct);

        Assert.Equal((GroupsErrors.SetInvalid, "name"), (noName.Error!.Code, noName.Error.Details!.Keys.Single()));
        Assert.Equal("maxGroupSize", zeroMax.Error!.Details!.Keys.Single());
        Assert.Equal(ErrorType.NotFound, unknownOffering.Error!.Type);
    }
}

// Spec groups, requirement "Studenten worden handmatig ingedeeld".
public sealed class StudentenWordenHandmatigIngedeeld
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StudentToevoegen()
    {
        await using var f = await Fixture.CreateAsync();
        var student = f.Directory.Add("I6000001");
        var set = await f.SetAsync("Project teams", null, "Team A", "Team B");

        await Fixture.Ok(f.Service().AssignAsync(await f.GroupIdAsync(set.Id, "Team A"), student.PersonId, Ct));

        Assert.Equal([student.PersonId], (await f.GroupsOfAsync(set.Id))["Team A"]);
    }

    [Fact]
    public async Task StudentVerplaatsen()
    {
        await using var f = await Fixture.CreateAsync();
        var student = f.Directory.Add("I6000001");
        var set = await f.SetAsync("Project teams", null, "Team A", "Team B");
        await Fixture.Ok(f.Service().AssignAsync(await f.GroupIdAsync(set.Id, "Team A"), student.PersonId, Ct));

        await Fixture.Ok(f.Service().AssignAsync(await f.GroupIdAsync(set.Id, "Team B"), student.PersonId, Ct));

        var groups = await f.GroupsOfAsync(set.Id);
        Assert.Empty(groups["Team A"]);
        Assert.Equal([student.PersonId], groups["Team B"]);
    }

    [Fact]
    public async Task GroepIsVol()
    {
        await using var f = await Fixture.CreateAsync();
        var students = f.Directory.AddStudents(3);
        var set = await f.SetAsync("Project teams", 2, "Team A");
        var teamA = await f.GroupIdAsync(set.Id, "Team A");
        await Fixture.Ok(f.Service().AssignAsync(teamA, students[0].PersonId, Ct));
        await Fixture.Ok(f.Service().AssignAsync(teamA, students[1].PersonId, Ct));

        var third = await f.Service().AssignAsync(teamA, students[2].PersonId, Ct);

        Assert.Equal(GroupsErrors.GroupFull, third.Error!.Code);
        Assert.Contains("full", third.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, (await f.GroupsOfAsync(set.Id))["Team A"].Count);
    }

    [Fact]
    public async Task PersoonZonderStudentinschrijving()
    {
        await using var f = await Fixture.CreateAsync();
        var lecturer = f.Directory.Add("P7000002", OfferingRole.Lecturer);
        var set = await f.SetAsync("Project teams", null, "Team A");

        var result = await f.Service().AssignAsync(await f.GroupIdAsync(set.Id, "Team A"), lecturer.PersonId, Ct);
        var outsider = await f.Service().AssignAsync(await f.GroupIdAsync(set.Id, "Team A"), Guid.Parse("33333333-0000-0000-0000-000000000001"), Ct);

        Assert.Equal(GroupsErrors.NotAStudent, result.Error!.Code);
        Assert.Equal(GroupsErrors.NotAStudent, outsider.Error!.Code);
        Assert.Empty((await f.GroupsOfAsync(set.Id))["Team A"]);
    }

    [Fact]
    public async Task StudentUitEenGroepHalen()
    {
        await using var f = await Fixture.CreateAsync();
        var student = f.Directory.Add("I6000001");
        var set = await f.SetAsync("Project teams", null, "Team A");
        await Fixture.Ok(f.Service().AssignAsync(await f.GroupIdAsync(set.Id, "Team A"), student.PersonId, Ct));

        await Fixture.Ok(f.Service().UnassignAsync(set.Id, student.PersonId, Ct));

        Assert.Empty((await f.GroupsOfAsync(set.Id))["Team A"]);
    }
}

// Spec groups, requirement "Wijzigingen in groepslidmaatschap worden als event gepubliceerd".
public sealed class WijzigingenInGroepslidmaatschapWordenAlsEventGepubliceerd
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task VerplaatsenGeeftTweeEvents()
    {
        await using var f = await Fixture.CreateAsync();
        var student = f.Directory.Add("I6000001");
        var set = await f.SetAsync("Project teams", null, "Team A", "Team B");
        var (teamA, teamB) = (await f.GroupIdAsync(set.Id, "Team A"), await f.GroupIdAsync(set.Id, "Team B"));
        await Fixture.Ok(f.Service().AssignAsync(teamA, student.PersonId, Ct));
        f.Db.Events.Published.Clear();

        await Fixture.Ok(f.Service().AssignAsync(teamB, student.PersonId, Ct));

        Assert.Equal(
            [
                new GroupMembershipChanged(f.Directory.OfferingId, set.Id, teamA, student.PersonId, MembershipChange.Removed),
                new GroupMembershipChanged(f.Directory.OfferingId, set.Id, teamB, student.PersonId, MembershipChange.Added),
            ],
            f.Db.Events.Published);
    }

    [Fact]
    public async Task VerwijderenVanEenGroepGeeftEventsVoorDeLeden()
    {
        await using var f = await Fixture.CreateAsync();
        var student = f.Directory.Add("I6000001");
        var set = await f.SetAsync("Project teams", null, "Team A");
        var teamA = await f.GroupIdAsync(set.Id, "Team A");
        await Fixture.Ok(f.Service().AssignAsync(teamA, student.PersonId, Ct));
        f.Db.Events.Published.Clear();

        await Fixture.Ok(f.Service().DeleteGroupAsync(teamA, Ct));

        Assert.Equal([new GroupMembershipChanged(f.Directory.OfferingId, set.Id, teamA, student.PersonId, MembershipChange.Removed)], f.Db.Events.Published);
    }
}

// Spec groups, requirement "Studenten worden willekeurig verdeeld".
public sealed class StudentenWordenWillekeurigVerdeeld
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task VerdelenOverDrieGroepen()
    {
        await using var f = await Fixture.CreateAsync();
        var students = f.Directory.AddStudents(10);
        f.Directory.Add("P7000002", OfferingRole.Lecturer);
        var set = await f.SetAsync();

        Assert.Equal(3, await Fixture.Ok(f.Service().DistributeAsync(set.Id, 3, null, Ct)));

        var groups = await f.GroupsOfAsync(set.Id);
        Assert.Equal(["Group 1", "Group 2", "Group 3"], groups.Keys);
        Assert.Equal([3, 3, 4], groups.Values.Select(g => g.Count).Order());
        Assert.Equal(students.Select(s => s.PersonId).Order(), groups.Values.SelectMany(g => g).Order());
    }

    [Fact]
    public async Task GroepenVanVier()
    {
        await using var f = await Fixture.CreateAsync();
        f.Directory.AddStudents(10);
        var set = await f.SetAsync();

        await Fixture.Ok(f.Service().DistributeAsync(set.Id, null, 4, Ct));

        Assert.Equal([3, 3, 4], (await f.GroupsOfAsync(set.Id)).Values.Select(g => g.Count).Order());
    }

    [Fact]
    public async Task AlIngedeeldeStudentenBlijven()
    {
        await using var f = await Fixture.CreateAsync();
        var students = f.Directory.AddStudents(10);
        var set = await f.SetAsync("Project teams", null, "Group 1");
        var group1 = await f.GroupIdAsync(set.Id, "Group 1");
        await Fixture.Ok(f.Service().AssignAsync(group1, students[0].PersonId, Ct));
        await Fixture.Ok(f.Service().AssignAsync(group1, students[1].PersonId, Ct));

        await Fixture.Ok(f.Service().DistributeAsync(set.Id, 2, null, Ct));

        var groups = await f.GroupsOfAsync(set.Id);
        Assert.Equal(["Group 1", "Group 2", "Group 3"], groups.Keys);
        Assert.Equal([students[0].PersonId, students[1].PersonId], groups["Group 1"].Order());
        Assert.Equal((4, 4), (groups["Group 2"].Count, groups["Group 3"].Count));
    }

    [Fact]
    public async Task VerdelenBovenDeMaximaleGroepsgrootteWordtGeweigerd()
    {
        await using var f = await Fixture.CreateAsync();
        f.Directory.AddStudents(10);
        var set = await f.SetAsync("Project teams", 3);

        var result = await f.Service().DistributeAsync(set.Id, 3, null, Ct);

        Assert.Equal(GroupsErrors.GroupFull, result.Error!.Code);
        Assert.Empty(await f.GroupsOfAsync(set.Id));
    }

    [Fact]
    public async Task OngeldigeVerdeling()
    {
        await using var f = await Fixture.CreateAsync();
        f.Directory.AddStudents(2);
        var set = await f.SetAsync();

        var both = await f.Service().DistributeAsync(set.Id, 2, 2, Ct);
        var zero = await f.Service().DistributeAsync(set.Id, 0, null, Ct);
        await Fixture.Ok(f.Service().DistributeAsync(set.Id, 1, null, Ct));
        var nobodyLeft = await f.Service().DistributeAsync(set.Id, 1, null, Ct);

        Assert.All([both, zero, nobodyLeft], r => Assert.Equal(GroupsErrors.DistributionInvalid, r.Error!.Code));
    }
}

// Task 3.4 (design D5): who may manage the groups of an offering.
public sealed class GroupsAccessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(OfferingRole.Lecturer, true)]
    [InlineData(OfferingRole.Coordinator, true)]
    [InlineData(OfferingRole.TeachingAssistant, false)]
    [InlineData(OfferingRole.Student, false)]
    public async Task RolInDeUitvoering(OfferingRole role, bool expected)
    {
        var directory = new FakeDirectory();
        var person = directory.Add("X1", role);

        Assert.Equal(expected, await new GroupsAccess(directory).CanManageAsync(FakeDirectory.User(person.PersonId), directory.OfferingId, Ct));
    }

    [Fact]
    public async Task DocentVanEenAndereUitvoering()
    {
        var directory = new FakeDirectory();
        var lecturer = directory.Add("P7000002", OfferingRole.Lecturer);

        Assert.False(await new GroupsAccess(directory).CanManageAsync(FakeDirectory.User(lecturer.PersonId), Guid.Empty, Ct));
    }

    [Fact]
    public async Task PermissieGroupsManage()
    {
        var directory = new FakeDirectory();

        Assert.True(await new GroupsAccess(directory).CanManageAsync(FakeDirectory.User(null, GroupsPermissions.Manage), directory.OfferingId, Ct));
        Assert.False(await new GroupsAccess(directory).CanManageAsync(FakeDirectory.User(null), directory.OfferingId, Ct));
    }
}

// Every error code of the module has an English (UI) and a Dutch (API default) title.
public sealed class ElkeFoutcodeHeeftEenTitel
{
    private static readonly ResourceManager Titles = new("Doctrina.Modules.Groups.Resources.GroupsErrors", typeof(GroupsModule).Assembly);

    public static TheoryData<string> Codes() => [.. GroupsErrors.All, Api.GroupsEndpoints.OfferingRequiredCode];

    [Theory]
    [MemberData(nameof(Codes))]
    public void FoutcodeHeeftEenEngelseEnNederlandseTitel(string code)
    {
        var english = Titles.GetString(code, CultureInfo.GetCultureInfo("en"));
        var dutch = Titles.GetString(code, CultureInfo.GetCultureInfo("nl"));

        Assert.False(string.IsNullOrEmpty(english));
        Assert.False(string.IsNullOrEmpty(dutch));
        Assert.NotEqual(dutch, english);
    }
}
