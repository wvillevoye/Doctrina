using System.Net;
using Doctrina.Infrastructure.Authentication;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Modules;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Doctrina.Host.IntegrationTests.HostUi;

// Tests follow the spec delta of change add-idp-unavailable-notice (user-authentication):
// class = requirement, method = scenario.
namespace Doctrina.Host.IntegrationTests.UserAuthentication;

/// <summary>Recognising an unreachable provider (design D2).</summary>
public sealed class HerkenningVanEenOnbereikbareProvider
{
    [Fact]
    public async Task EchteIdx20803KetenIsOnbereikbaar()
    {
        var exception = await Record.ExceptionAsync(() =>
            StubOidcProvider.CreateUnreachableConfigurationManager().GetConfigurationAsync(TestContext.Current.CancellationToken));

        Assert.Contains("IDX20803", exception?.Message, StringComparison.Ordinal);
        Assert.True(IdentityProviderFailure.IsUnreachable(exception!));
    }

    [Fact]
    public void MislukteTerugkeerMetNetwerkfoutIsOnbereikbaar() =>
        Assert.True(IdentityProviderFailure.IsUnreachable(new AuthenticationFailureException("remote login", new HttpRequestException("refused"))));

    [Fact]
    public void TimeOutIsOnbereikbaar() =>
        Assert.True(IdentityProviderFailure.IsUnreachable(
            new AuthenticationFailureException("remote login", new TaskCanceledException("timeout", new TimeoutException()))));

    [Fact]
    public void NetwerkfoutZonderAuthenticatiebronIsNietOnbereikbaar()
    {
        var exception = Record.Exception(CallExternalApi);

        Assert.False(IdentityProviderFailure.IsUnreachable(exception!));

        static void CallExternalApi() => throw new InvalidOperationException("external api", new HttpRequestException("refused"));
    }

    [Fact]
    public void AndereExceptieIsNietOnbereikbaar()
    {
        Assert.False(IdentityProviderFailure.IsUnreachable(new InvalidOperationException("bug")));
        Assert.False(IdentityProviderFailure.IsUnreachable(new AuthenticationFailureException("invalid state")));
    }
}

public sealed class EenOnbereikbareInlogdienstGeeftEenDuidelijkeMelding : IClassFixture<DoctrinaHostFactory>
{
    private readonly DoctrinaHostFactory _factory;

    public EenOnbereikbareInlogdienstGeeftEenDuidelijkeMelding(DoctrinaHostFactory factory)
    {
        _factory = factory;
        _factory.Idp.IsReachable = true;
        _factory.Idp.IsTokenEndpointReachable = true;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InloggenTerwijlDeInlogdienstOnbereikbaarIs()
    {
        using var browser = _factory.CreateBrowser();
        _factory.Idp.IsReachable = false;

        using var signIn = await browser.GetAsync(new Uri("/sign-in?returnUrl=%2Fprofile%3Ftab%3D2", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        var location = signIn.Headers.Location!;
        Assert.StartsWith($"{AuthenticationEndpoints.UnavailablePath}?returnUrl=%2Fprofile%3Ftab%3D2", location.OriginalString, StringComparison.Ordinal);

        using var notice = await browser.GetAsync(location, Ct);
        var html = await notice.Content.ReadAsStringAsync(Ct);
        var page = Html.Parse(html);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, notice.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), notice.Headers.RetryAfter?.Delta);
        Assert.Equal("Sign-in unavailable", page.QuerySelector("main h1")?.TextContent);
        Assert.Equal("/sign-in?returnUrl=%2Fprofile%3Ftab%3D2", page.QuerySelector("main a[data-testid=retry]")?.GetAttribute("href"));
        Assert.DoesNotContain("IDX", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Connection refused", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BeveiligdePaginaOpenenTerwijlDeInlogdienstOnbereikbaarIs()
    {
        using var browser = _factory.CreateBrowser();
        _factory.Idp.IsReachable = false;

        using var response = await browser.GetAsync(new Uri("/profile", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith($"{AuthenticationEndpoints.UnavailablePath}?returnUrl=%2Fprofile", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TerugkeerVanDeProviderMisluktDoorEenNetwerkfout()
    {
        using var browser = _factory.CreateBrowser();
        using var challenge = await browser.GetAsync(new Uri("/profile", UriKind.Relative), Ct);
        _factory.Idp.IsTokenEndpointReachable = false;

        using var callback = await browser.CompleteSignInAsync(challenge, _factory.Idp, StubUser.Student, Ct);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.StartsWith($"{AuthenticationEndpoints.UnavailablePath}?returnUrl=%2F", callback.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain(
            callback.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [],
            c => c.StartsWith("doctrina.session=", StringComparison.Ordinal) && !c.StartsWith("doctrina.session=;", StringComparison.Ordinal));
        _factory.Idp.IsTokenEndpointReachable = true;
        using var profile = await browser.GetAsync(new Uri("/profile", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Redirect, profile.StatusCode);
    }

    [Fact]
    public async Task AndereFoutenBlijvenDeFoutpaginaGeven()
    {
        using var host = _factory.WithModules([new SampleModule()], SampleModule.ModuleName);
        using var browser = host.CreateBrowser();
        _factory.Idp.IsReachable = false;

        using var response = await browser.GetAsync(new Uri("/sample/error", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task MeldingspaginaStuurtNooitNaarEenAndereSite()
    {
        using var browser = _factory.CreateBrowser();

        using var response = await browser.GetAsync(
            new Uri($"{AuthenticationEndpoints.UnavailablePath}?returnUrl=https%3A%2F%2Fevil.example%2F&traceId=%3Cscript%3E", UriKind.Relative), Ct);
        var page = Html.Parse(await response.Content.ReadAsStringAsync(Ct));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("/sign-in?returnUrl=%2F", page.QuerySelector("main a[data-testid=retry]")?.GetAttribute("href"));
        Assert.Null(page.QuerySelector("[data-testid=trace-id]"));
    }
}

public sealed class UitloggenBeeindigtDeSessieOokAlsDeInlogdienstOnbereikbaarIs : IClassFixture<DoctrinaHostFactory>
{
    private readonly DoctrinaHostFactory _factory;

    public UitloggenBeeindigtDeSessieOokAlsDeInlogdienstOnbereikbaarIs(DoctrinaHostFactory factory)
    {
        _factory = factory;
        _factory.Idp.IsReachable = true;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UitloggenTerwijlDeInlogdienstOnbereikbaarIs()
    {
        using var browser = _factory.CreateBrowser();
        var start = await browser.SignInAndGetAsync("/sign-in?returnUrl=%2F", _factory.Idp, StubUser.Student, Ct);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["__RequestVerificationToken"] = BrowserSignIn.ReadAntiforgeryToken(start),
            ["returnUrl"] = "/",
        });
        _factory.Idp.IsReachable = false;

        using var signOut = await browser.PostAsync(new Uri("/sign-out", UriKind.Relative), form, Ct);

        Assert.Equal(HttpStatusCode.Redirect, signOut.StatusCode);
        Assert.Contains(signOut.Headers.GetValues("Set-Cookie"), c => c.StartsWith("doctrina.session=;", StringComparison.Ordinal));
        var location = signOut.Headers.Location!;
        Assert.StartsWith(AuthenticationEndpoints.UnavailablePath, location.OriginalString, StringComparison.Ordinal);
        using var notice = await browser.GetAsync(location, Ct);
        var page = Html.Parse(await notice.Content.ReadAsStringAsync(Ct));
        Assert.Contains("You have been signed out of Doctrina", page.QuerySelector("main")?.TextContent, StringComparison.Ordinal);

        using var profile = await browser.GetAsync(new Uri("/profile", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Redirect, profile.StatusCode);
    }
}

public sealed class EenOnbereikbareInlogdienstWordtGelogdZonderPersoonsgegevens(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeOorzaakStaatInDeLogZonderPersoonsgegevens()
    {
        var logs = new CapturingLoggerProvider();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        using var browser = host.CreateBrowser();
        using var challenge = await browser.GetAsync(new Uri("/profile", UriKind.Relative), Ct);
        var state = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query)["state"].ToString();
        factory.Idp.IsTokenEndpointReachable = false;

        try
        {
            using var callback = await browser.CompleteSignInAsync(challenge, factory.Idp, StubUser.Student, Ct);
            var traceId = QueryHelpers.ParseQuery(new Uri(browser.BaseAddress!, callback.Headers.Location!).Query)["traceId"].ToString();

            var warning = Assert.Single(logs.Logs, l => l.Level == LogLevel.Warning && l.Category.EndsWith("IdentityProviderUnavailableMiddleware", StringComparison.Ordinal));
            Assert.Contains("idp.test/realms/doctrina", warning.Message, StringComparison.Ordinal);
            Assert.False(string.IsNullOrEmpty(traceId));
            Assert.Contains(traceId, warning.Message, StringComparison.Ordinal);
            foreach (var text in logs.Logs.Select(l => $"{l.Message} {l.Exception}"))
            {
                Assert.DoesNotContain(StubUser.Student.Name, text, StringComparison.Ordinal);
                Assert.DoesNotContain(StubUser.Student.Email!, text, StringComparison.Ordinal);
                Assert.DoesNotContain(StubUser.Student.PersonId!, text, StringComparison.Ordinal);
                Assert.DoesNotContain("code=", text, StringComparison.Ordinal);
                Assert.DoesNotContain("state=", text, StringComparison.Ordinal);
                Assert.DoesNotContain(state, text, StringComparison.Ordinal);
            }
        }
        finally
        {
            factory.Idp.IsTokenEndpointReachable = true;
        }
    }
}
