using System.Text.RegularExpressions;

// Spec theming (change add-sidebar-navigation), requirement "De zijbalk heeft eigen tokens met voldoende contrast"; design D4, D5.
namespace Doctrina.UI.Tests.Theming;

public sealed partial class DeZijbalkHeeftEigenTokensMetVoldoendeContrast
{
    public static TheoryData<string> Themas => new() { TokenFile.Light, TokenFile.Dark };

    [Theory]
    [MemberData(nameof(Themas))]
    public void TekstcontrastInDeZijbalk(string mixin)
    {
        var theme = TokenFile.Theme(mixin);

        foreach (var text in new[] { "--dc-sidebar-text", "--dc-sidebar-text-muted" })
        {
            foreach (var background in new[] { "--dc-sidebar-bg", "--dc-sidebar-hover", "--dc-sidebar-active-bg" })
            {
                // The muted text is only used on the sidebar background and on the hover/active background of the link.
                var ratio = TokenFile.Contrast(theme, text, background);
                Assert.True(ratio >= 4.5, $"{mixin}: {text} op {background} heeft contrast {ratio:F2}:1.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Themas))]
    public void IndicatorVanHetActieveItemValtOp(string mixin)
    {
        var theme = TokenFile.Theme(mixin);

        // The indicator sits on the sidebar background (bar next to the active item); the focus ring on the same background.
        foreach (var foreground in new[] { "--dc-sidebar-indicator", "--dc-focus" })
        {
            var ratio = TokenFile.Contrast(theme, foreground, "--dc-sidebar-bg");
            Assert.True(ratio >= 3, $"{mixin}: {foreground} op --dc-sidebar-bg heeft contrast {ratio:F2}:1.");
        }
    }

    [Fact]
    public void ZijbalkstijlenBevattenGeenLetterlijkeKleuren()
    {
        foreach (var file in new[] { "DcAppShell.razor.css", "DcNavMenu.razor.css" })
        {
            var css = File.ReadAllText(Path.Combine(RepoPaths.Ui, "Components", file));

            Assert.False(LiteralColor().IsMatch(css), $"{file} bevat een letterlijke kleur.");
        }
    }

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\b(rgb|rgba|hsl|hsla)\(")]
    private static partial Regex LiteralColor();
}
