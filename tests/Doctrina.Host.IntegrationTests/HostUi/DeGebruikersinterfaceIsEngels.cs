using System.Net;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Modules;

namespace Doctrina.Host.IntegrationTests.HostUi;

// Spec app-shell, requirement "De gebruikersinterface is Engels" (change switch-ui-to-english).
public sealed class DeGebruikersinterfaceIsEngels(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task EngelsePaden()
    {
        using var browser = factory.CreateBrowser();

        var page = Html.Parse(await browser.SignInAndGetAsync("/profile", factory.Idp, StubUser.Student, Ct));
        using var old = await browser.GetAsync(new Uri("/profiel", UriKind.Relative), Ct);

        Assert.NotNull(page.QuerySelector("main [data-testid=name]"));
        Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
    }

    [Fact]
    public async Task FouttitelInDeUiIsEngelsBijEenNederlandseBrowser()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName);
        using var browser = host.CreateBrowser();
        browser.DefaultRequestHeaders.AcceptLanguage.ParseAdd("nl");

        using var response = await browser.GetAsync(new Uri("/sample/result", UriKind.Relative), Ct);
        var page = Html.Parse(await response.Content.ReadAsStringAsync(Ct));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var alert = page.QuerySelector("[data-error-code='Sample.Item.NotFound']");
        Assert.NotNull(alert);
        Assert.Contains("Sample item not found", alert.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Voorbeelditem niet gevonden", alert.TextContent, StringComparison.Ordinal);
    }
}
