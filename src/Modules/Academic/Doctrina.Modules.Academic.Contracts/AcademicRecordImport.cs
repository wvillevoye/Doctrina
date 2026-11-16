using Doctrina.Core.Results;

namespace Doctrina.Modules.Academic.Contracts;

// Write contract for modules that bring records in from another system (design D2 of add-ooapi-consumer). The records go
// through the same validation and upsert as a CSV import and the REST API, and end up as an import job with a report.

/// <summary>What to import. <see cref="Source"/> owns the records; <see cref="Name"/> and <see cref="StartedBy"/> label the job.</summary>
public sealed record RecordImportRequest(string Source, string Name, string StartedBy, bool DryRun);

/// <summary>
/// One record: <see cref="Type"/> is the import type (<c>persons</c>, <c>courses</c>, <c>course-offerings</c>, …),
/// <see cref="Fields"/> the fields by their CSV/OOAPI name. <see cref="Line"/> is shown in the report.
/// </summary>
public sealed record ImportRecord(string Type, int Line, IReadOnlyDictionary<string, string?> Fields);

/// <summary>The outcome of a run. When <see cref="Succeeded"/> is false the job has failed with <see cref="Message"/>; what was saved before stays saved.</summary>
public sealed record ImportRunSummary(Guid JobId, bool Succeeded, string? Message, int Created, int Updated, int Unchanged, int Failed);

/// <summary>Thrown by a record stream that cannot continue (the source is down, refuses us, …); the run ends as failed with the message.</summary>
public sealed class RecordSourceException : Exception
{
    public RecordSourceException()
        : base("The record source failed.")
    {
    }

    public RecordSourceException(string message)
        : base(message)
    {
    }

    public RecordSourceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Imports a stream of records as one job, in two steps so a caller can hand out the job id before the run starts.</summary>
public interface IAcademicRecordImport
{
    /// <summary>Creates the job as <c>queued</c>. An invalid request (e.g. a source that is empty or too long) is a failed <see cref="Result"/> and makes no job.</summary>
    Task<Result<Guid>> BeginAsync(RecordImportRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Runs the job: every record through the upsert of its type, then the report. <paramref name="warnings"/> is filled by the
    /// stream while it is read and ends up on the job. A job that is already finished is a conflict; one that was
    /// interrupted (still running) starts again, which is safe because upserts are idempotent.
    /// </summary>
    Task<Result<ImportRunSummary>> RunAsync(Guid jobId, IAsyncEnumerable<ImportRecord> records, ICollection<string> warnings, CancellationToken cancellationToken);
}
