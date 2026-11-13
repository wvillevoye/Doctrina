using Doctrina.Modules.Academic.Domain;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Academic.Tests;

// Spec academic-data, requirement "Het kernmodel volgt OOAPI v5".
public sealed class HetKernmodelVolgtOoapiV5
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UitvoeringMetCursusPeriodeEnInschrijving()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using (var context = db.CreateContext())
        {
            var course = new Course { ExternalId = "BIO101", Source = "SIS", Name = "Cell biology" };
            var session = new AcademicSession
            {
                ExternalId = "2026-P1", Source = "SIS", Name = "Period 1", AcademicSessionType = AcademicSessionType.Period,
                StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 10, 31),
            };
            var person = new Person { ExternalId = "I6000001", Source = "SIS", GivenName = "Sanne", Surname = "Student" };
            var offering = new CourseOffering { ExternalId = "BIO101-2026-P1", Source = "SIS", Name = "Cell biology 2026 P1", Course = course, AcademicSession = session };
            context.Add(new Association { ExternalId = "A1", Source = "SIS", Role = AssociationRole.Student, Person = person, CourseOffering = offering });
            await context.SaveChangesAsync(Ct);
        }

        await using var read = db.CreateContext();
        var stored = await read.CourseOfferings
            .Include(o => o.Course).Include(o => o.AcademicSession).Include(o => o.Associations).ThenInclude(a => a.Person)
            .SingleAsync(o => o.ExternalId == "BIO101-2026-P1", Ct);

        Assert.Equal("BIO101", stored.Course.ExternalId);
        Assert.Equal("2026-P1", stored.AcademicSession.ExternalId);
        var association = Assert.Single(stored.Associations);
        Assert.Equal(AssociationRole.Student, association.Role);
        Assert.Equal("I6000001", association.Person.ExternalId);
    }
}
