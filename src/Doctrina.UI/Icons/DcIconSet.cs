using System.Collections.Concurrent;
using System.Reflection;

namespace Doctrina.UI.Icons;

/// <summary>De ingesloten Lucide-iconen (design D6). Geeft de inhoud van de SVG zonder het buitenste element.</summary>
public static class DcIconSet
{
    private const string Prefix = "Doctrina.UI.Icons.";
    private const string Suffix = ".svg";

    private static readonly Assembly Assembly = typeof(DcIconSet).Assembly;
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    /// <summary>Namen van alle beschikbare iconen, gesorteerd.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        .. Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(Suffix, StringComparison.Ordinal))
            .Select(n => n[Prefix.Length..^Suffix.Length])
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>De paden e.d. binnen het <c>&lt;svg&gt;</c>-element van icoon <paramref name="name"/>.</summary>
    /// <exception cref="InvalidOperationException">Het icoon bestaat niet (fout van de ontwikkelaar).</exception>
    public static string Content(string name) => Cache.GetOrAdd(name, Load);

    private static string Load(string name)
    {
        using var stream = Assembly.GetManifestResourceStream(Prefix + name + Suffix)
            ?? throw new InvalidOperationException(
                $"Onbekend icoon '{name}'. Beschikbaar: {string.Join(", ", Names)}. "
                + "Voeg een Lucide-icoon toe in src/Doctrina.UI/Icons (zie VERSION aldaar).");
        using var reader = new StreamReader(stream);
        var svg = reader.ReadToEnd();
        var start = svg.IndexOf('>', svg.IndexOf("<svg", StringComparison.Ordinal)) + 1;
        var end = svg.LastIndexOf("</svg>", StringComparison.Ordinal);
        return string.Join(string.Empty, svg[start..end].Split('\n').Select(l => l.Trim()));
    }
}
