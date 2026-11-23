using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Doctrina.Modules.Content.Application;

/// <summary>
/// Markdown of a page to HTML that is safe to show (design D7 of add-content): HTML in the text is shown as text, and
/// links and images only keep an address that starts with http://, https:// or (links) mailto:. Text between
/// <c>{accent}</c> and <c>{/accent}</c> (also blue, lightblue, success, warning, danger, muted, purple, teal, pink, brown, olive) or <c>{center}</c> (also left, right) gets a theme colour through a CSS class
/// (design D2 of add-markdown-toolbar); other or unclosed markers stay plain text.
/// </summary>
internal static partial class Markdown
{
    /// <summary>The colour names that can be used in <c>{name}…{/name}</c>.</summary>
    public static readonly IReadOnlyList<string> ColorNames = ["accent", "blue", "lightblue", "success", "warning", "danger", "muted", "purple", "teal", "pink", "brown", "olive"];

    /// <summary>The alignment names that can be used in <c>{name}…{/name}</c>.</summary>
    public static readonly IReadOnlyList<string> AlignNames = ["left", "center", "right"];

    private static readonly MarkdownPipeline Pipeline = BuildPipeline();

    private static MarkdownPipeline BuildPipeline()
    {
        var builder = new MarkdownPipelineBuilder().UsePipeTables().UseAutoLinks().UseEmphasisExtras(EmphasisExtraOptions.Strikethrough).UseTaskLists().DisableHtml();
        builder.Extensions.Add(new ColorExtension());
        return builder.Build();
    }

    [GeneratedRegex(@"\{(/?)(accent|blue|lightblue|success|warning|danger|muted|purple|teal|pink|brown|olive|left|center|right)\}")]
    private static partial Regex ColorMarker();

    public static string ToSafeHtml(string? text)
    {
        var document = Markdig.Markdown.Parse(text ?? string.Empty, Pipeline);
        // The inlines of a paragraph or heading hang off its block, so those containers are listed explicitly.
        var containers = document.Descendants<LeafBlock>().Where(b => b.Inline is not null).Select(b => b.Inline!)
            .Concat(document.Descendants<ContainerInline>()).Distinct().ToList();
        foreach (var container in containers)
        {
            ApplyColors(container);
        }

        foreach (var inline in document.Descendants<Inline>())
        {
            switch (inline)
            {
                case LinkInline link when !Allowed(link.Url, link.IsImage):
                    link.Url = string.Empty;
                    break;
                case AutolinkInline auto when !Allowed(auto.Url, image: false):
                    auto.Url = string.Empty;
                    break;
            }
        }

        return document.ToHtml(Pipeline);
    }

    private static bool Allowed(string? url, bool image) =>
        url is not null
        && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || (!image && url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)));


    // Replaces the markers in the literals of one container by ColorInline elements. Only markers in the same
    // container are paired, so a colour never crosses the edge of bold, a link or a block. The children are
    // detached first and then appended again in their new places, so no node is ever in two lists at once.
    private static void ApplyColors(ContainerInline container)
    {
        var children = container.ToList();
        var tokens = new List<Inline>();
        foreach (var child in children)
        {
            if (child is not LiteralInline literal)
            {
                tokens.Add(child);
                continue;
            }

            var content = literal.Content.ToString();
            var position = 0;
            foreach (var match in ColorMarker().Matches(content).Cast<Match>())
            {
                if (match.Index > position)
                {
                    tokens.Add(new LiteralInline(content[position..match.Index]));
                }

                tokens.Add(new ColorMarkerInline(match.Groups[2].Value, closing: match.Groups[1].Length > 0, match.Value));
                position = match.Index + match.Length;
            }

            tokens.Add(position == 0 ? literal : new LiteralInline(content[position..]));
        }

        if (!tokens.Any(t => t is ColorMarkerInline))
        {
            return;
        }

        foreach (var child in children)
        {
            child.Remove();
        }

        Build(tokens, 0, tokens.Count, container);
    }

    // Pairs each opening marker with the next closing marker of the same name inside [from, to) and builds the
    // contents recursively, so markers nest ({center}{danger}x{/danger}{/center}). What does not pair stays text.
    private static void Build(List<Inline> tokens, int from, int to, ContainerInline target)
    {
        for (var i = from; i < to; i++)
        {
            if (tokens[i] is not ColorMarkerInline marker)
            {
                target.AppendChild(tokens[i]);
                continue;
            }

            var close = marker.Closing ? -1 : tokens.FindIndex(i + 1, to - i - 1, t => t is ColorMarkerInline { Closing: true } c && c.Name == marker.Name);
            if (close < 0)
            {
                target.AppendChild(new LiteralInline(marker.Original)); // unclosed or stray: stays text
                continue;
            }

            var color = new ColorInline(marker.Name);
            Build(tokens, i + 1, close, color);
            target.AppendChild(color);
            i = close;
        }
    }

    /// <summary>A marker found in the text, before it is paired.</summary>
    private sealed class ColorMarkerInline(string name, bool closing, string original) : LeafInline
    {
        public string Name { get; } = name;
        public bool Closing { get; } = closing;
        public string Original { get; } = original;
    }

    /// <summary>Text with a theme colour, written as a <c>span</c> with a class and nothing else.</summary>
    private sealed class ColorInline(string name) : ContainerInline
    {
        public string Name { get; } = name;
    }

    private sealed class ColorRenderer : HtmlObjectRenderer<ColorInline>
    {
        protected override void Write(HtmlRenderer renderer, ColorInline obj)
        {
            renderer.Write(AlignNames.Contains(obj.Name) ? "<span class=\"md-align md-align--" : "<span class=\"md-color md-color--").Write(obj.Name).Write("\">");
            renderer.WriteChildren(obj);
            renderer.Write("</span>");
        }
    }

    private sealed class ColorExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is HtmlRenderer html)
            {
                html.ObjectRenderers.AddIfNotAlready<ColorRenderer>();
            }
        }
    }
}
