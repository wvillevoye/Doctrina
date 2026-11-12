using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Doctrina.Infrastructure.Modules;
using Doctrina.TestKit.Modules;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// Tests volgen spec openspec/changes/add-modular-foundation/specs/module-system/spec.md:
// klasse = requirement, methode = scenario.
namespace Doctrina.Host.IntegrationTests.ModuleSystem;

public sealed class ModulesWordenViaConfiguratieAangezet(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task IngeschakeldeModuleWordtGestart()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName);
        using var client = factory.CreateApiClient(host);

        using var endpoint = await client.GetAsync(new Uri("/api/v1/samples", UriKind.Relative), Ct);
        using var page = await client.GetAsync(new Uri("/sample", UriKind.Relative), Ct);
        var start = await client.GetStringAsync(new Uri("/", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, endpoint.StatusCode);
        Assert.Equal(SampleModule.Samples, await endpoint.Content.ReadFromJsonAsync<string[]>(Ct));
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains($"<h1>{SampleModule.PageTitle}</h1>", await page.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Contains($">{SampleModule.NavTitle}<", start, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UitgeschakeldeModuleDraagtNietsBij()
    {
        using var host = factory.WithModules([new SampleModule { Health = HealthStatus.Unhealthy }]);
        using var client = factory.CreateApiClient(host);

        using var endpoint = await client.GetAsync(new Uri("/api/v1/samples", UriKind.Relative), Ct);
        using var page = await client.GetAsync(new Uri("/sample", UriKind.Relative), Ct);
        using var ready = await client.GetAsync(new Uri(ModuleHealthEndpoints.ReadyPath, UriKind.Relative), Ct);
        var start = await client.GetStringAsync(new Uri("/", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, endpoint.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        // De falende health check van de uitgeschakelde module telt niet mee (de database hoort hier niet bij deze test).
        var report = await ready.Content.ReadFromJsonAsync<HealthResponse>(JsonSerializerOptions.Web, Ct);
        Assert.NotNull(report);
        Assert.False(report.Modules.ContainsKey(SampleModule.ModuleName));
        Assert.DoesNotContain($">{SampleModule.NavTitle}<", start, StringComparison.Ordinal);
        Assert.Empty(host.Services.GetServices<InitializedModule>());
        Assert.Empty(host.Services.GetRequiredService<ModuleCatalog>().Enabled);
    }

    [Fact]
    public void OnbekendeModulenaamLaatHetOpstartenMislukken()
    {
        using var host = factory.WithModules([new SampleModule()], "Onbekend");

        var failure = host.StartupFailure();

        Assert.NotNull(failure);
        Assert.Contains("Onbekende module 'Onbekend'", failure.Message, StringComparison.Ordinal);
    }
}

public sealed class ModuleAfhankelijkhedenWordenBijHetOpstartenOpgelost(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    [Fact]
    public void AfhankelijkhedenStartenEerst()
    {
        // B staat bewust eerst in Modules:Enabled.
        using var host = factory.WithModules([TestModules.A(), TestModules.B("A")], "B", "A");

        var initialized = host.Services.GetServices<InitializedModule>().Select(m => m.Name);
        var catalog = host.Services.GetRequiredService<ModuleCatalog>().Enabled.Select(m => m.Name);

        Assert.Equal(["A", "B"], initialized);
        Assert.Equal(["A", "B"], catalog);
    }

    [Fact]
    public void OntbrekendeAfhankelijkheidLaatHetOpstartenMislukken()
    {
        using var host = factory.WithModules([TestModules.A(), TestModules.B("A")], "B");

        var failure = host.StartupFailure();

        Assert.NotNull(failure);
        Assert.Contains("Module 'B' vereist module 'A'", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CyclischeAfhankelijkheidLaatHetOpstartenMislukken()
    {
        using var host = factory.WithModules([TestModules.A("B"), TestModules.B("A")], "A", "B");

        var failure = host.StartupFailure();

        Assert.NotNull(failure);
        var cycle = Assert.Single(failure.Errors, e => e.StartsWith("Cyclische afhankelijkheid", StringComparison.Ordinal));
        Assert.Contains("A → B → A", cycle, StringComparison.Ordinal);
    }
}

public sealed class ModulesMeldenHunGezondheid(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FalendeModulecheckMaaktDeHostNietGereed()
    {
        using var host = factory.WithModules(
            [new SampleModule { Health = HealthStatus.Unhealthy }, new TestModule("A") { Health = HealthStatus.Healthy }],
            SampleModule.ModuleName,
            "A");
        using var client = host.CreateBrowser();

        using var response = await client.GetAsync(new Uri(ModuleHealthEndpoints.ReadyPath, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<HealthResponse>(JsonSerializerOptions.Web, Ct);
        Assert.NotNull(report);
        Assert.Equal("Unhealthy", report.Status);
        Assert.Equal("Unhealthy", report.Modules[SampleModule.ModuleName].Status);
        Assert.Equal("Unhealthy", report.Modules[SampleModule.ModuleName].Checks["sample-check"]);
        Assert.Equal("Healthy", report.Modules["A"].Status);
    }
}

public static class ModuleHostExtensions
{
    /// <summary>Client met een bearer-token van de nagebootste provider van <paramref name="root"/>.</summary>
    public static HttpClient CreateApiClient(this DoctrinaHostFactory root, WebApplicationFactory<Program> host)
    {
        var client = host.CreateBrowser();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", root.CreateApiToken());
        return client;
    }
}
