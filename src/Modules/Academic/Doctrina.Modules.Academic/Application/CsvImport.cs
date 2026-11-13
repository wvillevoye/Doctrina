using System.Text;
using Doctrina.Core.Results;
using Microsoft.VisualBasic.FileIO;

namespace Doctrina.Modules.Academic.Application;

/// <summary>Limits of the synchronous import (<c>Academic:Import</c>), see design D6.</summary>
public sealed class AcademicImportOptions
{
    public const string Section = "Academic:Import";

    public int MaxRows { get; set; } = 10_000;

    public long MaxFileSize { get; set; } = 5 * 1024 * 1024;
}

/// <summary>A data row; <see cref="Line"/> is the line number in the file (the header is line 1).</summary>
internal sealed record CsvRow(int Line, IReadOnlyDictionary<string, string?> Fields);

internal sealed record CsvFile(IReadOnlyList<CsvRow> Rows, IReadOnlyList<string> Warnings);

/// <summary>
/// Reads a Doctrina CSV for one type (design D5): UTF-8 with or without BOM, <c>,</c> or <c>;</c> (from the header),
/// required header, columns by OOAPI name (case-insensitive). A missing required column or a file over the limits
/// rejects the whole file; unknown columns give a warning.
/// </summary>
internal static class CsvReader
{
    public const string EmptyCode = "Academic.Import.Empty";
    public const string MissingColumnsCode = "Academic.Import.MissingColumns";
    public const string TooLargeCode = "Academic.Import.TooLarge";

    public static async Task<Result<CsvFile>> ReadAsync(Stream stream, AcademicType type, AcademicImportOptions limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(limits);

        // Read at most one byte more than allowed, so a too large file is detected without reading all of it.
        var buffer = new MemoryStream();
        await CopyAtMostAsync(stream, buffer, limits.MaxFileSize + 1, cancellationToken);
        if (buffer.Length > limits.MaxFileSize)
        {
            return FileError(TooLargeCode, $"The file is larger than the maximum of {limits.MaxFileSize / 1024 / 1024} MB.");
        }

        buffer.Position = 0;
        using var text = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await text.ReadToEndAsync(cancellationToken);
        var firstLine = content.Split('\n', 2)[0];
        var delimiter = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ";" : ",";

        using var parser = new TextFieldParser(new StringReader(content)) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(delimiter);
        var header = parser.EndOfData ? null : parser.ReadFields();
        if (header is null || header.All(string.IsNullOrWhiteSpace))
        {
            return FileError(EmptyCode, "The file is empty or has no header row.");
        }

        var known = type.Fields.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = type.Fields.Where(f => f.Required && !header.Contains(f.Name, StringComparer.OrdinalIgnoreCase)).Select(f => f.Name).ToList();
        if (missing.Count > 0)
        {
            return FileError(MissingColumnsCode, $"Required column(s) missing: {string.Join(", ", missing)}.");
        }

        var warnings = header.Where(h => !string.IsNullOrWhiteSpace(h) && !known.Contains(h))
            .Select(h => $"Unknown column '{h}' is ignored.").ToList();

        var rows = new List<CsvRow>();
        while (!parser.EndOfData)
        {
            var line = (int)parser.LineNumber;
            var values = parser.ReadFields();
            if (values is null || values.All(string.IsNullOrEmpty))
            {
                continue;
            }

            if (rows.Count == limits.MaxRows)
            {
                return FileError(TooLargeCode, $"The file has more than the maximum of {limits.MaxRows} rows.");
            }

            var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < header.Length; i++)
            {
                if (known.Contains(header[i]))
                {
                    fields[header[i]] = i < values.Length ? values[i] : null;
                }
            }

            rows.Add(new CsvRow(line, fields));
        }

        return new CsvFile(rows, warnings);
    }

    /// <summary>A file-level error; the message is also a detail, so DcForm/DcResult show it under the title.</summary>
    internal static Error FileError(string code, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]> { ["file"] = [message] });

    private static async Task CopyAtMostAsync(Stream source, Stream target, long max, CancellationToken cancellationToken)
    {
        var chunk = new byte[81920];
        int read;
        while (target.Length < max && (read = await source.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, max - target.Length)), cancellationToken)) > 0)
        {
            await target.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
    }
}
