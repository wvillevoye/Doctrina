using System.Text;
using Doctrina.Modules.Academic.Application;
using Doctrina.Modules.Academic.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Academic.Tests;

internal static class Importer
{
    public static async Task<ImportReport> RunAsync(AcademicDbContext db, string type, string csv, string source = "SIS")
    {
        var importer = new AcademicImporter(db, Options.Create(new AcademicImportOptions()));
        var result = await importer.ImportAsync(Fields.Type(type), source, new MemoryStream(Encoding.UTF8.GetBytes(csv)), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value!;
    }
}

// Spec academic-csv-import, requirement "Het rapport toont het resultaat per regel".
public sealed class HetRapportToontHetResultaatPerRegel
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EenFouteRegelTussenGoede()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        await context.SeedOfferingAndPersonAsync(Ct);
        await context.UpsertAsync("persons", Fields.Person("I6000002"), Ct);
        await context.UpsertAsync("persons", Fields.Person("P7000002"), Ct);

        var report = await Importer.RunAsync(context, "associations", """
            associationId,personId,offeringId,role
            A1,I6000001,BIO101-2026-P1,student
            A2,I6999999,BIO101-2026-P1,student
            A3,I6000002,BIO101-2026-P1,student
            A4,P7000002,BIO101-2026-P1,lecturer
            """);

        Assert.Equal([2, 3, 4, 5], report.Rows.Select(r => r.Line));
        Assert.Equal(
            [ImportRowOutcome.Created, ImportRowOutcome.Failed, ImportRowOutcome.Created, ImportRowOutcome.Created],
            report.Rows.Select(r => r.Outcome));
        var failed = report.Rows[1];
        Assert.Equal("A2", failed.ExternalId);
        Assert.Equal("Academic.Association.ReferenceNotFound", failed.Error!.Code);
        Assert.Contains("personId: ", failed.Message, StringComparison.Ordinal);
        Assert.Equal((3, 1), (report.Count(ImportRowOutcome.Created), report.Count(ImportRowOutcome.Failed)));
        Assert.Equal(["A1", "A3", "A4"], await context.Associations.OrderBy(a => a.ExternalId).Select(a => a.ExternalId).ToListAsync(Ct));
    }

    [Fact]
    public async Task OngeldigeBronWordtGeweigerd()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        var importer = new AcademicImporter(context, Options.Create(new AcademicImportOptions()));

        var result = await importer.ImportAsync(Fields.Type("courses"), " ", new MemoryStream(Encoding.UTF8.GetBytes("courseId,name\nBIO101,Cells\n")), Ct);

        Assert.Equal(AcademicImporter.InvalidSourceCode, result.Error!.Code);
    }
}

// Spec academic-csv-import, requirement "Importeren is idempotent".
public sealed class ImporterenIsIdempotent
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZelfdeBestandOpnieuw()
    {
        const string Csv = "courseId;name;studyLoad\nBIO101;Cell biology;6\nWIS201;Calculus 2;5\n";
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using (var first = db.CreateContext())
        {
            await Importer.RunAsync(first, "courses", Csv);
        }

        await using var context = db.CreateContext();
        var report = await Importer.RunAsync(context, "courses", Csv);

        Assert.Equal(2, await context.Courses.CountAsync(Ct));
        Assert.All(report.Rows, r => Assert.Equal(ImportRowOutcome.Unchanged, r.Outcome));
    }
}
