using Doctrina.Modules.Content.Application;

namespace Doctrina.Modules.Content.Tests;

// Spec course-content (change extend-markdown-toolbar): strikethrough and task lists, still without HTML.
public sealed class PaginasKunnenDoorhalenEnTakenlijstenGebruiken
{
    [Fact]
    public void DoorhalenWordtWeergegeven() => Assert.Contains("<del>old</del>", Markdown.ToSafeHtml("~~old~~"));

    [Fact]
    public void TakenlijstWordtWeergegeven()
    {
        var html = Markdown.ToSafeHtml("- [x] Done\n- [ ] Todo");

        Assert.Equal(2, html.Split("type=\"checkbox\"").Length - 1);
        Assert.Equal(2, html.Split("disabled=\"disabled\"").Length - 1);
        Assert.Equal(1, html.Split("checked=\"checked\"").Length - 1);
    }

    [Fact]
    public void HtmlBlijftUitNaastDeNieuweOpmaak() =>
        Assert.DoesNotContain("<script", Markdown.ToSafeHtml("~~x~~ <script>alert(1)</script>"));
}
