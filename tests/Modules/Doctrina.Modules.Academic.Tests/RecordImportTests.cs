using Doctrina.Modules.Academic.Application;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Academic.Domain;
using Doctrina.Modules.Academic.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Academic.Tests;

internal static class RecordStreams
{
    public static async IAsyncEnumerable<ImportRecord> Of(params (string Type, Dictionary<string, string?> Fields)[] records)
    {
        var line = 0;
        foreach (var (type, fields) in records)
        {
            await Task.Yield();
            yield return new ImportRecord(type, ++line, fields);
        }
    }

    public static async IAsyncEnumerable<ImportRecord> ThenFail(IEnumerable<ImportRecord> records, string message)
    {
        foreach (var record in records)
        {
            await Task.Yield();
            yield return record;
        }

        throw new RecordSourceException(message);
    }

    public static RecordImport Import(AcademicDatabase db, AcademicDbContext context, AcademicImportOptions? limits = null) =>
        new(context, db.Time, Options.Create(limits ?? new AcademicImportOptions()), NullLogger<RecordImport>.Instance);

    /// <summary>Begin and run in one go, like a caller that has no need for the job id in between.</summary>
    public static async Task<Doctrina.Core.Results.Result<ImportRunSummary>> ImportAsync(
        this RecordImport import, RecordImportRequest request, IAsyncEnumerable<ImportRecord> records, CancellationToken ct, ICollection<string>? warnings = null)
    {
        var begun = await import.BeginAsync(request, ct);
        return begun.IsFailure ? begun.Error! : await import.RunAsync(begun.Value, records, warnings ?? [], ct);
    }
}

// Spec ooapi-consumer, requirement "Een sync is een importjob met rapport en dry-run" (the contract side of it).
public sealed class EenSyncIsEenImportjobMetRapportEnDryRun
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly RecordImportRequest Request = new("ooapi-sis", "ooapi-sis", "schedule", DryRun: false);

    [Fact]
    public async Task RecordsViaHetContractGevenEenRapport()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();

        var result = await RecordStreams.Import(db, context).ImportAsync(
            Request,
            RecordStreams.Of(("persons", Fields.Person("P1")), ("persons", Fields.Person("P2")), ("persons", Fields.Person("P3"))),
            Ct);

        var summary = result.Value!;
        Assert.True(summary.Succeeded);
        Assert.Equal(3, summary.Created);
        await using var read = db.CreateContext();
        Assert.All(await read.Persons.AsNoTracking().ToListAsync(Ct), p => Assert.Equal("ooapi-sis", p.Source));
        var job = await read.ImportJobs.AsNoTracking().SingleAsync(j => j.Id == summary.JobId, Ct);
        Assert.Equal((ImportJobStatus.Completed, "ooapi-sis", 3), (job.Status, job.Source, job.Created));
        var rows = await read.ImportJobRows.AsNoTracking().Where(r => r.ImportJobId == job.Id).ToListAsync(Ct);
        Assert.Equal(3, rows.Count(r => r.Outcome == ImportRowOutcome.Created && r.File == "persons"));
    }

    [Fact]
    public async Task DryRunSlaatNietsOp()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();

        var result = await RecordStreams.Import(db, context).ImportAsync(
            Request with { DryRun = true }, RecordStreams.Of(("courses", Fields.Course())), Ct);

        Assert.Equal(1, result.Value!.Created);
        await using var read = db.CreateContext();
        Assert.Empty(await read.Courses.AsNoTracking().ToListAsync(Ct));
        Assert.True((await read.ImportJobs.AsNoTracking().SingleAsync(Ct)).DryRun);
    }

    [Fact]
    public async Task OnbekendTypeIsEenMislukteRegel()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();

        var result = await RecordStreams.Import(db, context).ImportAsync(
            Request, RecordStreams.Of(("klassen", Fields.Of(("klasId", "K1"))), ("courses", Fields.Course())), Ct);

        Assert.Equal((1, 1), (result.Value!.Failed, result.Value.Created));
        await using var read = db.CreateContext();
        var failed = await read.ImportJobRows.AsNoTracking().SingleAsync(r => r.Outcome == ImportRowOutcome.Failed, Ct);
        Assert.Equal(ImportUpload.UnknownTypeCode, failed.Code);
        Assert.Single(await read.Courses.AsNoTracking().ToListAsync(Ct));
    }

    [Fact]
    public async Task EenBronFoutLaatDeJobMislukkenEnBewaartWatAlOpsloeg()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        var first = new ImportRecord("courses", 1, Fields.Course());

        var result = await RecordStreams.Import(db, context).ImportAsync(Request, RecordStreams.ThenFail([first], "courses: HTTP 503"), Ct);

        Assert.False(result.Value!.Succeeded);
        Assert.Equal("courses: HTTP 503", result.Value.Message);
        await using var read = db.CreateContext();
        Assert.Single(await read.Courses.AsNoTracking().ToListAsync(Ct));
        Assert.Equal(ImportJobStatus.Failed, (await read.ImportJobs.AsNoTracking().SingleAsync(Ct)).Status);
    }

    [Fact]
    public async Task TeVeelRecordsLatenDeJobMislukken()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();

        var result = await RecordStreams.Import(db, context, new AcademicImportOptions { MaxRows = 1 }).ImportAsync(
            Request, RecordStreams.Of(("courses", Fields.Course("A")), ("courses", Fields.Course("B"))), Ct);

        Assert.False(result.Value!.Succeeded);
        Assert.Contains("maximum of 1", result.Value.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EenJobIsQueuedTotDeRunStart()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        var import = RecordStreams.Import(db, context);

        var id = (await import.BeginAsync(Request, Ct)).Value;

        await using var read = db.CreateContext();
        Assert.Equal(ImportJobStatus.Queued, (await read.ImportJobs.AsNoTracking().SingleAsync(j => j.Id == id, Ct)).Status);
        var summary = (await import.RunAsync(id, RecordStreams.Of(("courses", Fields.Course())), [], Ct)).Value!;
        Assert.Equal((id, 1), (summary.JobId, summary.Created));
        var again = await import.RunAsync(id, RecordStreams.Of(), [], Ct);
        Assert.True(again.IsFailure);
    }

    [Fact]
    public async Task EenTeLangeBronnaamWordtGeweigerd()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();

        var result = await RecordStreams.Import(db, context).ImportAsync(Request with { Source = new string('x', 65) }, RecordStreams.Of(), Ct);

        Assert.True(result.IsFailure);
        Assert.Empty(await db.CreateContext().ImportJobs.AsNoTracking().ToListAsync(Ct));
    }

    [Fact]
    public async Task WaarschuwingenVanDeBronKomenOpDeJob()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();

        await RecordStreams.Import(db, context).ImportAsync(
            Request, RecordStreams.Of(), Ct, warnings: ["associations: skipped 2 of type programOfferingAssociation"]);

        await using var read = db.CreateContext();
        Assert.Contains("programOfferingAssociation", (await read.ImportJobs.AsNoTracking().SingleAsync(Ct)).Warnings, StringComparison.Ordinal);
    }
}
