using System.Collections.Concurrent;
using System.Reflection;

namespace Doctrina.UI.Icons;

/// <summary>The embedded Lucide icons (design D6). Returns the content of the SVG without the outer element.</summary>
public static class DcIconSet
{
    private const string Prefix = "Doctrina.UI.Icons.";
    private const string Suffix = ".svg";

    private static readonly Assembly Assembly = typeof(DcIconSet).Assembly;
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    /// <summary>Names of all available icons, sorted.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        .. Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(Suffix, StringComparison.Ordinal))
            .Select(n => n[Prefix.Length..^Suffix.Length])
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>The paths etc. inside the <c>&lt;svg&gt;</c> element of icon <paramref name="name"/>.</summary>
    /// <exception cref="InvalidOperationException">The icon does not exist (developer error).</exception>
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
