using Doctrina.Modules.Academic.Application;
using Doctrina.Modules.Academic.Contracts;

namespace Doctrina.Modules.Academic.Tests;

// add-ooapi-interop task 3.1 (design D4): IAcademicExchangeReader. Spec ooapi-provider, requirements
// "Alleen actieve records worden getoond" and "Lijsten zijn gepagineerd volgens de standaard".
public sealed class AlleenActieveRecordsWordenGetoond
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task VerwijderdRecordIsOnzichtbaar()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        await context.SeedOfferingAndPersonAsync(Ct);
        await context.UpsertAsync("persons", Fields.Of(("personId", "I6000009"), ("givenName", "Gone"), ("surname", "Person"), ("status", "toBeDeleted")), Ct);
        var reader = new AcademicExchangeReader(context);
        var gone = context.Persons.Single(p => p.ExternalId == "I6000009").Id;

        var list = await reader.PersonsAsync(null, 1, 10, Ct);
        var byId = await reader.PersonAsync(gone, Ct);

        Assert.Equal(["I6000001"], list.Items.Select(p => p.ExternalId));
        Assert.Null(byId);
    }
}

public sealed class LijstenZijnGepagineerdVolgensDeStandaard
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TweedePaginaVanPersonen()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        for (var i = 1; i <= 45; i++)
        {
            await context.UpsertAsync("persons", Fields.Of(("personId", $"I{i:D7}"), ("givenName", "Stu"), ("surname", $"Dent{i:D2}")), Ct);
        }

        var reader = new AcademicExchangeReader(context);

        var first = await reader.PersonsAsync(null, 1, 20, Ct);
        var second = await reader.PersonsAsync(null, 2, 20, Ct);
        var third = await reader.PersonsAsync(null, 3, 20, Ct);
        var filtered = await reader.PersonsAsync("I0000007", 1, 20, Ct);

        Assert.Equal((20, 20, 5, 45), (first.Items.Count, second.Items.Count, third.Items.Count, second.TotalItems));
        Assert.Empty(first.Items.Select(p => p.Id).Intersect(second.Items.Select(p => p.Id)));
        Assert.Equal("Dent21", second.Items[0].Surname);
        Assert.Equal(["I0000007"], filtered.Items.Select(p => p.ExternalId));
    }

    [Fact]
    public async Task UitvoeringenEnInschrijvingenKrijgenDatumsRollenEnFilters()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        await context.SeedOfferingAndPersonAsync(Ct);
        await context.UpsertAsync("persons", Fields.Of(("personId", "P7000002"), ("givenName", "Dirk"), ("surname", "Docent")), Ct);
        await context.UpsertAsync("associations", Fields.Association("A1"), Ct);
        await context.UpsertAsync("associations", Fields.Association("A2", "P7000002", role: "lecturer"), Ct);
        var reader = new AcademicExchangeReader(context);
        var offering = context.CourseOfferings.Single();

        var offerings = await reader.OfferingsAsync(offering.CourseId, null, null, 1, 10, Ct);
        var lecturers = await reader.AssociationsAsync(offering.Id, null, OfferingRole.Lecturer, 1, 10, Ct);
        var student = await reader.PersonAsync(context.Persons.Single(p => p.ExternalId == "I6000001").Id, Ct);

        var session = context.AcademicSessions.Single();
        Assert.Equal((session.StartDate, session.EndDate), (offerings.Items.Single().StartDate, offerings.Items.Single().EndDate));
        Assert.Equal(["A2"], lecturers.Items.Select(a => a.ExternalId));
        Assert.Equal([OfferingRole.Student], student!.Roles);
    }
}
