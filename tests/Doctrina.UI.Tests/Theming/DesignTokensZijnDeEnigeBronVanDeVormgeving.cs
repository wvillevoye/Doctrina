using System.Text.RegularExpressions;

// Spec theming, requirement "Design tokens zijn de enige bron van de vormgeving"; taken 2.2 en 2.3, design D2.
namespace Doctrina.UI.Tests.Theming;

public sealed partial class DesignTokensZijnDeEnigeBronVanDeVormgeving
{
    [Fact]
    public void GewijzigdAccenttokenWerktDoorInKnoppen()
    {
        // Een stylesheet die alleen --dc-accent wijzigt, verandert de knop als die zijn achtergrond (via Bootstrap's
        // --bs-btn-bg) uit dat token haalt en geen letterlijke kleur heeft die het overschrijft.
        var button = CompiledCss.Declarations(".btn");
        var primary = CompiledCss.Declarations(".btn-primary");

        Assert.Equal("var(--bs-btn-bg)", button["background-color"]);
        Assert.Equal("var(--dc-accent)", primary["--bs-btn-bg"]);
        Assert.Equal("var(--dc-accent)", primary["--bs-btn-border-color"]);
        Assert.Equal("var(--dc-accent-contrast)", primary["--bs-btn-color"]);
        Assert.StartsWith("color-mix(in srgb, var(--dc-accent)", primary["--bs-btn-hover-bg"], StringComparison.Ordinal);
        Assert.DoesNotContain("background-color", primary.Keys);
    }

    [Fact]
    public void BootstrapVolgtDeTokensOokInGenesteThemas()
    {
        // Brug op :root én op [data-dc-theme]: een container met een ander thema (stijlgids) krijgt de juiste waarden.
        var bridge = CompiledCss.Declarations("[data-dc-theme]");

        Assert.Equal("var(--dc-bg)", bridge["--bs-body-bg"]);
        Assert.Equal("var(--dc-text)", bridge["--bs-body-color"]);
        Assert.Equal("var(--dc-focus)", bridge["--bs-focus-ring-color"]);
    }

    [Fact]
    public void GeenJavaScriptAfhankelijkeBootstrapOnderdelen()
    {
        // Taak 1.2 / design D1, D9, D10: native <dialog> en Popover API in plaats van Bootstrap-JS.
        foreach (var selector in new[] { ".modal", ".dropdown-menu", ".collapse:not(.show)", ".offcanvas", ".tooltip", ".popover" })
        {
            Assert.Empty(CompiledCss.Declarations(selector));
        }
    }

    [Fact]
    public void ComponentstijlenBevattenGeenLetterlijkeKleuren()
    {
        var sep = Path.DirectorySeparatorChar;
        var vendor = Path.Combine(RepoPaths.Styles, "vendor") + sep;
        var files = Directory.EnumerateFiles(RepoPaths.Ui, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor.css", StringComparison.Ordinal) || f.EndsWith(".scss", StringComparison.Ordinal))
            .Where(f => !f.StartsWith(vendor, StringComparison.Ordinal)
                && !f.Contains($"{sep}obj{sep}", StringComparison.Ordinal)
                && !f.Contains($"{sep}bin{sep}", StringComparison.Ordinal)
                && Path.GetFileName(f) != "_tokens.scss")
            .ToList();

        var violations = files
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => LiteralColor().IsMatch(l.Text))
            .Select(l => $"{Path.GetRelativePath(RepoPaths.Ui, l.File)}:{l.Line}: {l.Text.Trim()}")
            .ToList();

        Assert.Contains(files, f => Path.GetFileName(f) == "_bootstrap-bridge.scss");
        Assert.Empty(violations);
    }

    /// <summary>#rgb, #rrggbb(aa), rgb(a)(…), hsl(a)(…); <c>#{…}</c> (Sass-interpolatie) telt niet.</summary>
    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(")]
    private static partial Regex LiteralColor();
}
