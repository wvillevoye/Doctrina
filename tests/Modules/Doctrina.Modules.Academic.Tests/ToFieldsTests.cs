using Doctrina.Modules.Academic.Application;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Academic.Tests;

// add-rest-api task 1.1 (design D2): ToFields is the reverse of the upsert, for every type.
public sealed class ToFieldsIsHetOmgekeerdeVanDeUpsert
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task SeedEverythingAsync(Infrastructure.AcademicDbContext db)
    {
        await db.UpsertAsync("organizations", Fields.Of(("organizationId", "UM"), ("name", "Maastricht University"), ("organizationType", "root")), Ct);
        await db.UpsertAsync("organizations", Fields.Of(("organizationId", "FSE"), ("name", "Science, Engineering"), ("shortName", "FSE"), ("organizationType", "faculty"), ("parentOrganizationId", "UM")), Ct);
        await db.UpsertAsync("academic-sessions", Fields.Of(("academicSessionId", "2026-2027"), ("name", "Year"), ("academicSessionType", "academic year"), ("startDate", "2026-09-01"), ("endDate", "2027-08-31")), Ct);
        await db.UpsertAsync("academic-sessions", Fields.Of(("academicSessionId", "2026-P1"), ("name", "P1"), ("academicSessionType", "period"), ("startDate", "2026-09-01"), ("endDate", "2026-10-23"), ("parentAcademicSessionId", "2026-2027")), Ct);
        await db.UpsertAsync("courses", Fields.Of(("courseId", "BIO101"), ("name", "Cell biology"), ("abbreviation", "BIO"), ("description", "Cells."), ("studyLoad", "7.5"), ("organizationId", "FSE")), Ct);
        await db.UpsertAsync("course-offerings", Fields.Of(("offeringId", "BIO101-2026-P1"), ("name", "Cell biology P1"), ("courseId", "BIO101"), ("academicSessionId", "2026-P1"), ("startDate", "2026-09-07"), ("endDate", "2026-10-16")), Ct);
        await db.UpsertAsync("persons", Fields.Of(("personId", "I6000001"), ("givenName", "Sanne"), ("surnamePrefix", "de"), ("surname", "Student"), ("displayName", "Sanne de Student"), ("mail", "sanne@example.test")), Ct);
        await db.UpsertAsync("associations", Fields.Of(("associationId", "A1"), ("personId", "I6000001"), ("offeringId", "BIO101-2026-P1"), ("role", "teaching-assistant"), ("status", "toBeDeleted")), Ct);
    }

    [Fact]
    public async Task TerugschrijvenVeranderdNiets()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using (var seed = db.CreateContext())
        {
            await SeedEverythingAsync(seed);
        }

        await using var context = db.CreateContext();
        foreach (var type in AcademicTypes.All)
        {
            var records = await type.Query(context).AsNoTracking().ToListAsync(Ct);
            Assert.NotEmpty(records);
            foreach (var record in records)
            {
                var outcome = await context.UpsertAsync(type.Name, type.ToFields(record), Ct);
                Assert.True(outcome == UpsertOutcome.Unchanged, $"{type.Name} {record.ExternalId}: {outcome}");
            }
        }
    }

    [Fact]
    public async Task VerwijzingenDatumsEnWaarden()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        await SeedEverythingAsync(context);

        var offering = Fields.Type("course-offerings").ToFields(await Fields.Type("course-offerings").Query(context).SingleAsync(Ct));
        var course = Fields.Type("courses").ToFields(await Fields.Type("courses").Query(context).SingleAsync(Ct));
        var association = Fields.Type("associations").ToFields(await Fields.Type("associations").Query(context).SingleAsync(Ct));

        Assert.Equal(("BIO101-2026-P1", "BIO101", "2026-P1", "2026-09-07"), (offering["offeringId"], offering["courseId"], offering["academicSessionId"], offering["startDate"]));
        Assert.Equal(("7.5", "FSE"), (course["studyLoad"], course["organizationId"]));
        Assert.Equal(("teaching-assistant", "toBeDeleted", "I6000001"), (association["role"], association["status"], association["personId"]));
    }
}
