using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Doctrina.Core.Results;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.Host.IntegrationTests.DataPersistence;
using Doctrina.Host.IntegrationTests.ModuleSystem;
using Doctrina.Infrastructure.Api;
using Doctrina.Infrastructure.Modules;
using Doctrina.TestKit.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Tests volgen spec openspec/specs/api-conventions/spec.md:
// klasse = requirement, methode = scenario.
namespace Doctrina.Host.IntegrationTests.ApiConventions;

internal static class ApiHosts
{
    public static async Task<JsonElement> ProblemAsync(this HttpResponseMessage response, CancellationToken ct)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    /// <summary>Testmodule met een endpoint per fouttype: <c>GET errors/{type}</c> en <c>GET errors/validatie</c>.</summary>
    public static TestModule ErrorsModule() => new("Errors")
    {
        Endpoints = api =>
        {
            api.MapGet("errors/{type}", (ErrorType type) => Result.Failure(new Error($"Errors.{type}", $"Fout van type {type}.", type)).ToHttpResult());
            api.MapGet("errors/validatie", () => Result<string>.Failure(Error.Validation(
                "Errors.Invalid",
                "Ongeldige invoer.",
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["email"] = ["Ongeldig e-mailadres."],
                    ["givenName"] = ["Verplicht."],
                })).ToHttpResult());
            api.MapGet("errors/exception", string () => throw new InvalidOperationException("Geheime melding over I6123456"));
        },
    };
}

public sealed class ModuleEndpointsStaanGeversioneerdOnderApiV1(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ModuleEndpointOnderHetVersieprefix()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName);
        using var client = factory.CreateApiClient(host);

        using var versioned = await client.GetAsync(new Uri("/api/v1/samples", UriKind.Relative), Ct);
        using var unversioned = await client.GetAsync(new Uri("/samples", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, versioned.StatusCode);
        Assert.Equal(SampleModule.Samples, await versioned.Content.ReadFromJsonAsync<string[]>(Ct));
        Assert.Equal(HttpStatusCode.NotFound, unversioned.StatusCode);
    }
}

public sealed class DeApiIsGedocumenteerdMetOpenApi(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OpenApiDocumentToontModuleEndpoints()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName).InEnvironment(Environments.Development);
        using var client = host.CreateBrowser();

        var document = await client.GetFromJsonAsync<JsonElement>(new Uri("/openapi/v1.json", UriKind.Relative), Ct);
        using var scalarStart = await client.GetAsync(new Uri("/scalar", UriKind.Relative), Ct);
        using var scalar = scalarStart.Headers.Location is { } location
            ? await client.GetAsync(new Uri(location.IsAbsoluteUri ? location.PathAndQuery : new Uri(new Uri("https://localhost/scalar"), location).PathAndQuery, UriKind.Relative), Ct)
            : scalarStart;

        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/samples", out var samples));
        Assert.False(paths.TryGetProperty("/inloggen", out _));
        var scheme = document.GetProperty("components").GetProperty("securitySchemes").GetProperty(ApiDocumentation.SchemeName);
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        var security = samples.GetProperty("get").GetProperty("security");
        Assert.Contains(security.EnumerateArray(), r => r.TryGetProperty(ApiDocumentation.SchemeName, out _));
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
        Assert.Contains("scalar", await scalar.Content.ReadAsStringAsync(Ct), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DocumentatieStandaardVerborgenInProductie()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName).InEnvironment(Environments.Production);
        using var enabled = factory.WithModules([new SampleModule()], SampleModule.ModuleName)
            .InEnvironment(Environments.Production, (ApiDocumentation.EnabledKey, "true"));
        using var client = host.CreateBrowser();
        using var enabledClient = enabled.CreateBrowser();

        using var document = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Ct);
        using var scalar = await client.GetAsync(new Uri("/scalar", UriKind.Relative), Ct);
        using var documentWhenEnabled = await enabledClient.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, scalar.StatusCode);
        Assert.Equal(HttpStatusCode.OK, documentWhenEnabled.StatusCode);
    }
}

public sealed class VerwachteFoutenWordenAlsProblemDetailsTeruggegeven(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Failure, 500)]
    public async Task FouttypeBepaaltDeStatus(ErrorType type, int status)
    {
        using var host = factory.WithModules([ApiHosts.ErrorsModule()], "Errors");
        using var client = factory.CreateApiClient(host);

        using var response = await client.GetAsync(new Uri($"/api/v1/errors/{type}", UriKind.Relative), Ct);

        Assert.Equal(status, (int)response.StatusCode);
        var problem = await response.ProblemAsync(Ct);
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.Equal($"Errors.{type}", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ValidatiefoutenPerVeld()
    {
        using var host = factory.WithModules([ApiHosts.ErrorsModule()], "Errors");
        using var client = factory.CreateApiClient(host);

        using var response = await client.GetAsync(new Uri("/api/v1/errors/validatie", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ProblemAsync(Ct);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("Errors.Invalid", problem.GetProperty("code").GetString());
        var errors = problem.GetProperty("errors");
        Assert.Equal("Ongeldig e-mailadres.", errors.GetProperty("email")[0].GetString());
        Assert.Equal("Verplicht.", errors.GetProperty("givenName")[0].GetString());
    }
}

public sealed class FouttitelsZijnVertaald(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EngelseTitelGevraagd()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName);
        using var dutch = factory.CreateApiClient(host);
        using var english = factory.CreateApiClient(host);
        english.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));

        using var dutchResponse = await dutch.GetAsync(new Uri("/api/v1/samples/ontbreekt", UriKind.Relative), Ct);
        using var englishResponse = await english.GetAsync(new Uri("/api/v1/samples/ontbreekt", UriKind.Relative), Ct);

        Assert.Equal("Sample item not found", (await englishResponse.ProblemAsync(Ct)).GetProperty("title").GetString());
        Assert.Equal("Voorbeelditem niet gevonden", (await dutchResponse.ProblemAsync(Ct)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task OntbrekendeVertalingValtTerugOpDeMelding()
    {
        using var host = factory.WithModules([new SampleModule()], SampleModule.ModuleName);
        using var client = factory.CreateApiClient(host);
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));

        using var response = await client.GetAsync(new Uri("/api/v1/samples/onvertaald", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Item not found", (await response.ProblemAsync(Ct)).GetProperty("title").GetString());
    }
}

public sealed class OnverwachteFoutenLekkenGeenDetails(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OnafgehandeldeExceptionInProductie()
    {
        using var logs = new CapturingLoggerProvider();
        using var host = factory.WithModules([ApiHosts.ErrorsModule()], "Errors")
            .InEnvironment(Environments.Production)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        using var client = factory.CreateApiClient(host);

        using var response = await client.GetAsync(new Uri("/api/v1/errors/exception", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        var problem = await response.ProblemAsync(Ct);
        Assert.Equal(UnexpectedExceptionHandler.Code, problem.GetProperty("code").GetString());
        var traceId = problem.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrEmpty(traceId));
        Assert.DoesNotContain("Geheime melding", body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);

        var logged = Assert.Single(logs.Logs, l => l.Category == typeof(UnexpectedExceptionHandler).FullName);
        Assert.Equal(LogLevel.Error, logged.Level);
        Assert.Contains(traceId!, logged.Message, StringComparison.Ordinal);
        Assert.Contains("api/v1/errors/exception", logged.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("I6123456", logged.Message, StringComparison.Ordinal);
        Assert.Null(logged.Exception); // geen exceptionobject met melding in de log
    }
}

public sealed class LivenessEnReadinessEndpoints(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private const string UnreachableDatabase = "Server=tcp:127.0.0.1,1;Database=Doctrina;User Id=doctrina;Password=geheim-wachtwoord;Connect Timeout=2;TrustServerCertificate=True";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LivenessTerwijlDeDatabaseOnbereikbaarIs()
    {
        using var host = factory.WithAppSettings(("ConnectionStrings:Doctrina", UnreachableDatabase));
        using var client = host.CreateBrowser();

        using var response = await client.GetAsync(new Uri(ModuleHealthEndpoints.LivePath, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadinessTerwijlDeDatabaseOnbereikbaarIs()
    {
        using var host = factory.WithAppSettings(("ConnectionStrings:Doctrina", UnreachableDatabase));
        using var client = host.CreateBrowser();

        using var response = await client.GetAsync(new Uri(ModuleHealthEndpoints.ReadyPath, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        var report = JsonSerializer.Deserialize<HealthResponse>(body, JsonSerializerOptions.Web)!;
        Assert.Equal("Unhealthy", report.Checks["database"]);
        Assert.DoesNotContain("127.0.0.1", body, StringComparison.Ordinal);
        Assert.DoesNotContain("geheim-wachtwoord", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadinessMetBereikbareDatabase()
    {
        await using var database = await TestDatabases.CreateAsync(Ct);
        using var host = factory.WithAppSettings(("ConnectionStrings:Doctrina", database.ConnectionString));
        using var client = host.CreateBrowser();

        using var response = await client.GetAsync(new Uri(ModuleHealthEndpoints.ReadyPath, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
