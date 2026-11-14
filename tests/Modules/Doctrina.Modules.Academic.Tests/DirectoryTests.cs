using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Application;
using Doctrina.Modules.Academic.Contracts;

namespace Doctrina.Modules.Academic.Tests;

// add-groups task 1.1 (design D1): the public contract IAcademicDirectory.
public sealed class AcademicDirectoryVoorAndereModules
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UitvoeringOpTeVerwijderenIsNietGevonden()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        await context.SeedOfferingAndPersonAsync(Ct);
        var directory = new AcademicDirectory(context, new CurrentPerson(context));
        var id = context.CourseOfferings.Single().Id;

        var active = await directory.FindOfferingAsync(id, Ct);
        var offering = Fields.Offering();
        offering["status"] = "toBeDeleted";
        await context.UpsertAsync("course-offerings", offering, Ct);
        var deleted = await directory.FindOfferingAsync(id, Ct);

        Assert.Equal(("BIO101-2026-P1", "Celbiologie", "Period 1"), (active.Value!.ExternalId, active.Value.CourseName, active.Value.SessionName));
        Assert.Equal(ErrorType.NotFound, deleted.Error!.Type);
        Assert.Equal(AcademicDirectory.OfferingNotFoundCode, deleted.Error.Code);
    }

    [Fact]
    public async Task LedenZijnActieveInschrijvingenVanActievePersonen()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        await context.SeedOfferingAndPersonAsync(Ct);
        await context.UpsertAsync("persons", Fields.Of(("personId", "P7000002"), ("givenName", "Dirk"), ("surname", "Docent")), Ct);
        await context.UpsertAsync("persons", Fields.Of(("personId", "I6000002"), ("givenName", "Sam"), ("surnamePrefix", "de"), ("surname", "Smit")), Ct);
        await context.UpsertAsync("persons", Fields.Of(("personId", "I6000003"), ("givenName", "Gone"), ("surname", "Person"), ("status", "toBeDeleted")), Ct);
        await context.UpsertAsync("associations", Fields.Association("A1"), Ct);
        await context.UpsertAsync("associations", Fields.Association("A2", "P7000002", role: "lecturer"), Ct);
        await context.UpsertAsync("associations", Fields.Association("A3", "I6000002", status: "toBeDeleted"), Ct);
        await context.UpsertAsync("associations", Fields.Association("A4", "I6000003"), Ct);
        var directory = new AcademicDirectory(context, new CurrentPerson(context));

        var members = await directory.MembersAsync(context.CourseOfferings.Single().Id, Ct);

        Assert.Equal(
            [("P7000002", "Dirk Docent", OfferingRole.Lecturer), ("I6000001", "Sanne Student", OfferingRole.Student)],
            members.Select(m => (m.ExternalId, m.Name, m.Role)));
    }
}

// add-content task 1.2 (design D4): the roles of the signed-in user in an offering, shared by Groups and Content.
public sealed class RollenVanDeIngelogdeGebruikerInEenUitvoering
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RollenPerSoortGebruiker()
    {
        var directory = new Doctrina.TestKit.Fakes.FakeDirectory();
        var lecturer = directory.Add("L1", OfferingRole.Lecturer);
        var student = directory.Add("I6000001");
        var outsider = Guid.NewGuid();

        Task<IReadOnlyList<OfferingRole>> Roles(Guid? person, Guid? offering = null) =>
            directory.CurrentRolesAsync(Doctrina.TestKit.Fakes.FakeDirectory.User(person), offering ?? directory.OfferingId, Ct);

        Assert.Equal([OfferingRole.Lecturer], await Roles(lecturer.PersonId));
        Assert.Equal([OfferingRole.Student], await Roles(student.PersonId));
        Assert.Empty(await Roles(outsider));
        Assert.Empty(await Roles(null));
        Assert.Empty(await Roles(lecturer.PersonId, Guid.NewGuid()));
    }
}

// Spec course-content of change add-course-templates; task 1.1. Class = requirement, method = scenario.
public sealed class ContentKopierenNaarEenLegeUitvoering
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AcademicGeeftDeAndereUitvoeringenVanDezelfdeCursus()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        var course = new Doctrina.Modules.Academic.Domain.Course { ExternalId = "BIO101", Source = "SIS", Name = "Cell biology" };
        var other = new Doctrina.Modules.Academic.Domain.Course { ExternalId = "WIS201", Source = "SIS", Name = "Algebra" };
        Doctrina.Modules.Academic.Domain.AcademicSession Session(string id, int year) => new()
        {
            ExternalId = id, Source = "SIS", Name = id, AcademicSessionType = Doctrina.Modules.Academic.Domain.AcademicSessionType.AcademicYear,
            StartDate = new DateOnly(year, 9, 1), EndDate = new DateOnly(year + 1, 8, 31),
        };
        Doctrina.Modules.Academic.Domain.CourseOffering Offering(string id, Doctrina.Modules.Academic.Domain.Course c, int year) =>
            new() { ExternalId = id, Source = "SIS", Name = id, Course = c, AcademicSession = Session("S" + id, year) };
        var (y24, y25, y26) = (Offering("BIO-2024", course, 2024), Offering("BIO-2025", course, 2025), Offering("BIO-2026", course, 2026));
        var gone = Offering("BIO-GONE", course, 2023);
        gone.Status = Doctrina.Core.Entities.EntityStatus.ToBeDeleted;
        context.AddRange(y24, y25, y26, gone, Offering("WIS-2026", other, 2026));
        await context.SaveChangesAsync(Ct);
        var directory = new AcademicDirectory(context, new CurrentPerson(context));

        var siblings = await directory.SiblingOfferingsAsync(y25.Id, Ct);
        var unknown = await directory.SiblingOfferingsAsync(Guid.NewGuid(), Ct);

        Assert.Equal(["BIO-2026", "BIO-2024"], siblings.Select(o => o.ExternalId));
        Assert.Empty(unknown);
    }
}
