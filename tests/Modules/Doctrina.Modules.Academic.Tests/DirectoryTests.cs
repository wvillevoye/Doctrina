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
