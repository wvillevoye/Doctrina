using System.Text.RegularExpressions;

namespace Doctrina.UI.Tests.Theming;

/// <summary>Simple reader of the compiled <c>doctrina.css</c> (expanded or compressed).</summary>
internal static partial class CompiledCss
{
    private static readonly Lazy<string> Text = new(() =>
    {
        Assert.True(File.Exists(RepoPaths.CompiledCss), $"{RepoPaths.CompiledCss} ontbreekt; bouw Doctrina.UI (AspNetCore.SassCompiler).");
        return Comment().Replace(File.ReadAllText(RepoPaths.CompiledCss), string.Empty);
    });

    public static string All => Text.Value;

    /// <summary>Declarations (property → value, last wins) of all rules with exactly this selector, outside at-rules.</summary>
    public static IReadOnlyDictionary<string, string> Declarations(string selector) =>
        Declarations(TopLevel(All), selector);

    /// <summary>Content of all <c>@media</c> blocks with this condition, one after another.</summary>
    public static string MediaBlock(string condition)
    {
        var pattern = @"@media\s*" + Regex.Escape(condition).Replace(@"\ ", @"\s*", StringComparison.Ordinal) + @"\s*\{";
        var matches = Regex.Matches(All, pattern);
        Assert.True(matches.Count > 0, $"@media {condition} niet gevonden in doctrina.css");
        return string.Concat(matches.Select(m => Block(All, m.Index + m.Length - 1)));
    }

    public static IReadOnlyDictionary<string, string> Declarations(string css, string selector)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match rule in Rule().Matches(css))
        {
            var selectors = rule.Groups["selector"].Value.Split(',').Select(s => Whitespace().Replace(s.Trim(), " "));
            if (!selectors.Contains(selector, StringComparer.Ordinal))
            {
                continue;
            }

            foreach (var declaration in rule.Groups["body"].Value.Split(';'))
            {
                var colon = declaration.IndexOf(':', StringComparison.Ordinal);
                if (colon > 0)
                {
                    result[declaration[..colon].Trim()] = declaration[(colon + 1)..].Trim();
                }
            }
        }

        return result;
    }

    /// <summary>The CSS without at-rules (<c>@media</c>, <c>@font-face</c>, <c>@charset</c>, …).</summary>
    private static string TopLevel(string css)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < css.Length; i++)
        {
            if (css[i] != '@')
            {
                builder.Append(css[i]);
                continue;
            }

            var open = css.IndexOf('{', i);
            var semicolon = css.IndexOf(';', i);
            i = semicolon >= 0 && (open < 0 || semicolon < open)
                ? semicolon
                : open + Block(css, open).Length + 1;
        }

        return builder.ToString();
    }

    /// <summary>Text between the brace at <paramref name="open"/> and the matching closing brace.</summary>
    private static string Block(string css, int open)
    {
        var depth = 0;
        for (var i = open; i < css.Length; i++)
        {
            depth += css[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return css[(open + 1)..i];
            }
        }

        throw new InvalidOperationException("Ongesloten blok in doctrina.css");
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}")]
    private static partial Regex Rule();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
