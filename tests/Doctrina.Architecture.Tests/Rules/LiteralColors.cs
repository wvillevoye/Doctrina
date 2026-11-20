using System.Text.RegularExpressions;

namespace Doctrina.Architecture.Tests.Rules;

/// <summary>
/// Rule "no literal colours outside Doctrina.UI" (house rule 4, spec ui-components and app-shell): colours come from
/// <c>--dc-*</c> tokens. Checks <c>.css</c>/<c>.razor.css</c> in full and, in <c>.razor</c>, the <c>style</c> attributes
/// and <c>&lt;style&gt;</c> blocks (so anchors like <c>href="#add"</c> don't raise a false alarm).
/// </summary>
internal static partial class LiteralColors
{
    public static IReadOnlyList<string> Violations(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        var sep = Path.DirectorySeparatorChar;
        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".css", StringComparison.Ordinal) || f.EndsWith(".razor", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{sep}bin{sep}", StringComparison.Ordinal)
                && !f.Contains($"{sep}obj{sep}", StringComparison.Ordinal)
                // White-label themes of an installation (docs/ontwikkelen/vormgeving.md) contain token values by design.
                && !f.Contains($"{sep}wwwroot{sep}theme{sep}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        var violations = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            var razor = file.EndsWith(".razor", StringComparison.Ordinal);
            var inStyle = false;
            for (var i = 0; i < lines.Length; i++)
            {
                var checkedText = razor ? StyledParts(lines[i], ref inStyle) : CssComment().Replace(lines[i], string.Empty);
                foreach (Match color in LiteralColor().Matches(checkedText))
                {
                    violations.Add($"{Path.GetRelativePath(root, file)}:{i + 1}: letterlijke kleur '{color.Value.TrimEnd('(')}'; gebruik een --dc-token of een Dc*-component.");
                }
            }
        }

        return violations;
    }

    /// <summary>The parts of a line in a <c>.razor</c> file where CSS lives.</summary>
    private static string StyledParts(string line, ref bool inStyle)
    {
        var parts = new List<string>(StyleAttribute().Matches(line).Select(m => m.Groups["value"].Value));
        var rest = line;
        while (rest.Length > 0)
        {
            if (inStyle)
            {
                var end = rest.IndexOf("</style>", StringComparison.OrdinalIgnoreCase);
                parts.Add(end < 0 ? rest : rest[..end]);
                if (end < 0)
                {
                    break;
                }

                inStyle = false;
                rest = rest[(end + 8)..];
            }
            else
            {
                var start = rest.IndexOf("<style", StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                {
                    break;
                }

                inStyle = true;
                var close = rest.IndexOf('>', start);
                rest = close < 0 ? string.Empty : rest[(close + 1)..];
            }
        }

        return string.Join(' ', parts);
    }

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\b(?:rgba?|hsla?)\(")]
    private static partial Regex LiteralColor();

    [GeneratedRegex("""\bstyle\s*=\s*"(?<value>[^"]*)"|\bstyle\s*=\s*'(?<value>[^']*)'""")]
    private static partial Regex StyleAttribute();

    [GeneratedRegex(@"/\*.*?\*/")]
    private static partial Regex CssComment();
}
