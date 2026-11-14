using System.IO.Compression;
using System.Text;
using Doctrina.Modules.Academic.Application;
using Doctrina.Modules.Academic.Domain;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Academic.Tests;

/// <summary>Builds uploads and runs import jobs directly through <see cref="ImportRunner"/> (no Hangfire server).</summary>
internal static class Imports
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static byte[] Csv(string text) => Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal));

    public static byte[] Zip(params (string Name, string Content)[] files)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        return buffer.ToArray();
    }

    public static async Task<ImportJob> RunAsync(
        AcademicDatabase db, string name, byte[] upload, string? type = null, bool dryRun = false, string source = "SIS", AcademicImportOptions? limits = null)
    {
        var job = new ImportJob { Source = source, StartedBy = "test", UploadName = name, UploadType = type, Upload = upload, DryRun = dryRun, CreatedAt = db.Time.GetUtcNow() };
        await using (var create = db.CreateContext())
        {
            create.ImportJobs.Add(job);
            await create.SaveChangesAsync(Ct);
        }

        await RunAsync(db, job.Id, limits);
        await using var read = db.CreateContext();
        return await read.ImportJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id, Ct);
    }

    public static async Task RunAsync(AcademicDatabase db, Guid jobId, AcademicImportOptions? limits = null)
    {
        await using var run = db.CreateContext();
        await new ImportRunner(run, db.Time, Options.Create(limits ?? new AcademicImportOptions()), NullLogger<ImportRunner>.Instance).RunAsync(jobId, Ct);
    }

    public static async Task<List<ImportJobRow>> RowsAsync(AcademicDatabase db, Guid jobId)
    {
        await using var read = db.CreateContext();
        return await read.ImportJobRows.AsNoTracking().Where(r => r.ImportJobId == jobId).OrderBy(r => r.File).ThenBy(r => r.Line).ToListAsync(Ct);
    }

    public const string Sessions = "academicSessionId,name,academicSessionType,startDate,endDate\n2026-P1,Period 1,period,2026-09-01,2026-10-31\n";
    public const string Courses = "courseId,name\nBIO101,Cell biology\n";
    public const string Offerings = "offeringId,name,courseId,academicSessionId\nBIO101-2026-P1,Cell biology P1,BIO101,2026-P1\n";
    public const string Persons = "personId,givenName,surname\nI6000001,Sanne,Student\nI6000002,Sam,Smit\n";
    public const string Associations = "associationId,personId,offeringId,role\nA1,I6000001,BIO101-2026-P1,student\nA2,I6000002,BIO101-2026-P1,student\n";
}

/// <summary>Records what is queued, instead of a Hangfire server.</summary>
internal sealed class FakeJobClient : IBackgroundJobClient
{
    public List<Job> Created { get; } = [];

    public string Create(Job job, IState state)
    {
        Created.Add(job);
        return Created.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public bool ChangeState(string jobId, IState state, string expectedState) => true;
}

// add-bulk-import task 2.1 (design D6): creating and repeating jobs.
public sealed class ImportjobsAanmakenEnHerhalen
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ImportJobs Jobs(AcademicDatabase db, Infrastructure.AcademicDbContext context, FakeJobClient client) =>
        new(context, client, db.Time, Options.Create(new AcademicImportOptions()));

    [Fact]
    public async Task JobWordtOpgeslagenEnIngepland()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();
        var client = new FakeJobClient();

        var job = (await Jobs(db, context, client).CreateAsync(Imports.Csv(Imports.Courses), "courses.csv", "courses", "SIS", dryRun: false, "Beheer", Ct)).Value!;

        Assert.Equal((ImportJobStatus.Queued, "courses", "Beheer"), (job.Status, job.UploadType, job.StartedBy));
        var queued = Assert.Single(client.Created);
        Assert.Equal((typeof(ImportRunner), nameof(ImportRunner.RunAsync)), (queued.Type, queued.Method.Name));
        Assert.Equal(job.Id, queued.Args[0]);
    }

    [Fact]
    public async Task CsvZonderTypeWordtGeweigerd()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();

        var result = await Jobs(db, context, new FakeJobClient()).CreateAsync(Imports.Csv(Imports.Courses), "data.csv", "grades", "SIS", false, "Beheer", Ct);

        Assert.Equal(ImportUpload.UnknownTypeCode, result.Error!.Code);
    }

    [Fact]
    public async Task HerhalenMaaktEenNieuweJobZonderDryRun()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        var dryRun = await Imports.RunAsync(db, "courses.csv", Imports.Csv(Imports.Courses), "courses", dryRun: true);
        await using var context = db.CreateContext();
        var client = new FakeJobClient();

        var repeat = (await Jobs(db, context, client).RepeatAsync(dryRun.Id, "Beheer", Ct)).Value!;

        Assert.Equal((false, dryRun.Id, ImportJobStatus.Queued), (repeat.DryRun, repeat.RepeatOfId, repeat.Status));
        Assert.Single(client.Created);
    }

    [Fact]
    public async Task HerhalenZonderUpload()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        var dryRun = await Imports.RunAsync(db, "courses.csv", Imports.Csv(Imports.Courses), "courses", dryRun: true);
        await using (var clear = db.CreateContext())
        {
            await clear.ImportJobs.ExecuteUpdateAsync(s => s.SetProperty(j => j.Upload, (byte[]?)null), Ct);
        }

        await using var context = db.CreateContext();
        var result = await Jobs(db, context, new FakeJobClient()).RepeatAsync(dryRun.Id, "Beheer", Ct);

        Assert.Equal(ImportErrors.UploadExpired, result.Error!.Code);
    }
}

// Spec academic-csv-import, requirement "Een zip bevat meerdere typen in een vaste volgorde" (reading; task 2.2).
public sealed class EenZipBevatMeerdereTypenInEenVasteVolgorde
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void ManifestMetEigenBestandsnamen()
    {
        var zip = Imports.Zip(("manifest.csv", "file,type\nstudenten.csv,persons\n"), ("studenten.csv", Imports.Persons));

        var files = ImportUpload.Read(zip, "upload.zip", null, new AcademicImportOptions()).Value!;

        Assert.Equal([("studenten.csv", "persons")], files.Select(f => (f.Name, f.Type.Name)));
    }

    [Fact]
    public void OnbekendBestand()
    {
        var zip = Imports.Zip(("persons.csv", Imports.Persons), ("cijfers.csv", "x\n1\n"));

        var result = ImportUpload.Read(zip, "upload.zip", null, new AcademicImportOptions());

        Assert.Equal(ImportUpload.UnknownFileCode, result.Error!.Code);
        Assert.Contains("cijfers.csv", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VolgordeKomtUitDeTypen()
    {
        var zip = Imports.Zip(("associations.csv", Imports.Associations), ("persons.csv", Imports.Persons), ("courses.csv", Imports.Courses),
            ("data/academic-sessions.csv", Imports.Sessions), ("__MACOSX/._persons.csv", "junk"), ("readme.txt", "hello"));

        var files = ImportUpload.Read(zip, "upload.zip", null, new AcademicImportOptions()).Value!;

        Assert.Equal(["academic-sessions", "courses", "persons", "associations"], files.Select(f => f.Type.Name));
    }

    [Fact]
    public void TeGrootUitgepaktBestand()
    {
        var zip = Imports.Zip(("persons.csv", "personId,givenName,surname\n" + new string('x', 5000)));

        var result = ImportUpload.Read(zip, "upload.zip", null, new AcademicImportOptions { MaxFileSize = 1000 });

        Assert.Equal(CsvReader.TooLargeCode, result.Error!.Code);
    }

    [Fact]
    public void GeenGeldigeZip()
    {
        var result = ImportUpload.Read(Encoding.UTF8.GetBytes("not a zip"), "upload.zip", null, new AcademicImportOptions());

        Assert.Equal(ImportUpload.BadZipCode, result.Error!.Code);
    }

    [Fact]
    public async Task ZipMetVerwijzingenTussenBestanden()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        var zip = Imports.Zip(("associations.csv", Imports.Associations), ("persons.csv", Imports.Persons), ("course-offerings.csv", Imports.Offerings),
            ("courses.csv", Imports.Courses), ("academic-sessions.csv", Imports.Sessions));

        var job = await Imports.RunAsync(db, "data.zip", zip);

        Assert.Equal((ImportJobStatus.Completed, 7, 0), (job.Status, job.Created, job.Failed));
        Assert.All(await Imports.RowsAsync(db, job.Id), r => Assert.Equal(ImportRowOutcome.Created, r.Outcome));
    }
}

// Spec academic-csv-import, requirement "Een import draait als achtergrondjob".
public sealed class EenImportDraaitAlsAchtergrondjob
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task JobDoorlooptDeStatussen()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);

        var job = await Imports.RunAsync(db, "persons.csv", Imports.Csv(Imports.Persons), "persons");

        Assert.Equal((ImportJobStatus.Completed, 2), (job.Status, job.Created));
        Assert.NotNull(job.StartedAt);
        Assert.NotNull(job.FinishedAt);
    }

    [Fact]
    public async Task OntbrekendeVerplichteKolom()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);

        var job = await Imports.RunAsync(db, "persons.csv", Imports.Csv("personId,givenName\nI6000001,Sanne\n"), "persons");

        Assert.Equal(ImportJobStatus.Failed, job.Status);
        Assert.Contains("surname", job.Message, StringComparison.Ordinal);
        await using var context = db.CreateContext();
        Assert.Equal(0, await context.Persons.CountAsync(Ct));
    }

    [Fact]
    public async Task FouteZipSlaatNietsOp()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);

        // persons.csv is fine, associations.csv misses a column: nothing at all is saved.
        var job = await Imports.RunAsync(db, "data.zip", Imports.Zip(("persons.csv", Imports.Persons), ("associations.csv", "associationId,personId\nA1,I6000001\n")));

        Assert.Equal(ImportJobStatus.Failed, job.Status);
        Assert.Contains("associations.csv", job.Message, StringComparison.Ordinal);
        await using var context = db.CreateContext();
        Assert.Equal(0, await context.Persons.CountAsync(Ct));
    }

    [Fact]
    public async Task OnderbrokenJobBegintOpnieuw()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        var job = await Imports.RunAsync(db, "persons.csv", Imports.Csv(Imports.Persons), "persons");
        await using (var reset = db.CreateContext())
        {
            await reset.ImportJobs.ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, ImportJobStatus.Running), Ct);
        }

        await Imports.RunAsync(db, job.Id);

        await using var context = db.CreateContext();
        Assert.Equal(ImportJobStatus.Completed, (await context.ImportJobs.SingleAsync(Ct)).Status);
        Assert.Equal(2, await context.ImportJobRows.CountAsync(Ct)); // no duplicates from the first attempt
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
        await Imports.RunAsync(db, "data.zip", Imports.Zip(("courses.csv", Imports.Courses), ("academic-sessions.csv", Imports.Sessions),
            ("course-offerings.csv", Imports.Offerings), ("persons.csv", Imports.Persons + "P7000002,Dirk,Docent\nI6000003,Tess,Bakker\n")));

        var job = await Imports.RunAsync(db, "associations.csv", Imports.Csv("""
            associationId,personId,offeringId,role
            A1,I6000001,BIO101-2026-P1,student
            A2,I6999999,BIO101-2026-P1,student
            A3,I6000003,BIO101-2026-P1,student
            A4,P7000002,BIO101-2026-P1,lecturer
            """), "associations");

        var rows = await Imports.RowsAsync(db, job.Id);
        Assert.Equal([2, 3, 4, 5], rows.Select(r => r.Line));
        Assert.Equal([ImportRowOutcome.Created, ImportRowOutcome.Failed, ImportRowOutcome.Created, ImportRowOutcome.Created], rows.Select(r => r.Outcome));
        Assert.Equal(("A2", "Academic.Association.ReferenceNotFound"), (rows[1].Key, rows[1].Code));
        Assert.Contains("personId: ", rows[1].Message, StringComparison.Ordinal);
        Assert.Equal((3, 1), (job.Created, job.Failed));
    }

    [Fact]
    public void RapportAlsCsv()
    {
        var row = new ImportJobRow { File = "persons.csv", Line = 3, Key = "I6000002", Outcome = ImportRowOutcome.Failed, Code = "Academic.Person.Invalid", Message = "Invalid. surname: Required, really." };

        var text = Encoding.UTF8.GetString(ImportReportCsv.Encode(ImportReportCsv.Header, [ImportReportCsv.Line(row)])).TrimStart('﻿');

        Assert.Equal("file,line,key,result,code,message\npersons.csv,3,I6000002,failed,Academic.Person.Invalid,\"Invalid. surname: Required, really.\"\n", text);
    }

    [Fact]
    public async Task OnbekendeKolomGeeftEenWaarschuwing()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);

        var job = await Imports.RunAsync(db, "courses.csv", Imports.Csv("courseId,name,colour\nBIO101,Cell biology,green\n"), "courses");

        Assert.Contains("colour", job.Warnings, StringComparison.Ordinal);
    }
}

// Spec academic-csv-import, requirement "De import heeft een grens".
public sealed class DeImportHeeftEenGrens
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TeGrootBestand()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        var csv = "courseId,name\n" + string.Concat(Enumerable.Range(1, 101).Select(i => $"C{i},Course {i}\n"));

        var job = await Imports.RunAsync(db, "courses.csv", Imports.Csv(csv), "courses", limits: new AcademicImportOptions { MaxRows = 100 });

        Assert.Equal(ImportJobStatus.Failed, job.Status);
        Assert.Contains("100", job.Message, StringComparison.Ordinal);
        await using var context = db.CreateContext();
        Assert.Equal(0, await context.Courses.CountAsync(Ct));
    }

    [Fact]
    public async Task GrensGeldtVoorDeHeleUpload()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);

        var job = await Imports.RunAsync(db, "data.zip", Imports.Zip(("courses.csv", Imports.Courses), ("persons.csv", Imports.Persons)),
            limits: new AcademicImportOptions { MaxRows = 2 });

        Assert.Equal(ImportJobStatus.Failed, job.Status);
    }
}

// Spec academic-csv-import, requirement "Een dry-run laat zien wat er zou gebeuren zonder op te slaan".
public sealed class EenDryRunLaatZienWatErZouGebeurenZonderOpTeSlaan
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DryRunVanEenZip()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        var zip = Imports.Zip(("associations.csv", Imports.Associations), ("persons.csv", Imports.Persons), ("course-offerings.csv", Imports.Offerings),
            ("courses.csv", Imports.Courses), ("academic-sessions.csv", Imports.Sessions));

        var job = await Imports.RunAsync(db, "data.zip", zip, dryRun: true);

        Assert.Equal((ImportJobStatus.Completed, 7, 0), (job.Status, job.Created, job.Failed));
        Assert.Equal(7, (await Imports.RowsAsync(db, job.Id)).Count(r => r.Outcome == ImportRowOutcome.Created));
        await using var context = db.CreateContext();
        Assert.Equal((0, 0, 0), (await context.Courses.CountAsync(Ct), await context.Persons.CountAsync(Ct), await context.Associations.CountAsync(Ct)));
    }
}

// Spec academic-csv-import, requirement "Importeren is idempotent" (now through the runner).
public sealed class ImporterenIsIdempotent
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZelfdeBestandOpnieuw()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        var csv = Imports.Csv("courseId;name;studyLoad\nBIO101;Cell biology;6\nWIS201;Calculus 2;5\n");
        await Imports.RunAsync(db, "courses.csv", csv, "courses");

        var second = await Imports.RunAsync(db, "courses.csv", csv, "courses");

        Assert.Equal((0, 2), (second.Created, second.Unchanged));
        await using var context = db.CreateContext();
        Assert.Equal(2, await context.Courses.CountAsync(Ct));
    }
}

// Spec academic-csv-import, requirement "Geüploade bestanden worden na de bewaartermijn verwijderd".
public sealed class GeuploadeBestandenWordenNaDeBewaartermijnVerwijderd
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OudeUploadOpruimen()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        var old = await Imports.RunAsync(db, "courses.csv", Imports.Csv(Imports.Courses), "courses");
        db.Time.Advance(TimeSpan.FromDays(31));
        var recent = await Imports.RunAsync(db, "persons.csv", Imports.Csv(Imports.Persons), "persons");

        await using (var run = db.CreateContext())
        {
            await new ImportRetention(run, db.Time, Options.Create(new AcademicImportOptions())).RunAsync(Ct);
        }

        await using var context = db.CreateContext();
        var jobs = await context.ImportJobs.AsNoTracking().ToDictionaryAsync(j => j.Id, Ct);
        Assert.Null(jobs[old.Id].Upload);
        Assert.NotNull(jobs[recent.Id].Upload);
        Assert.Single(await Imports.RowsAsync(db, old.Id)); // the report stays
    }
}

// Spec academic-csv-import, requirement "Er is een leeg sjabloon per type".
public sealed class ErIsEenLeegSjabloonPerType
{
    [Fact]
    public void SjabloonVoorPersonen() =>
        Assert.Equal("personId,givenName,surnamePrefix,surname,displayName,mail,status\n", ImportReportCsv.Template(Fields.Type("persons")));
}
