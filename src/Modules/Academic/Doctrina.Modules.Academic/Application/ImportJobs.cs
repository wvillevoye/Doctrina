using System.Diagnostics;
using System.Text;
using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Domain;
using Doctrina.Modules.Academic.Infrastructure;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Academic.Application;

/// <summary>Error codes of import jobs (titles in Resources/AcademicErrors.resx).</summary>
internal static class ImportErrors
{
    public const string InvalidSource = "Academic.Import.InvalidSource";
    public const string NotFound = "Academic.Import.NotFound";
    public const string CannotRepeat = "Academic.Import.CannotRepeat";
    public const string UploadExpired = "Academic.Import.UploadExpired";

    public static readonly string[] All =
    [
        InvalidSource, NotFound, CannotRepeat, UploadExpired, CsvReader.EmptyCode, CsvReader.MissingColumnsCode, CsvReader.TooLargeCode,
        ImportUpload.UnknownFileCode, ImportUpload.UnknownTypeCode, ImportUpload.BadZipCode,
    ];
}

/// <summary>Creates import jobs and queues them (change add-bulk-import, design D6).</summary>
internal sealed class ImportJobs(AcademicDbContext db, IBackgroundJobClient jobs, TimeProvider time, IOptions<AcademicImportOptions> options)
{
    public async Task<Result<ImportJob>> CreateAsync(
        byte[] upload, string name, string? type, string source, bool dryRun, string startedBy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(upload);
        source = source?.Trim() ?? string.Empty;
        if (source.Length is 0 or > ExchangeableEntity.SourceMaxLength)
        {
            var message = $"Enter a source of at most {ExchangeableEntity.SourceMaxLength} characters.";
            return Error.Validation(ImportErrors.InvalidSource, message, new Dictionary<string, string[]> { ["source"] = [message] });
        }

        if (upload.Length == 0)
        {
            return CsvReader.FileError(CsvReader.EmptyCode, "The file is empty.");
        }

        if (upload.Length > options.Value.MaxFileSize)
        {
            return CsvReader.FileError(CsvReader.TooLargeCode, $"The file is larger than the maximum of {options.Value.MaxFileSize / 1024 / 1024} MB.");
        }

        if (!ImportUpload.IsZip(name) && AcademicTypes.Find(type ?? string.Empty) is null)
        {
            return ImportUpload.UnknownType(type);
        }

        var job = new ImportJob
        {
            Source = source,
            StartedBy = Truncate(startedBy, ImportJob.StartedByMaxLength),
            UploadName = Truncate(Path.GetFileName(name), ImportJob.NameMaxLength),
            UploadType = ImportUpload.IsZip(name) ? null : AcademicTypes.Find(type!)!.Name,
            Upload = upload,
            DryRun = dryRun,
            CreatedAt = time.GetUtcNow(),
        };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(ct);
        jobs.Enqueue<ImportRunner>(r => r.RunAsync(job.Id, CancellationToken.None));
        return job;
    }

    /// <summary>Runs the upload of a completed dry run for real, as a new job.</summary>
    public async Task<Result<ImportJob>> RepeatAsync(Guid jobId, string startedBy, CancellationToken ct)
    {
        var dryRun = await db.ImportJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId, ct);
        if (dryRun is null)
        {
            return Error.NotFound(ImportErrors.NotFound, $"Import {jobId} doesn't exist.");
        }

        if (!dryRun.DryRun || dryRun.Status != ImportJobStatus.Completed)
        {
            return Error.Conflict(ImportErrors.CannotRepeat, "Only a completed dry run can be imported for real.");
        }

        if (dryRun.Upload is null)
        {
            return Error.Conflict(ImportErrors.UploadExpired, "The uploaded file has been removed after the retention period; upload it again.");
        }

        var result = await CreateAsync(dryRun.Upload, dryRun.UploadName, dryRun.UploadType, dryRun.Source, dryRun: false, startedBy, ct);
        if (result.IsSuccess)
        {
            result.Value!.RepeatOfId = dryRun.Id;
            await db.SaveChangesAsync(ct);
        }

        return result;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

/// <summary>
/// The Hangfire job that processes an import (design D4): reads every file first, then each row through the upsert of
/// its type; a dry run does the same inside a transaction that is rolled back. The report is saved afterwards.
/// </summary>
internal sealed partial class ImportRunner(AcademicDbContext db, TimeProvider time, IOptions<AcademicImportOptions> options, ILogger<ImportRunner> logger)
{
    private const int ClearEvery = 500;

    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await db.ImportJobs.SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null || job.IsFinished)
        {
            return;
        }

        // A job interrupted by a restart starts again; upserts are idempotent.
        await db.ImportJobRows.Where(r => r.ImportJobId == jobId).ExecuteDeleteAsync(cancellationToken);
        job.Status = ImportJobStatus.Running;
        job.StartedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        List<ImportJobRow> rows;
        List<string> warnings;
        try
        {
            var read = await ReadAsync(job, cancellationToken);
            if (read.IsFailure)
            {
                await FinishAsync(jobId, ImportJobStatus.Failed, read.Error!.Details?.Values.FirstOrDefault()?.FirstOrDefault() ?? read.Error.Message, [], [], cancellationToken);
                return;
            }

            (rows, warnings) = await ProcessAsync(job, read.Value!, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var trace = Activity.Current?.Id ?? jobId.ToString("D");
            LogFailed(logger, exception, jobId, trace);
            db.ChangeTracker.Clear();
            await FinishAsync(jobId, ImportJobStatus.Failed, $"Unexpected error (trace {trace}).", [], [], cancellationToken);
            return;
        }

        await FinishAsync(jobId, ImportJobStatus.Completed, null, rows, warnings, cancellationToken);
    }

    /// <summary>All files of the upload as rows; any problem with a file as a whole fails the job before anything is saved.</summary>
    private async Task<Result<List<(ImportFile File, CsvFile Csv)>>> ReadAsync(ImportJob job, CancellationToken ct)
    {
        var limits = options.Value;
        var files = ImportUpload.Read(job.Upload ?? [], job.UploadName, job.UploadType, limits);
        if (files.IsFailure)
        {
            return files.Error!;
        }

        var result = new List<(ImportFile, CsvFile)>();
        var rows = 0;
        foreach (var file in files.Value!)
        {
            var remaining = new AcademicImportOptions { MaxRows = limits.MaxRows - rows, MaxFileSize = limits.MaxFileSize };
            var csv = await CsvReader.ReadAsync(new MemoryStream(file.Content), file.Type, remaining, ct);
            if (csv.IsFailure)
            {
                // Sizes were checked on upload and when unzipping, so "too large" here means the row limit.
                return csv.Error!.Code == CsvReader.TooLargeCode
                    ? CsvReader.FileError(CsvReader.TooLargeCode, $"The upload has more than the maximum of {limits.MaxRows} rows.")
                    : CsvReader.FileError(csv.Error.Code, $"{file.Name}: {csv.Error.Details?.Values.FirstOrDefault()?.FirstOrDefault() ?? csv.Error.Message}");
            }

            rows += csv.Value!.Rows.Count;
            result.Add((file, csv.Value));
        }

        return result;
    }

    private async Task<(List<ImportJobRow> Rows, List<string> Warnings)> ProcessAsync(
        ImportJob job, List<(ImportFile File, CsvFile Csv)> files, CancellationToken ct)
    {
        var (jobId, source) = (job.Id, job.Source);
        var transaction = job.DryRun ? await db.Database.BeginTransactionAsync(ct) : null;
        var rows = new List<ImportJobRow>();
        var warnings = new List<string>();
        try
        {
            foreach (var (file, csv) in files)
            {
                warnings.AddRange(csv.Warnings.Select(w => $"{file.Name}: {w}"));
                foreach (var row in csv.Rows)
                {
                    var report = await ImportReport.RowAsync(db, source, file.Type, jobId, file.Name, row.Line, row.Fields, ct);
                    rows.Add(report);
                    if (report.Outcome == ImportRowOutcome.Failed || rows.Count % ClearEvery == 0)
                    {
                        db.ChangeTracker.Clear();
                    }
                }
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                await transaction.DisposeAsync();
            }

            db.ChangeTracker.Clear();
        }

        return (rows, warnings);
    }

    private Task FinishAsync(Guid jobId, ImportJobStatus status, string? message, List<ImportJobRow> rows, List<string> warnings, CancellationToken ct) =>
        ImportReport.FinishAsync(db, time, jobId, status, message, rows, warnings, ct);

    [LoggerMessage(Level = LogLevel.Error, Message = "Import {JobId} failed unexpectedly (trace {Trace}).")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid jobId, string trace);
}

/// <summary>The part of an import that CSV uploads and record streams share: one row through the upsert, and closing the job (design D2 of add-ooapi-consumer).</summary>
internal static class ImportReport
{
    private const int SaveRowsPer = 1000;

    /// <summary>Runs one record through the upsert of its type and describes the result; a failed record is a failed row, not an exception.</summary>
    public static async Task<ImportJobRow> RowAsync(
        AcademicDbContext db, string source, AcademicType type, Guid jobId, string file, int line, IReadOnlyDictionary<string, string?> fields, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(fields);
        var key = fields.TryGetValue(type.KeyField, out var k) ? k?.Trim() : null;
        var result = await type.UpsertAsync(db, source, fields, ct);
        return new ImportJobRow
        {
            ImportJobId = jobId,
            File = file,
            Line = line,
            Key = key,
            Outcome = result.IsSuccess ? Outcome(result.Value) : ImportRowOutcome.Failed,
            Code = result.Error?.Code,
            Message = Message(result.Error),
        };
    }

    public static async Task FinishAsync(
        AcademicDbContext db, TimeProvider time, Guid jobId, ImportJobStatus status, string? message, List<ImportJobRow> rows, List<string> warnings, CancellationToken ct)
    {
        var job = await db.ImportJobs.SingleAsync(j => j.Id == jobId, ct);
        job.Status = status;
        job.FinishedAt = time.GetUtcNow();
        job.Message = Limit(message, ImportJob.MessageMaxLength);
        job.Warnings = warnings.Count == 0 ? null : Limit(string.Join('\n', warnings), ImportJob.MessageMaxLength);
        job.Created = rows.Count(r => r.Outcome == ImportRowOutcome.Created);
        job.Updated = rows.Count(r => r.Outcome == ImportRowOutcome.Updated);
        job.Unchanged = rows.Count(r => r.Outcome == ImportRowOutcome.Unchanged);
        job.Failed = rows.Count(r => r.Outcome == ImportRowOutcome.Failed);
        await db.SaveChangesAsync(ct);
        foreach (var chunk in rows.Chunk(SaveRowsPer))
        {
            db.ChangeTracker.Clear();
            db.ImportJobRows.AddRange(chunk);
            await db.SaveChangesAsync(ct);
        }

        db.ChangeTracker.Clear();
    }

    private static ImportRowOutcome Outcome(UpsertOutcome outcome) => outcome switch
    {
        UpsertOutcome.Created => ImportRowOutcome.Created,
        UpsertOutcome.Updated => ImportRowOutcome.Updated,
        _ => ImportRowOutcome.Unchanged,
    };

    /// <summary>"The Person record is invalid. surname: Required."</summary>
    internal static string? Message(Error? error) => error is null ? null
        : Limit(string.Join(' ', [error.Message, .. (error.Details ?? new Dictionary<string, string[]>()).Select(d => $"{d.Key}: {string.Join(' ', d.Value)}")]), ImportJobRow.MessageMaxLength);

    private static string? Limit(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}

/// <summary>Removes uploads after the retention period; jobs and reports stay (design D9).</summary>
internal sealed class ImportRetention(AcademicDbContext db, TimeProvider time, IOptions<AcademicImportOptions> options)
{
    public const string JobId = "academic-import-retention";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var before = time.GetUtcNow().AddDays(-options.Value.RetentionDays);
        await db.ImportJobs
            .Where(j => j.Upload != null && j.CreatedAt < before)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Upload, (byte[]?)null), cancellationToken);
    }
}

/// <summary>The report of a job as CSV: <c>file,line,key,result,code,message</c>.</summary>
internal static class ImportReportCsv
{
    public static string Header => "file,line,key,result,code,message";

    public static string Line(ImportJobRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return string.Join(',', Value(row.File), row.Line.ToString(System.Globalization.CultureInfo.InvariantCulture), Value(row.Key),
            row.Outcome.ToString().ToLowerInvariant(), Value(row.Code), Value(row.Message));
    }

    /// <summary>A CSV template for a type: only the header row (spec "Er is een leeg sjabloon per type").</summary>
    public static string Template(AcademicType type) => string.Join(',', type.Fields.Select(f => f.Name)) + "\n";

    private static string Value(string? value) =>
        value is null ? string.Empty
        : value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"'
        : value;

    public static byte[] Encode(string header, IEnumerable<string> lines)
    {
        var text = new StringBuilder(header).Append('\n');
        foreach (var line in lines)
        {
            text.Append(line).Append('\n');
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
    }
}

/// <summary>Registers the daily retention job at start-up, when background jobs are enabled (design D9).</summary>
internal sealed class ImportRetentionSchedule(IServiceProvider services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    : Microsoft.Extensions.Hosting.IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Doctrina.Infrastructure.Jobs.JobServiceCollectionExtensions.IsEnabled(configuration))
        {
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IRecurringJobManager>(services)
                .AddOrUpdate<ImportRetention>(ImportRetention.JobId, r => r.RunAsync(CancellationToken.None), Cron.Daily(3));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
