// Spec theming, requirement "Beweging respecteert de voorkeur van de gebruiker"; taak 2.2.
namespace Doctrina.UI.Tests.Theming;

public sealed class BewegingRespecteertDeVoorkeurVanDeGebruiker
{
    [Fact]
    public void MinderBewegingGevraagd()
    {
        var block = CompiledCss.MediaBlock("(prefers-reduced-motion: reduce)");
        var everything = CompiledCss.Declarations(block, "*");
        var root = CompiledCss.Declarations(block, ":root");

        // Waarden genormaliseerd: gecomprimeerde CSS (Release) schrijft "0.01ms !important" als ".01ms!important".
        Assert.Equal(".01ms!important", Normalize(everything["transition-duration"]));
        Assert.Equal(".01ms!important", Normalize(everything["animation-duration"]));
        Assert.Equal("1!important", Normalize(everything["animation-iteration-count"]));
        Assert.Equal("0ms", Normalize(root["--dc-duration"]));
        Assert.Equal("0ms", Normalize(root["--dc-duration-fast"]));
    }

    private static string Normalize(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value.Replace(" ", string.Empty, StringComparison.Ordinal), @"(?<!\d)0\.", ".");

    [Fact]
    public void AnimatiesZijnKort()
    {
        var tokens = TokenFile.Theme(TokenFile.Light);

        Assert.Equal("180ms", tokens["--dc-duration"]);
        Assert.Equal("150ms", tokens["--dc-duration-fast"]);
    }
}
