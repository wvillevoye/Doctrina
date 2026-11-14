using System.Net;
using System.Text.Json;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.Host.IntegrationTests.HostUi;
using Doctrina.Modules.Lti;
using Doctrina.Modules.Lti.Application;
using Microsoft.Extensions.DependencyInjection;
using Doctrina.TestKit.Authentication;

// Specs lti-tool-registration and lti-launch of change add-lti-platform, against the host with the Lti module on.
namespace Doctrina.Host.IntegrationTests.Academic;

internal static class LtiSeed
{
    /// <summary>The Lti stack: Academic, Content and Lti; role <c>lti.admin</c> gets <c>Lti.Manage</c>.</summary>
    public static Task<AcademicHost> HostAsync(DoctrinaHostFactory factory, CancellationToken ct, params (string Key, string? Value)[] settings) =>
        AcademicHost.CreateAsync(factory, [(AccessHosts.Mapping("lti.admin", 0), LtiPermissions.Manage), .. settings], ct, "Content", "Lti");

    public static StubUser Admin { get; } = new("sub-lti-admin", "Lotte Beheer", ["lti.admin"], "lotte@example.test", "P7000098");
}

public sealed class DoctrinaPubliceertEigenOndertekeningssleutelsOpHetWeb(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task JwksIsAnoniemBereikbaar()
    {
        await using var academic = await LtiSeed.HostAsync(factory, Ct);
        using var browser = academic.Host.CreateBrowser(); // not signed in

        using var response = await browser.GetAsync(new Uri("/lti/jwks.json", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var jwks = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var key = Assert.Single(jwks.RootElement.GetProperty("keys").EnumerateArray());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
    }
}

internal static class LtiTools
{
    public static async Task<Doctrina.Modules.Lti.Domain.LtiTool> ToolAsync(this AcademicHost academic, string name = "Quiz tool", bool sendName = false, bool sendEmail = false)
    {
        using var scope = academic.Host.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ToolService>().CreateAsync(
            new ToolInput(name, "https://tool.example.org/login", "https://tool.example.org/launch", "https://tool.example.org/launch", sendName, sendEmail), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value!;
    }
}

public sealed class BeheerdersRegistrerenLtiToolsOpHetWeb(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AlleenBeheerdersBeherenTools()
    {
        await using var academic = await LtiSeed.HostAsync(factory, Ct);

        var (studentStatus, studentHtml) = await academic.GetAsync(factory, StubUser.Student, "/lti/tools", Ct);
        var (adminStatus, adminHtml) = await academic.GetAsync(factory, LtiSeed.Admin, "/lti/tools", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, studentStatus);
        Assert.Contains(">Access denied</h1>", studentHtml, StringComparison.Ordinal);
        Assert.Null(Html.Parse(studentHtml).QuerySelector("[data-testid=tool-form]"));
        Assert.Equal(HttpStatusCode.OK, adminStatus);
        Assert.NotNull(Html.Parse(adminHtml).QuerySelector("[data-testid=tool-form]"));
    }

    [Fact]
    public async Task MenuItemAlleenMetDePermissie()
    {
        await using var academic = await LtiSeed.HostAsync(factory, Ct);

        var (_, admin) = await academic.GetAsync(factory, LtiSeed.Admin, "/", Ct);
        var (_, student) = await academic.GetAsync(factory, StubUser.Student, "/", Ct);

        Assert.Contains(">LTI tools<", admin, StringComparison.Ordinal);
        Assert.DoesNotContain(">LTI tools<", student, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BeheerpaginaToontDePlatformgegevens()
    {
        await using var academic = await LtiSeed.HostAsync(factory, Ct);
        var tool = await academic.ToolAsync();

        var (status, html) = await academic.GetAsync(factory, LtiSeed.Admin, "/lti/tools", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var page = Html.Parse(html);
        var issuer = page.QuerySelector("[data-testid=issuer]")!.TextContent;
        Assert.StartsWith("http", issuer, StringComparison.Ordinal);
        Assert.Equal($"{issuer}/lti/authorize", page.QuerySelector("[data-testid=authorize-url]")!.TextContent);
        Assert.Equal($"{issuer}/lti/jwks.json", page.QuerySelector("[data-testid=jwks-url]")!.TextContent);
        Assert.False(string.IsNullOrWhiteSpace(page.QuerySelector("[data-testid=active-kid]")!.TextContent));
        var card = page.QuerySelector("[data-testid=tool]")!;
        Assert.Equal(tool.ClientId, card.QuerySelector("[data-testid=client-id]")!.TextContent);
        Assert.Equal(tool.DeploymentId, card.QuerySelector("[data-testid=deployment-id]")!.TextContent);
    }

    [Fact]
    public async Task IngesteldeIssuerWordtGebruikt()
    {
        await using var academic = await LtiSeed.HostAsync(factory, Ct, ("Lti:Issuer", "https://lms.example.org/"));

        var (_, html) = await academic.GetAsync(factory, LtiSeed.Admin, "/lti/tools", Ct);

        var page = Html.Parse(html);
        Assert.Equal("https://lms.example.org", page.QuerySelector("[data-testid=issuer]")!.TextContent);
        Assert.Equal("https://lms.example.org/lti/jwks.json", page.QuerySelector("[data-testid=jwks-url]")!.TextContent);
    }
}
