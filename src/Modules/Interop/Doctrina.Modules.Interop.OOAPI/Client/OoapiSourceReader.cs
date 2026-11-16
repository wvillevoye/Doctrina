using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Text.Json;
using Doctrina.Modules.Academic.Contracts;

namespace Doctrina.Modules.Interop.OOAPI.Client;

/// <summary>
/// The records of one OOAPI v5 source as import records, in dependency order (design D3, D5 of add-ooapi-consumer):
/// organizations, academic sessions, courses, the offerings of every course, persons, the associations of every offering.
/// References are OOAPI ids of the source; they are turned into the <c>primaryCode</c> of the record they point to, which is what
/// the import resolves. A reference to a record the source did not list stays as it is, so the report shows it as not found.
/// </summary>
internal sealed partial class OoapiSourceReader(OoapiClient client, OoapiOptions options, ICollection<string> warnings)
{
    /// <summary>Offerings are listed per course and, without <c>since</c>, only from today on (OOAPI v5 release 5.0).</summary>
    internal const string Since = "1900-01-01";

    private readonly Dictionary<string, string> _codes = new(StringComparer.OrdinalIgnoreCase);

    public async IAsyncEnumerable<ImportRecord> ReadAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var line = 0;
        foreach (var o in ParentFirst(await client.ItemsAsync("organizations", "organizations", ct).ToListAsync(ct), "organizationId"))
        {
            var (_, code) = Identify(o, "organizationId");
            yield return Record("organizations", ++line, ("organizationId", code),
                ("name", Text(o, "name")), ("shortName", Str(o, "shortName")), ("organizationType", OrganizationType(Str(o, "organizationType"))),
                ("parentOrganizationId", Ref(o, "parent")));
        }

        line = 0;
        foreach (var s in ParentFirst(await client.ItemsAsync("academic-sessions", "academic-sessions", ct).ToListAsync(ct), "academicSessionId"))
        {
            var (_, code) = Identify(s, "academicSessionId");
            yield return Record("academic-sessions", ++line, ("academicSessionId", code),
                ("name", Text(s, "name")), ("academicSessionType", Str(s, "academicSessionType")?.ToLowerInvariant()),
                ("startDate", Date(Str(s, "startDate"))), ("endDate", Date(Str(s, "endDate"))), ("parentAcademicSessionId", Ref(s, "parent")));
        }

        line = 0;
        var courses = new List<(string Id, string? Code)>();
        await foreach (var c in client.ItemsAsync("courses", "courses", ct))
        {
            var (id, code) = Identify(c, "courseId");
            courses.Add((id, code));
            yield return Record("courses", ++line, ("courseId", code),
                ("name", Text(c, "name")), ("abbreviation", Str(c, "abbreviation")), ("description", Text(c, "description")),
                ("organizationId", Ref(c, "organization")));
        }

        line = 0;
        var offerings = new List<(string Id, string? Code)>();
        foreach (var (courseId, courseCode) in courses.Where(c => c.Id.Length > 0))
        {
            await foreach (var o in client.ItemsAsync($"courses/{Uri.EscapeDataString(courseId)}/offerings", "offerings", ct, ("since", Since)))
            {
                var (id, code) = Identify(o, "offeringId");
                offerings.Add((id, code));
                yield return Record("course-offerings", ++line, ("offeringId", code),
                    ("name", Text(o, "name")), ("courseId", courseCode), ("academicSessionId", Ref(o, "academicSession")),
                    ("startDate", Date(Str(o, "startDate"))), ("endDate", Date(Str(o, "endDate"))));
            }
        }

        line = 0;
        await foreach (var p in client.ItemsAsync("persons", "persons", ct))
        {
            var (_, code) = Identify(p, "personId");
            yield return Record("persons", ++line, ("personId", code),
                ("givenName", Str(p, "givenName")), ("surnamePrefix", Str(p, "surnamePrefix")), ("surname", Str(p, "surname")),
                ("displayName", Str(p, "displayName")), ("mail", Str(p, "mail")));
        }

        line = 0;
        var skipped = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (offeringId, offeringCode) in offerings.Where(o => o.Id.Length > 0))
        {
            await foreach (var a in client.ItemsAsync($"offerings/{Uri.EscapeDataString(offeringId)}/associations", "associations", ct))
            {
                var type = Str(a, "associationType");
                if (type != "courseOfferingAssociation")
                {
                    Skip(skipped, $"of type {type ?? "(none)"}");
                    continue;
                }

                if (Str(a, "state") != "associated")
                {
                    Skip(skipped, $"with state {Str(a, "state") ?? "(none)"}");
                    continue;
                }

                // The association key is its own id: the standard gives an association no primaryCode.
                yield return Record("associations", ++line, ("associationId", Str(a, "associationId")),
                    ("personId", Ref(a, "person")), ("offeringId", offeringCode), ("role", Str(a, "role")?.Replace(' ', '-')));
            }
        }

        foreach (var (reason, count) in skipped)
        {
            warnings.Add($"associations: skipped {count} {reason}.");
        }
    }

    /// <summary>
    /// A record can only be imported after the record it has as <c>parent</c>, and a source lists in any order (Doctrina's own
    /// OOAPI lists by name). Parents come first; a parent the source does not list, or a cycle, does not hold anything back.
    /// </summary>
    private static List<JsonElement> ParentFirst(List<JsonElement> items, string idProperty)
    {
        var listed = items.Select(i => Str(i, idProperty)).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<JsonElement>(items.Count);
        var pending = items;
        while (pending.Count > 0)
        {
            var next = new List<JsonElement>();
            foreach (var item in pending)
            {
                var parent = RawRef(item, "parent");
                if (parent is null || !listed.Contains(parent) || done.Contains(parent))
                {
                    ordered.Add(item);
                    if (Str(item, idProperty) is { } id)
                    {
                        done.Add(id);
                    }
                }
                else
                {
                    next.Add(item);
                }
            }

            if (next.Count == pending.Count)
            {
                ordered.AddRange(next); // a cycle: the import reports what it cannot resolve
                break;
            }

            pending = next;
        }

        return ordered;
    }

    private static void Skip(SortedDictionary<string, int> skipped, string reason) =>
        skipped[reason] = skipped.GetValueOrDefault(reason) + 1;

    /// <summary>The source id (empty if there is none) and primaryCode of a record; remembers the pair so references to it can be translated.</summary>
    private (string Id, string? Code) Identify(JsonElement element, string idProperty)
    {
        var code = Code(element);
        var id = Str(element, idProperty) ?? string.Empty;
        if (id.Length > 0 && code is not null)
        {
            _codes[id] = code;
        }

        return (id, code);
    }

    private static ImportRecord Record(string type, int line, params (string Name, string? Value)[] fields) =>
        new(type, line, fields.Where(f => f.Value is not null).ToDictionary(f => f.Name, f => f.Value, StringComparer.OrdinalIgnoreCase));

    private static string? Code(JsonElement element) =>
        element.TryGetProperty("primaryCode", out var code) && code.ValueKind == JsonValueKind.Object ? Str(code, "code") : null;

    private static string? Str(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    /// <summary>A text typed by language: the one in <c>Ooapi:Language</c>, else the first; a plain string is taken as it is.</summary>
    private string? Text(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? first = null;
        foreach (var entry in value.EnumerateArray())
        {
            var text = Str(entry, "value");
            first ??= text;
            if (string.Equals(Str(entry, "language"), options.Language, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }
        }

        return first;
    }

    /// <summary>A reference is an id or an object with that id (OOAPI lets the source expand it); it becomes the primaryCode of the record it points to.</summary>
    private string? Ref(JsonElement element, string property) =>
        RawRef(element, property) is { } id ? _codes.GetValueOrDefault(id, id) : null;

    /// <summary>The id of the source in a reference.</summary>
    private static string? RawRef(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object => value.EnumerateObject().Where(p => p.Name.EndsWith("Id", StringComparison.Ordinal) && p.Value.ValueKind == JsonValueKind.String)
                .Select(p => p.Value.GetString()).FirstOrDefault(),
            _ => null,
        };
    }

    private static string? OrganizationType(string? type) => type?.ToLowerInvariant() is "institute" ? "institution" : type?.ToLowerInvariant();

    /// <summary>OOAPI dates are full dates, but a date-time is cut to its date.</summary>
    private static string? Date(string? value) => value is not null && DateStart().Match(value) is { Success: true } m ? m.Value : value;

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}")]
    private static partial Regex DateStart();
}
