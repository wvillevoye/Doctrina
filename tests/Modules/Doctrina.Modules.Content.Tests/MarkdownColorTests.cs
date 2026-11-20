using Doctrina.Modules.Content.Application;

namespace Doctrina.Modules.Content.Tests;

// Spec course-content, requirement "Pagina's worden in Markdown geschreven en veilig weergegeven" (colours, change
// add-markdown-toolbar): {name}text{/name} is a theme colour through a CSS class; nothing else becomes a colour.
public sealed class PaginasWordenInMarkdownGeschrevenEnVeiligWeergegeven_Kleur
{
    [Fact]
    public void KleurWordtWeergegeven()
    {
        var html = Markdown.ToSafeHtml("{danger}Deadline{/danger} is Friday");

        Assert.Contains("<span class=\"md-color md-color--danger\">Deadline</span> is Friday", html, StringComparison.Ordinal);
        Assert.DoesNotContain("style=", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("accent")]
    [InlineData("blue")]
    [InlineData("lightblue")]
    [InlineData("success")]
    [InlineData("warning")]
    [InlineData("danger")]
    [InlineData("muted")]
    public void AlleVasteNamenWerken(string name) =>
        Assert.Contains($"<span class=\"md-color md-color--{name}\">x</span>", Markdown.ToSafeHtml($"{{{name}}}x{{/{name}}}"), StringComparison.Ordinal);

    [Theory]
    [InlineData("left")]
    [InlineData("center")]
    [InlineData("right")]
    public void UitlijningWordtWeergegeven(string name)
    {
        var html = Markdown.ToSafeHtml($"{{{name}}}Welcome{{/{name}}}");

        Assert.Contains($"<span class=\"md-align md-align--{name}\">Welcome</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("style=", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KleurOmOpgemaakteTekst()
    {
        var html = Markdown.ToSafeHtml("{accent}**Read** this{/accent}");

        Assert.Contains("<span class=\"md-color md-color--accent\"><strong>Read</strong> this</span>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{cyan}x{/cyan}")]
    [InlineData("{danger}open")]
    [InlineData("close{/danger}")]
    [InlineData("{danger}x{/accent}")]
    public void OnbekendeOfOngeslotenKleurBlijftTekst(string text)
    {
        var html = Markdown.ToSafeHtml(text);

        Assert.DoesNotContain("md-color", html, StringComparison.Ordinal);
        Assert.Contains(text, html, StringComparison.Ordinal);
    }

    [Fact]
    public void KleurEnUitlijningCombineren()
    {
        var html = Markdown.ToSafeHtml("{center}{danger}Hi{/danger}{/center}");

        Assert.Contains("<span class=\"md-align md-align--center\"><span class=\"md-color md-color--danger\">Hi</span></span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void KleurMagGeenGrensVanOpmaakOverschrijden()
    {
        var html = Markdown.ToSafeHtml("**{danger}bold** after{/danger}");

        Assert.DoesNotContain("md-color", html, StringComparison.Ordinal);
        Assert.Contains("{danger}bold", html, StringComparison.Ordinal);
    }

    [Fact]
    public void KleurInCodeBlijftTekst()
    {
        var inline = Markdown.ToSafeHtml("`{danger}x{/danger}`");
        var block = Markdown.ToSafeHtml("```\n{danger}x{/danger}\n```");

        Assert.Contains("<code>{danger}x{/danger}</code>", inline, StringComparison.Ordinal);
        Assert.DoesNotContain("md-color", block, StringComparison.Ordinal);
        Assert.Contains("{danger}x{/danger}", block, StringComparison.Ordinal);
    }

    [Fact]
    public void KleurInEenKopEnLijst()
    {
        var html = Markdown.ToSafeHtml("# {success}Done{/success}\n\n- {muted}note{/muted}");

        Assert.Contains("<span class=\"md-color md-color--success\">Done</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"md-color md-color--muted\">note</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlBlijftUitgeschakeldBinnenEenKleur()
    {
        var html = Markdown.ToSafeHtml("{danger}<script>alert(1)</script>{/danger}");

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("md-color--danger", html, StringComparison.Ordinal);
    }
}
