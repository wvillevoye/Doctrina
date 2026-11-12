using System.Text.RegularExpressions;

namespace Doctrina.Architecture.Tests.Rules;

/// <summary>
/// Regel "geen ruwe Bootstrap-classes in modules" (huisregel 4): modules gebruiken <c>Doctrina.UI</c>-componenten en
/// <c>--dc-*</c>-tokens. Herkent de gangbare Bootstrap 5.3-classes in <c>class</c>-attributen van <c>.razor</c>-bestanden.
/// </summary>
internal static partial class RawBootstrap
{
    public static IReadOnlyList<string> Violations(string modulesRoot)
    {
        if (!Directory.Exists(modulesRoot))
        {
            return [];
        }

        var violations = new List<string>();
        foreach (var file in Directory.EnumerateFiles(modulesRoot, "*.razor", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match attribute in ClassAttribute().Matches(lines[i]))
                {
                    var found = attribute.Groups["value"].Value
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Where(c => BootstrapClass().IsMatch(c))
                        .ToList();
                    if (found.Count > 0)
                    {
                        violations.Add($"{Path.GetRelativePath(modulesRoot, file)}:{i + 1}: ruwe Bootstrap-class(es) {string.Join(", ", found)}; gebruik een Dc*-component.");
                    }
                }
            }
        }

        return violations;
    }

    [GeneratedRegex("""\bclass\s*=\s*["'](?<value>[^"']*)["']""")]
    private static partial Regex ClassAttribute();

    [GeneratedRegex("""^(btn(-.+)?|container(-.+)?|row|col(-.+)?|g[xy]?-\d|gap-\d|[mp][tbsexy]?-(\d|auto)|d-.+|flex-.+|justify-content-.+|align-(items|self|content)-.+|text-.+|bg-.+|border(-.+)?|rounded(-.+)?|shadow(-.+)?|alert(-.+)?|card(-.+)?|badge|form-.+|input-group(-.+)?|table(-.+)?|nav(bar)?(-.+)?|list-group(-.+)?|modal(-.+)?|dropdown(-.+)?|fw-.+|fs-\d|lh-.+|w-\d+|h-\d+|visually-hidden(-focusable)?)$""")]
    private static partial Regex BootstrapClass();
}
