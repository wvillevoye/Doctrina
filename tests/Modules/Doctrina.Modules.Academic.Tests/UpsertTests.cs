using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Application;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Academic.Tests;

// Spec academic-data, requirement "Opslaan is een upsert op ExternalId en Source".
public sealed class OpslaanIsEenUpsertOpExternalIdEnSource
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NieuwRecord()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();

        var outcome = await context.UpsertAsync("courses", Fields.Course(), Ct);

        Assert.Equal(UpsertOutcome.Created, outcome);
        Assert.Equal("BIO101", Assert.Single(await context.Courses.ToListAsync(Ct)).ExternalId);
    }

    [Fact]
    public async Task BestaandRecordBijwerken()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using (var first = db.CreateContext())
        {
            await first.UpsertAsync("courses", Fields.Course(name: "Celbiologie"), Ct);
        }

        await using var context = db.CreateContext();
        var outcome = await context.UpsertAsync("courses", Fields.Course(name: "Cell biology"), Ct);

        Assert.Equal(UpsertOutcome.Updated, outcome);
        Assert.Equal("Cell biology", Assert.Single(await context.Courses.AsNoTracking().ToListAsync(Ct)).Name);
    }

    [Fact]
    public async Task OngewijzigdRecord()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using (var first = db.CreateContext())
        {
            await first.UpsertAsync("courses", Fields.Course(), Ct);
        }

        db.Time.SetUtcNow(new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero));
        await using var context = db.CreateContext();
        var outcome = await context.UpsertAsync("courses", Fields.Course(), Ct);

        Assert.Equal(UpsertOutcome.Unchanged, outcome);
        var course = Assert.Single(await context.Courses.AsNoTracking().ToListAsync(Ct));
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero), course.DateLastModified);
    }

    [Fact]
    public async Task ZelfdeExternalIdUitEenAndereBronIsEenAnderRecord()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();

        await context.UpsertAsync("courses", Fields.Course(), Ct, source: "SIS");
        var outcome = await context.UpsertAsync("courses", Fields.Course(), Ct, source: "Manual");

        Assert.Equal(UpsertOutcome.Created, outcome);
        Assert.Equal(2, await context.Courses.CountAsync(Ct));
    }
}

// Spec academic-data, requirement "Verwijzingen lopen via ExternalId binnen dezelfde bron".
public sealed class VerwijzingenLopenViaExternalIdBinnenDezelfdeBron
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InschrijvingVoorEenOnbekendePersoon()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        await context.SeedOfferingAndPersonAsync(Ct);

        var result = await Fields.Type("associations").UpsertAsync(context, "SIS", Fields.Association(person: "I6999999"), Ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("Academic.Association.ReferenceNotFound", result.Error.Code);
        Assert.Equal(["personId"], result.Error.Details!.Keys);
        Assert.Equal(0, await context.Associations.CountAsync(Ct));
    }

    [Fact]
    public async Task VerwijzingNaarEenAndereBronWordtNietGevonden()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        await context.SeedOfferingAndPersonAsync(Ct);

        var result = await Fields.Type("associations").UpsertAsync(context, "Manual", Fields.Association(), Ct);

        Assert.Equal("Academic.Association.ReferenceNotFound", result.Error!.Code);
        Assert.Equal(["offeringId", "personId"], result.Error.Details!.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task TweedeInschrijvingMetDezelfdePersoonUitvoeringEnRolWordtGeweigerd()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        await context.SeedOfferingAndPersonAsync(Ct);
        await context.UpsertAsync("associations", Fields.Association(id: "A1"), Ct);

        var result = await Fields.Type("associations").UpsertAsync(context, "SIS", Fields.Association(id: "A2"), Ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("Academic.Association.Duplicate", result.Error.Code);
        Assert.Equal(1, await context.Associations.CountAsync(Ct));
    }
}

// Spec academic-data, requirement "Status "te verwijderen" verbergt een record zonder het te wissen".
public sealed class StatusTeVerwijderenVerbergtEenRecordZonderHetTeWissen
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InschrijvingOpTeVerwijderen()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        await context.SeedOfferingAndPersonAsync(Ct);
        await context.UpsertAsync("associations", Fields.Association(), Ct);

        var outcome = await context.UpsertAsync("associations", Fields.Association(status: "toBeDeleted"), Ct);

        Assert.Equal(UpsertOutcome.Updated, outcome);
        var stored = Assert.Single(await context.Associations.AsNoTracking().ToListAsync(Ct));
        Assert.Equal(EntityStatus.ToBeDeleted, stored.Status);
        Assert.Equal(0, await context.Associations.CountAsync(a => a.Status == EntityStatus.Active, Ct));
    }

    [Fact]
    public async Task OpTeVerwijderenRecordWordtWeerActief()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        await context.UpsertAsync("courses", Fields.Of(("courseId", "BIO101"), ("name", "Cell biology"), ("status", "toBeDeleted")), Ct);

        await context.UpsertAsync("courses", Fields.Course(name: "Cell biology"), Ct);

        Assert.Equal(EntityStatus.Active, Assert.Single(await context.Courses.AsNoTracking().ToListAsync(Ct)).Status);
    }
}
