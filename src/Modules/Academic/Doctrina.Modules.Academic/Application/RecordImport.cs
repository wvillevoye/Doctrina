using System.Diagnostics;
using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Academic.Domain;
using Doctrina.Modules.Academic.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Academic.Application;

/// <summary>
/// <see cref="IAcademicRecordImport"/> (design D2 of add-ooapi-consumer): the records of another system go through the same
/// upsert as a CSV import and end up as an import job with a report. A dry run does the same inside a transaction that is
/// rolled back. The job has no upload.
/// </summary>
internal sealed partial class RecordImport(AcademicDbContext db, TimeProvider time, IOptions<AcademicImportOptions> options, ILogger<RecordImport> logger)
    : IAcademicRecordImport
{
    private const int ClearEvery = 500;

    public async Task<Result<Guid>> BeginAsync(RecordImportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = request.Source?.Trim() ?? string.Empty;
        if (source.Length is 0 or > ExchangeableEntity.SourceMaxLength)
        {
            var message = $"Enter a source of at most {ExchangeableEntity.SourceMaxLength} characters.";
            return Error.Validation(ImportErrors.InvalidSource, message, new Dictionary<string, string[]> { ["source"] = [message] });
        }

        var job = new ImportJob
        {
            Source = source,
            StartedBy = Truncate(request.StartedBy, ImportJob.StartedByMaxLength),
            UploadName = Truncate(request.Name, ImportJob.NameMaxLength),
            DryRun = request.DryRun,
            CreatedAt = time.GetUtcNow(),
        };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return job.Id;
    }

    public async Task<Result<ImportRunSummary>> RunAsync(
        Guid jobId, IAsyncEnumerable<ImportRecord> records, ICollection<string> warnings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(warnings);
        var job = await db.ImportJobs.SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return Error.NotFound(ImportErrors.NotFound, $"Import {jobId} doesn't exist.");
        }

        if (job.IsFinished)
        {
            return Error.Conflict(ImportErrors.CannotRepeat, "This import has already finished.");
        }

        // A job interrupted by a restart starts again; upserts are idempotent.
        await db.ImportJobRows.Where(r => r.ImportJobId == jobId).ExecuteDeleteAsync(cancellationToken);
        var (source, dryRun) = (job.Source, job.DryRun);
        job.Status = ImportJobStatus.Running;
        job.StartedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var transaction = dryRun ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var rows = new List<ImportJobRow>();
        var (status, failure) = (ImportJobStatus.Completed, (string?)null);
        try
        {
            await foreach (var record in records.WithCancellation(cancellationToken))
            {
                if (rows.Count >= options.Value.MaxRows)
                {
                    (status, failure) = (ImportJobStatus.Failed, $"The source has more than the maximum of {options.Value.MaxRows} records.");
                    break;
                }

                var row = AcademicTypes.Find(record.Type) is { } type
                    ? await ImportReport.RowAsync(db, source, type, jobId, record.Type, record.Line, record.Fields, cancellationToken)
                    : new ImportJobRow
                    {
                        ImportJobId = jobId,
                        File = record.Type,
                        Line = record.Line,
                        Outcome = ImportRowOutcome.Failed,
                        Code = ImportUpload.UnknownTypeCode,
                        Message = $"Unknown record type '{record.Type}'.",
                    };
                rows.Add(row);
                if (row.Outcome == ImportRowOutcome.Failed || rows.Count % ClearEvery == 0)
                {
                    db.ChangeTracker.Clear();
                }
            }
        }
        catch (RecordSourceException exception)
        {
            (status, failure) = (ImportJobStatus.Failed, exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var trace = Activity.Current?.Id ?? jobId.ToString("D");
            LogFailed(logger, exception, jobId, trace);
            (status, failure) = (ImportJobStatus.Failed, $"Unexpected error (trace {trace}).");
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

        await ImportReport.FinishAsync(db, time, jobId, status, failure, rows, [.. warnings], cancellationToken);
        int Count(ImportRowOutcome outcome) => rows.Count(r => r.Outcome == outcome);
        return new ImportRunSummary(
            jobId, status == ImportJobStatus.Completed, failure,
            Count(ImportRowOutcome.Created), Count(ImportRowOutcome.Updated), Count(ImportRowOutcome.Unchanged), Count(ImportRowOutcome.Failed));
    }

    private static string Truncate(string? value, int max) => value is null ? string.Empty : value.Length <= max ? value : value[..max];

    [LoggerMessage(Level = LogLevel.Error, Message = "Record import {JobId} failed unexpectedly (trace {Trace}).")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid jobId, string trace);
}
