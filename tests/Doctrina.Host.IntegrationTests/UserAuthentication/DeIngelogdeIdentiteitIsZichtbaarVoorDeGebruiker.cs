using System.Net;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Modules;

namespace Doctrina.Host.IntegrationTests.UserAuthentication;

// Spec user-authentication, requirement "De ingelogde identiteit is zichtbaar voor de gebruiker".
public sealed class DeIngelogdeIdentiteitIsZichtbaarVoorDeGebruiker(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProfielpaginaToontDeIdentiteit()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName)
            .WithAppSettings((AccessHosts.Mapping("employee", 0), "Sample.Read"));
        using var browser = host.CreateBrowser();

        var html = await browser.SignInAndGetAsync("/profile", factory.Idp, new StubUser("sub-3", "Dirk Docent", ["employee"], PersonId: "P7002809"), Ct);

        Assert.Contains("data-testid=\"name\">Dirk Docent<", html, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"person-id\">P7002809<", html, StringComparison.Ordinal);
        Assert.Contains($"data-testid=\"issuer\">{StubOidcProvider.Issuer}<", html, StringComparison.Ordinal);
        Assert.Contains("<li>employee</li>", html, StringComparison.Ordinal);
        Assert.Contains("<code>Sample.Read</code>", html, StringComparison.Ordinal); // afgeleide permissie
        Assert.Contains("<li>employee: Sample.Read</li>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJ", html, StringComparison.Ordinal); // geen JWT's op de pagina
    }

    [Fact]
    public async Task ProfielpaginaZonderPersoonsnummer()
    {
        using var browser = factory.CreateBrowser();

        var html = await browser.SignInAndGetAsync("/profile", factory.Idp, new StubUser("sub-4", "Zonder Nummer", ["student"]), Ct);

        // Blazor codeert "–" als &#x2013;; vergelijk de gedecodeerde tekst.
        Assert.Contains("data-testid=\"person-id\">–<", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }
}
