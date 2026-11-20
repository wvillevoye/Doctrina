using System.Globalization;
using System.Text.RegularExpressions;

namespace Doctrina.UI.Tests.Theming;

/// <summary>
/// Reads <c>Styles/_tokens.scss</c> (design D2: one token per line) and gives the colour value of each <c>--dc-*</c> token
/// per theme, with <c>#{$sass-variable}</c> and <c>var(--dc-…)</c> resolved.
/// </summary>
internal static partial class TokenFile
{
    public const string Light = "dc-light-colors";
    public const string Dark = "dc-dark-colors";

    private static readonly Lazy<string[]> Lines = new(() => File.ReadAllLines(RepoPaths.Tokens));

    /// <summary>All tokens of a theme: the theme-independent <c>:root</c> tokens plus the theme's mixin.</summary>
    public static IReadOnlyDictionary<string, string> Theme(string mixin)
    {
        var sass = Lines.Value.Select(l => SassVariable().Match(l)).Where(m => m.Success)
            .ToDictionary(m => m.Groups["name"].Value, m => m.Groups["value"].Value.Trim(), StringComparer.Ordinal);

        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var block in new[] { ":root {", $"@mixin {mixin} {{" })
        {
            var start = Array.FindIndex(Lines.Value, l => l.TrimEnd() == block);
            Assert.True(start >= 0, $"Blok '{block}' niet gevonden in _tokens.scss");
            for (var i = start + 1; i < Lines.Value.Length && Lines.Value[i].Trim() != "}"; i++)
            {
                var match = Token().Match(Lines.Value[i]);
                if (match.Success)
                {
                    tokens[match.Groups["name"].Value] = SassReference().Replace(match.Groups["value"].Value.Trim(), m => sass[m.Groups["name"].Value]);
                }
            }
        }

        return tokens;
    }

    /// <summary>Colour of a token as RGB, with <c>var(--dc-…)</c> references followed.</summary>
    public static (double R, double G, double B) Color(IReadOnlyDictionary<string, string> theme, string token)
    {
        var value = theme[token];
        for (var depth = 0; VarReference().Match(value) is { Success: true } reference; depth++)
        {
            Assert.True(depth < 10, $"Kringverwijzing bij {token}");
            value = theme[reference.Groups["name"].Value];
        }

        var hex = HexColor().Match(value);
        Assert.True(hex.Success, $"{token} is geen hexkleur: {value}");
        var digits = hex.Groups["hex"].Value;
        if (digits.Length == 3)
        {
            digits = string.Concat(digits.Select(c => $"{c}{c}"));
        }

        double Channel(int i) => int.Parse(digits.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
        return (Channel(0), Channel(1), Channel(2));
    }

    /// <summary>Contrast ratio according to WCAG 2.2 (relative luminance).</summary>
    public static double Contrast(IReadOnlyDictionary<string, string> theme, string foreground, string background)
    {
        var a = Luminance(Color(theme, foreground));
        var b = Luminance(Color(theme, background));
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance((double R, double G, double B) c)
    {
        static double Linear(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        return (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));
    }

    [GeneratedRegex("""^\$(?<name>[a-z0-9-]+):\s*(?<value>[^;]+);""")]
    private static partial Regex SassVariable();

    [GeneratedRegex("""^\s*(?<name>--dc-[a-z0-9-]+):\s*(?<value>[^;]+);""")]
    private static partial Regex Token();

    [GeneratedRegex("""#\{\$(?<name>[a-z0-9-]+)\}""")]
    private static partial Regex SassReference();

    [GeneratedRegex("""^var\((?<name>--dc-[a-z0-9-]+)\)$""")]
    private static partial Regex VarReference();

    [GeneratedRegex("""^#(?<hex>[0-9a-fA-F]{6}|[0-9a-fA-F]{3})$""")]
    private static partial Regex HexColor();
}
