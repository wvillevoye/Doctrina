using System.Net;
using System.Text.RegularExpressions;
using Doctrina.TestKit.Authentication;

// Spec theming, requirement "Lettertypen en iconen worden zelf gehost"; taken 1.3 en 3.3.
namespace Doctrina.Host.IntegrationTests.Theming;

public sealed partial class LettertypenEnIconenWordenZelfGehost(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/_content/Doctrina.UI/css/doctrina.css", "text/css")]
    [InlineData("/_content/Doctrina.UI/fonts/inter-latin-wght-normal.woff2", "font/woff2")]
    [InlineData("/_content/Doctrina.UI/fonts/inter-latin-wght-italic.woff2", "font/woff2")]
    [InlineData("/_content/Doctrina.UI/fonts/fraunces-latin-wght-normal.woff2", "font/woff2")]
    [InlineData("/_content/Doctrina.UI/fonts/OFL.txt", "text/plain")]
    public async Task BronnenVanDeVormgevingWordenDoorDoctrinaGeserveerd(string path, string mediaType)
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.GetAsync(new Uri(path, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GeenExterneBronnenVoorDeVormgeving()
    {
        using var browser = factory.CreateBrowser();
        var pages = new[]
        {
            await browser.GetStringAsync(new Uri("/", UriKind.Relative), Ct),
            await browser.SignInAndGetAsync("/profiel", factory.Idp, StubUser.Student, Ct),
        };

        var external = new List<string>();
        var stylesheets = new Queue<Uri>();
        foreach (var html in pages)
        {
            foreach (var reference in HtmlReference().Matches(html).Select(m => m.Groups["url"].Value))
            {
                Check(reference, "pagina", external);
            }

            foreach (var href in Stylesheet().Matches(html).Select(m => m.Groups["url"].Value))
            {
                stylesheets.Enqueue(new Uri(new Uri("https://localhost/"), href));
            }
        }

        var seen = new HashSet<Uri>();
        var fonts = 0;
        while (stylesheets.TryDequeue(out var stylesheet))
        {
            if (!seen.Add(stylesheet) || !stylesheet.Host.Equals("localhost", StringComparison.Ordinal))
            {
                continue;
            }

            var css = await browser.GetStringAsync(new Uri(stylesheet.PathAndQuery, UriKind.Relative), Ct);
            foreach (var reference in CssReference().Matches(css).Select(m => m.Groups["url"].Value.Trim('"', '\'', ' ')))
            {
                Check(reference, stylesheet.AbsolutePath, external);
                fonts += reference.EndsWith(".woff2", StringComparison.Ordinal) ? 1 : 0;
                if (reference.EndsWith(".css", StringComparison.Ordinal))
                {
                    stylesheets.Enqueue(new Uri(stylesheet, reference));
                }
            }
        }

        Assert.Contains(seen, s => s.AbsolutePath.Contains("_content/Doctrina.UI/css/doctrina", StringComparison.Ordinal));
        Assert.Equal(3, fonts); // Inter normaal en cursief, Fraunces
        Assert.Empty(external);
    }

    /// <summary>Absolute URL naar een ander domein (of protocol-relatief) is een externe bron; <c>data:</c> niet.</summary>
    private static void Check(string reference, string source, List<string> external)
    {
        if (reference.StartsWith("data:", StringComparison.Ordinal) || reference.StartsWith('#'))
        {
            return;
        }

        if (reference.StartsWith("//", StringComparison.Ordinal)
            || (Uri.TryCreate(reference, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.Host != "localhost"))
        {
            external.Add($"{source}: {reference}");
        }
    }

    [GeneratedRegex("""<(?:link|script|img|source|iframe)\b[^>]*?\b(?:href|src)="(?<url>[^"]*)""")]
    private static partial Regex HtmlReference();

    [GeneratedRegex("""<link\b[^>]*rel="stylesheet"[^>]*href="(?<url>[^"]*)""")]
    private static partial Regex Stylesheet();

    [GeneratedRegex("""url\((?<url>[^)]*)\)|@import\s+["'](?<url>[^"']+)["']""")]
    private static partial Regex CssReference();
}
