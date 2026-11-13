using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Infrastructure;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Academic.Application;

internal enum ImportRowOutcome { Created, Updated, Unchanged, Failed }

internal sealed record ImportRowResult(int Line, string? ExternalId, ImportRowOutcome Outcome, Error? Error = null)
{
    /// <summary>The error message with the field errors, e.g. "The Person record is invalid. surname: Required."</summary>
    public string? Message => Error is null ? null
        : string.Join(' ', [Error.Message, .. (Error.Details ?? new Dictionary<string, string[]>()).Select(d => $"{d.Key}: {string.Join(' ', d.Value)}")]);
}

internal sealed record ImportReport(string Type, string Source, IReadOnlyList<string> Warnings, IReadOnlyList<ImportRowResult> Rows)
{
    public int Count(ImportRowOutcome outcome) => Rows.Count(r => r.Outcome == outcome);
}

/// <summary>
/// Imports one CSV file of one type, row by row through the type's upsert (design D6). A failing row is reported and
/// the next row continues. The report is returned, not stored (that comes with add-api-and-import).
/// </summary>
internal sealed class AcademicImporter(AcademicDbContext db, IOptions<AcademicImportOptions> options)
{
    public const string InvalidSourceCode = "Academic.Import.InvalidSource";
    private const int ClearEvery = 500;

    public async Task<Result<ImportReport>> ImportAsync(AcademicType type, string source, Stream csv, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(type);
        source = source?.Trim() ?? string.Empty;
        if (source.Length is 0 or > ExchangeableEntity.SourceMaxLength)
        {
            var message = $"Enter a source of at most {ExchangeableEntity.SourceMaxLength} characters.";
            return Error.Validation(InvalidSourceCode, message, new Dictionary<string, string[]> { ["source"] = [message] });
        }

        var file = await CsvReader.ReadAsync(csv, type, options.Value, cancellationToken);
        if (file.IsFailure)
        {
            return file.Error!;
        }

        var results = new List<ImportRowResult>(file.Value!.Rows.Count);
        foreach (var row in file.Value.Rows)
        {
            var externalId = row.Fields.TryGetValue(type.KeyField, out var key) ? key : null;
            var result = await type.UpsertAsync(db, source, row.Fields, cancellationToken);
            results.Add(result.IsSuccess
                ? new ImportRowResult(row.Line, externalId, result.Value switch
                {
                    UpsertOutcome.Created => ImportRowOutcome.Created,
                    UpsertOutcome.Updated => ImportRowOutcome.Updated,
                    _ => ImportRowOutcome.Unchanged,
                })
                : new ImportRowResult(row.Line, externalId, ImportRowOutcome.Failed, result.Error));
            if (results.Count % ClearEvery == 0)
            {
                db.ChangeTracker.Clear();
            }
        }

        return new ImportReport(type.Name, source, file.Value.Warnings, results);
    }
}
