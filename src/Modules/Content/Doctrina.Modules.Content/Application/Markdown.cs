using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Doctrina.Modules.Content.Application;

/// <summary>
/// Markdown of a page to HTML that is safe to show (design D7 of add-content): HTML in the text is shown as text, and
/// links and images only keep an address that starts with http://, https:// or (links) mailto:.
/// </summary>
internal static class Markdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseAutoLinks().DisableHtml().Build();

    public static string ToSafeHtml(string? text)
    {
        var document = Markdig.Markdown.Parse(text ?? string.Empty, Pipeline);
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
}
