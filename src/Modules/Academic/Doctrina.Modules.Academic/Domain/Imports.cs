using Doctrina.Core.Entities;

namespace Doctrina.Modules.Academic.Domain;

// Import jobs and their report (change add-bulk-import, design D3). Not exchangeable: these belong to Doctrina.

public enum ImportJobStatus { Queued, Running, Completed, Failed }

public enum ImportRowOutcome { Created, Updated, Unchanged, Failed }

public sealed class ImportJob : Entity
{
    public const int NameMaxLength = 260;
    public const int StartedByMaxLength = 200;
    public const int MessageMaxLength = 2000;

    public ImportJobStatus Status { get; set; } = ImportJobStatus.Queued;
    public bool DryRun { get; set; }
    public required string Source { get; set; }
    public required string StartedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Message { get; set; }

    /// <summary>Warnings about the files (e.g. unknown columns), one per line.</summary>
    public string? Warnings { get; set; }

    /// <summary>File name of the upload (a CSV or a zip).</summary>
    public required string UploadName { get; set; }

    /// <summary>Type of a single CSV (e.g. <c>persons</c>); null for a zip.</summary>
    public string? UploadType { get; set; }

    /// <summary>The uploaded bytes; null after the retention period (design D9).</summary>
    public byte[]? Upload { get; set; }

    public int Created { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Failed { get; set; }

    /// <summary>The dry run this job repeats for real.</summary>
    public Guid? RepeatOfId { get; set; }

    public bool IsFinished => Status is ImportJobStatus.Completed or ImportJobStatus.Failed;
}

/// <summary>The result of one data row of an import.</summary>
public sealed class ImportJobRow : Entity
{
    public const int FileMaxLength = 260;
    public const int MessageMaxLength = 2000;

    public Guid ImportJobId { get; set; }
    public required string File { get; set; }
    public int Line { get; set; }
    public string? Key { get; set; }
    public ImportRowOutcome Outcome { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
}
