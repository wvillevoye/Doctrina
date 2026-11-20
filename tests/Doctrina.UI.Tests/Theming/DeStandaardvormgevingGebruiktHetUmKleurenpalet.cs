// Spec theming (change add-um-theme), requirements "De standaardvormgeving gebruikt het UM-kleurenpalet" and
// "Merkkleuren die te zwak zijn als tekst krijgen een afgeleide kleur"; design D1-D5.
namespace Doctrina.UI.Tests.Theming;

public sealed class DeStandaardvormgevingGebruiktHetUmKleurenpalet
{
    [Fact]
    public void MerkkleurenInHetLichteThema()
    {
        var theme = TokenFile.Theme(TokenFile.Light);

        Assert.Equal("#d2460f", theme["--dc-accent"], ignoreCase: true);
        Assert.Equal("#001c3d", theme["--dc-heading"], ignoreCase: true);
        Assert.Equal("#333333", theme["--dc-text"], ignoreCase: true);
    }

    [Fact]
    public void DonkerblauweAchtergrondInHetDonkereThema() =>
        Assert.Equal("#001c3d", TokenFile.Theme(TokenFile.Dark)["--dc-bg"], ignoreCase: true);

    [Fact]
    public void OudePaletwaardenZijnVerdwenen()
    {
        string[] old = ["#1f2a44", "#b44d32", "#faf7f2"];
        var sources = Directory.EnumerateFiles(RepoPaths.Ui, "*.*", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f) is ".scss" or ".css" or ".razor" or ".cs")
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        var found = sources.Where(f => old.Any(o => File.ReadAllText(f).Contains(o, StringComparison.OrdinalIgnoreCase))).ToList();

        Assert.True(found.Count == 0, "Oude paletwaarden gevonden in: " + string.Join(", ", found));
    }

    /// <summary>Design D4: the hover of the primary button (15% shade) stays close to the UM hover colour and keeps contrast with white text.</summary>
    [Fact]
    public void HoverVanDePrimaireKnopBlijftBijDeUmHoverkleur()
    {
        var (r, g, b) = TokenFile.Color(TokenFile.Theme(TokenFile.Light), "--dc-accent");
        var mixed = (R: r * 0.85, G: g * 0.85, B: b * 0.85); // color-mix with black, 15%
        var um = (R: 0xB3 / 255d, G: 0x3C / 255d, B: 0x0D / 255d);

        Assert.True(Math.Abs(mixed.R - um.R) < 0.02 && Math.Abs(mixed.G - um.G) < 0.02 && Math.Abs(mixed.B - um.B) < 0.02);
    }
}

public sealed class MerkkleurenDieTeZwakZijnAlsTekstKrijgenEenAfgeleideKleur
{
    public static TheoryData<string> Themas => new() { TokenFile.Light, TokenFile.Dark };

    [Theory]
    [MemberData(nameof(Themas))]
    public void LinksHalenHetContrastMetAfgeleideKleuren(string mixin)
    {
        var theme = TokenFile.Theme(mixin);

        foreach (var background in new[] { "--dc-bg", "--dc-surface", "--dc-surface-sunken" })
        {
            var ratio = TokenFile.Contrast(theme, "--dc-link", background);
            Assert.True(ratio >= 4.5, $"{mixin}: --dc-link op {background} heeft contrast {ratio:F2}:1.");
        }
    }
}
