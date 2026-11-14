using System.IO.Compression;
using System.Text;
using Doctrina.Core.Results;
using Microsoft.VisualBasic.FileIO;

namespace Doctrina.Modules.Academic.Application;

/// <summary>One CSV file of an upload, with its type.</summary>
internal sealed record ImportFile(string Name, AcademicType Type, byte[] Content);

/// <summary>
/// Turns an upload into CSV files with their type, in import order (change add-bulk-import, design D5): a single CSV
/// with a given type, or a zip with <c>manifest.csv</c> (<c>file,type</c>) or files named <c>&lt;type&gt;.csv</c>.
/// </summary>
internal static class ImportUpload
{
    public const string UnknownFileCode = "Academic.Import.UnknownFile";
    public const string UnknownTypeCode = "Academic.Import.UnknownType";
    public const string BadZipCode = "Academic.Import.BadZip";
    public const string ManifestName = "manifest.csv";

    public static bool IsZip(string name) => name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    public static Result<List<ImportFile>> Read(byte[] upload, string name, string? type, AcademicImportOptions limits)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(limits);
        if (!IsZip(name))
        {
            return AcademicTypes.Find(type ?? string.Empty) is { } single
                ? new List<ImportFile> { new(name, single, upload) }
                : UnknownType(type);
        }

        try
        {
            using var zip = new ZipArchive(new MemoryStream(upload), ZipArchiveMode.Read);
            var entries = zip.Entries
                .Where(e => e.Name.Length > 0 && !e.FullName.StartsWith("__MACOSX/", StringComparison.Ordinal))
                .Where(e => e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var manifestEntry = entries.FirstOrDefault(e => string.Equals(e.FullName, ManifestName, StringComparison.OrdinalIgnoreCase));
            var manifest = manifestEntry is null ? null : ReadManifest(Unpack(manifestEntry, limits.MaxFileSize));

            var files = new List<ImportFile>();
            long total = 0;
            foreach (var entry in entries.Where(e => e != manifestEntry))
            {
                var typeName = manifest is null
                    ? Path.GetFileNameWithoutExtension(entry.Name)
                    : manifest.GetValueOrDefault(entry.FullName) ?? manifest.GetValueOrDefault(entry.Name);
                if (typeName is null || AcademicTypes.Find(typeName) is not { } fileType)
                {
                    return CsvReader.FileError(UnknownFileCode,
                        $"Can't tell what {entry.FullName} contains: name it after its type (e.g. persons.csv) or list it in {ManifestName}.");
                }

                // Against zip bombs: at most MaxFileSize per file and four times that in total (design D5).
                var content = Unpack(entry, limits.MaxFileSize);
                total += content.Length;
                if (content.Length > limits.MaxFileSize || total > 4 * limits.MaxFileSize)
                {
                    return CsvReader.FileError(CsvReader.TooLargeCode, $"The files in the zip are larger than allowed ({limits.MaxFileSize / 1024 / 1024} MB per file).");
                }

                files.Add(new ImportFile(entry.FullName, fileType, content));
            }

            return files.Count == 0
                ? CsvReader.FileError(CsvReader.EmptyCode, "The zip contains no CSV files.")
                : files.OrderBy(f => IndexOf(f.Type)).ThenBy(f => f.Name, StringComparer.Ordinal).ToList();
        }
        catch (InvalidDataException)
        {
            return CsvReader.FileError(BadZipCode, "The file is not a valid zip.");
        }
    }

    public static Error UnknownType(string? type) =>
        CsvReader.FileError(UnknownTypeCode, $"Choose a type for the CSV file: {string.Join(", ", AcademicTypes.All.Select(t => t.Name))}.");

    private static int IndexOf(AcademicType type)
    {
        for (var i = 0; i < AcademicTypes.All.Count; i++)
        {
            if (AcademicTypes.All[i] == type)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    /// <summary>Unpacks at most <paramref name="max"/> + 1 bytes, so a too large file is noticed without unpacking all of it.</summary>
    private static byte[] Unpack(ZipArchiveEntry entry, long max)
    {
        using var source = entry.Open();
        using var target = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while (target.Length <= max && (read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, max + 1 - target.Length))) > 0)
        {
            target.Write(buffer, 0, read);
        }

        return target.ToArray();
    }

    /// <summary>File (as in the zip) → type, from the columns <c>file</c> and <c>type</c>.</summary>
    private static Dictionary<string, string> ReadManifest(byte[] content)
    {
        using var parser = new TextFieldParser(new StreamReader(new MemoryStream(content), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true,
        };
        parser.SetDelimiters(",", ";");
        var header = parser.ReadFields() ?? [];
        var file = Array.FindIndex(header, h => string.Equals(h, "file", StringComparison.OrdinalIgnoreCase));
        var type = Array.FindIndex(header, h => string.Equals(h, "type", StringComparison.OrdinalIgnoreCase));
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (file >= 0 && type >= 0 && !parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is not null && fields.Length > Math.Max(file, type) && fields[file].Length > 0)
            {
                result[fields[file]] = fields[type];
            }
        }

        return result;
    }
}
