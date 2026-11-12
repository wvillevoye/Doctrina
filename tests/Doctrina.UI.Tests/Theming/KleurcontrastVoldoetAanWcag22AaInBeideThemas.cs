// Spec theming, requirement "Kleurcontrast voldoet aan WCAG 2.2 AA in beide thema's"; taak 2.1, design D12.
namespace Doctrina.UI.Tests.Theming;

public sealed class KleurcontrastVoldoetAanWcag22AaInBeideThemas
{
    /// <summary>Tekst op achtergrond: gewone tekst, links, tekst op gekleurde vlakken en meldingen (≥ 4,5:1).</summary>
    public static TheoryData<string, string> TekstParen => new()
    {
        { "--dc-text", "--dc-bg" },
        { "--dc-text", "--dc-surface" },
        { "--dc-text", "--dc-surface-sunken" },
        { "--dc-text", "--dc-neutral-subtle" },
        { "--dc-heading", "--dc-bg" },
        { "--dc-heading", "--dc-surface" },
        { "--dc-text-muted", "--dc-bg" },
        { "--dc-text-muted", "--dc-surface" },
        { "--dc-text-muted", "--dc-surface-sunken" },
        { "--dc-link", "--dc-bg" },
        { "--dc-link", "--dc-surface" },
        { "--dc-link", "--dc-surface-sunken" },
        { "--dc-danger", "--dc-surface" },
        { "--dc-danger", "--dc-bg" },
        { "--dc-accent-contrast", "--dc-accent" },
        { "--dc-success-contrast", "--dc-success" },
        { "--dc-warning-contrast", "--dc-warning" },
        { "--dc-danger-contrast", "--dc-danger" },
        { "--dc-info-contrast", "--dc-info" },
        { "--dc-text", "--dc-success-subtle" },
        { "--dc-text", "--dc-warning-subtle" },
        { "--dc-text", "--dc-danger-subtle" },
        { "--dc-text", "--dc-info-subtle" },
    };

    /// <summary>Focus-indicator en grenzen van bedieningselementen op elke achtergrond (≥ 3:1).</summary>
    public static TheoryData<string, string> NietTekstParen => new()
    {
        { "--dc-focus", "--dc-bg" },
        { "--dc-focus", "--dc-surface" },
        { "--dc-focus", "--dc-surface-sunken" },
        { "--dc-border-strong", "--dc-bg" },
        { "--dc-border-strong", "--dc-surface" },
    };

    [Theory]
    [MemberData(nameof(TekstParen))]
    public void TekstcontrastInHetLichteThema(string foreground, string background) =>
        AssertContrast(TokenFile.Light, foreground, background, 4.5);

    [Theory]
    [MemberData(nameof(TekstParen))]
    public void TekstcontrastInHetDonkereThema(string foreground, string background) =>
        AssertContrast(TokenFile.Dark, foreground, background, 4.5);

    [Theory]
    [MemberData(nameof(NietTekstParen))]
    public void FocusIndicatorValtOp(string foreground, string background)
    {
        AssertContrast(TokenFile.Light, foreground, background, 3);
        AssertContrast(TokenFile.Dark, foreground, background, 3);
    }

    private static void AssertContrast(string theme, string foreground, string background, double minimum)
    {
        var ratio = TokenFile.Contrast(TokenFile.Theme(theme), foreground, background);

        Assert.True(ratio >= minimum, $"{theme}: {foreground} op {background} heeft contrast {ratio:F2}:1, minimaal {minimum}:1 vereist.");
    }
}
