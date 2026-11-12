using System.Text.RegularExpressions;

namespace Doctrina.UI.Tests.Theming;

/// <summary>Eenvoudige lezer van de gecompileerde <c>doctrina.css</c> (uitgebreid of gecomprimeerd).</summary>
internal static partial class CompiledCss
{
    private static readonly Lazy<string> Text = new(() =>
    {
        Assert.True(File.Exists(RepoPaths.CompiledCss), $"{RepoPaths.CompiledCss} ontbreekt; bouw Doctrina.UI (AspNetCore.SassCompiler).");
        return Comment().Replace(File.ReadAllText(RepoPaths.CompiledCss), string.Empty);
    });

    public static string All => Text.Value;

    /// <summary>Declaraties (eigenschap → waarde, laatste wint) van alle regels met precies deze selector, buiten at-rules.</summary>
    public static IReadOnlyDictionary<string, string> Declarations(string selector) =>
        Declarations(TopLevel(All), selector);

    /// <summary>Inhoud van alle <c>@media</c>-blokken met deze voorwaarde, achter elkaar.</summary>
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

    /// <summary>De CSS zonder at-rules (<c>@media</c>, <c>@font-face</c>, <c>@charset</c>, …).</summary>
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

    /// <summary>Tekst tussen de accolade op <paramref name="open"/> en de bijbehorende sluitaccolade.</summary>
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
