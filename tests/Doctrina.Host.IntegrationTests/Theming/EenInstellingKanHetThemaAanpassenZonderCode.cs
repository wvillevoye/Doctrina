using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

// Spec theming, requirement "Een instelling kan het thema aanpassen zonder code"; taken 3.2 en 3.4, design D4.
namespace Doctrina.Host.IntegrationTests.Theming;

public sealed partial class EenInstellingKanHetThemaAanpassenZonderCode(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task EigenThemaStylesheetWordtNaDeStandaardstijlenGeladen()
    {
        using var host = factory.WithSettings(("Theme:StylesheetPath", "/theme/instelling.css"));
        using var browser = host.CreateBrowser();

        var html = await browser.GetStringAsync(new Uri("/", UriKind.Relative), Ct);
        var standard = html.IndexOf("_content/Doctrina.UI/css/doctrina.", StringComparison.Ordinal);
        var theme = html.IndexOf("<link rel=\"stylesheet\" href=\"/theme/instelling.css\"", StringComparison.Ordinal);

        Assert.True(standard > 0, "doctrina.css wordt niet geladen");
        Assert.True(theme > standard, "De stylesheet van de instelling moet ná doctrina.css staan.");
        Assert.True(theme < html.IndexOf("</head>", StringComparison.Ordinal), "De stylesheet van de instelling hoort in <head>.");
    }

    [Fact]
    public async Task EigenLogoVervangtHetWoordmerk()
    {
        using var host = factory.WithSettings(("Theme:LogoPath", "/theme/logo.svg"), ("Theme:LogoAlt", "Universiteit Voorbeeld"));
        using var browser = host.CreateBrowser();

        var html = await browser.GetStringAsync(new Uri("/", UriKind.Relative), Ct);
        var brand = Brand().Match(html);

        Assert.True(brand.Success, "Merklink niet gevonden");
        Assert.Contains("<img src=\"/theme/logo.svg\" alt=\"Universiteit Voorbeeld\"", brand.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("dc-shell__wordmark", brand.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZonderEigenThemaDeStandaardvormgeving()
    {
        using var browser = factory.CreateBrowser();

        var html = await browser.GetStringAsync(new Uri("/", UriKind.Relative), Ct);
        var brand = Brand().Match(html);

        Assert.DoesNotContain("data-testid=\"theme-stylesheet\"", html, StringComparison.Ordinal);
        Assert.Matches(">Doctrina</span>", brand.Value);
        Assert.DoesNotContain("<img", brand.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Theme:StylesheetPath", "https://cdn.example/theme.css", null, null)]
    [InlineData("Theme:StylesheetPath", "//cdn.example/theme.css", null, null)]
    [InlineData("Theme:StylesheetPath", "theme/theme.css", null, null)]
    [InlineData("Theme:LogoPath", "https://cdn.example/logo.svg", "Theme:LogoAlt", "Logo")]
    [InlineData("Theme:LogoPath", "/theme/logo.svg", null, null)]
    public void OngeldigeThemaInstellingLaatHetOpstartenMislukken(string key, string value, string? otherKey, string? otherValue)
    {
        (string, string?)[] settings = otherKey is null ? [(key, value)] : [(key, value), (otherKey, otherValue)];
        using var host = factory.WithSettings(settings);

        var failure = StartupFailure(host);

        Assert.NotNull(failure);
        Assert.Contains(otherKey is null && key == "Theme:LogoPath" ? "Theme:LogoAlt" : key, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VoorbeeldUitDeDocumentatieBevatAlleenBestaandeTokens()
    {
        var root = RepoRoot();
        var doc = File.ReadAllText(Path.Combine(root, "docs", "ontwikkelen", "vormgeving.md"));
        var tokens = File.ReadAllText(Path.Combine(root, "src", "Doctrina.UI", "Styles", "_tokens.scss"));

        var example = CssBlock().Match(doc);
        Assert.True(example.Success, "Geen ```css-voorbeeld in docs/ontwikkelen/vormgeving.md");
        var properties = Declaration().Matches(example.Groups["css"].Value).Select(m => m.Groups["property"].Value).ToList();

        Assert.NotEmpty(properties);
        Assert.All(properties, p =>
        {
            Assert.StartsWith("--dc-", p, StringComparison.Ordinal);
            Assert.Contains($"{p}:", tokens, StringComparison.Ordinal);
        });
    }

    private static OptionsValidationException? StartupFailure(WebApplicationFactory<Program> host)
    {
        var exception = Record.Exception(() => host.CreateClient().Dispose());
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is OptionsValidationException validation)
            {
                return validation;
            }
        }

        return null;
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Doctrina.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Doctrina.slnx niet gevonden");
    }

    [GeneratedRegex("""<a href="/" class="dc-shell__brand"[^>]*>.*?</a>""", RegexOptions.Singleline)]
    private static partial Regex Brand();

    [GeneratedRegex("""```css\r?\n(?<css>.*?)```""", RegexOptions.Singleline)]
    private static partial Regex CssBlock();

    [GeneratedRegex("""(?<property>[a-z-]+)\s*:\s*[^;{}]+;""")]
    private static partial Regex Declaration();
}
