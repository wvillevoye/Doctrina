using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using Doctrina.Host.IntegrationTests.Academic;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Academic.Domain;
using Doctrina.Modules.Interop.OOAPI;
using Doctrina.Modules.Interop.OOAPI.Client;
using Doctrina.TestKit.Modules;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Doctrina.Host.IntegrationTests.Ooapi;

// Tests follow openspec/changes/add-ooapi-consumer/specs/ooapi-consumer/spec.md.

/// <summary>An OOAPI v5 source in memory: a token endpoint and lists that are paged like the standard, with a log of what was asked.</summary>
internal sealed class FakeOoapiSource : HttpMessageHandler
{
    public const string BaseUrl = "https://sis.example.org/ooapi/v5";
    public const string TokenUrl = "https://idp.example.org/oauth/token";

    private readonly Dictionary<string, List<object>> _lists = [];
    private readonly Dictionary<string, List<object>> _past = [];

    /// <summary>"path?query" of every list request, in order.</summary>
    public List<string> Requests { get; } = [];

    public List<string?> Authorizations { get; } = [];

    public int TokenRequests { get; private set; }

    public string? LastTokenBody { get; private set; }

    public int ExpiresIn { get; set; } = 3600;

    public HttpStatusCode? TokenStatus { get; set; }

    /// <summary>While set, list requests wait for it (a read that is still busy).</summary>
    public TaskCompletionSource? Hold { get; set; }

    /// <summary>Paths that answer with this status instead of a page.</summary>
    public Dictionary<string, HttpStatusCode> Failing { get; } = [];

    public string[] Paths => [.. Requests.Select(r => r.Split('?')[0]).Distinct()];

    /// <summary>Items of a list. Items added with <see cref="AddPast"/> only show when the request has <c>since</c>, like past offerings.</summary>
    public FakeOoapiSource Add(string path, params object[] items)
    {
        _lists.TryAdd(path, []);
        _lists[path].AddRange(items);
        return this;
    }

    public FakeOoapiSource Clear(string path)
    {
        _lists.Remove(path);
        return this;
    }

    public FakeOoapiSource AddPast(string path, params object[] items)
    {
        _past.TryAdd(path, []);
        _past[path].AddRange(items);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        if (uri.ToString().StartsWith(TokenUrl, StringComparison.Ordinal))
        {
            TokenRequests++;
            LastTokenBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return TokenStatus is { } status
                ? new HttpResponseMessage(status)
                : Json(new { access_token = $"token-{TokenRequests}", expires_in = ExpiresIn });
        }

        var path = uri.AbsolutePath["/ooapi/v5/".Length..];
        Requests.Add(path + uri.Query);
        if (Hold is { } hold)
        {
            await hold.Task.WaitAsync(cancellationToken);
        }

        Authorizations.Add(request.Headers.Authorization?.ToString());
        if (Failing.TryGetValue(path, out var failure))
        {
            return new HttpResponseMessage(failure);
        }

        var query = HttpUtility.ParseQueryString(uri.Query);
        var (page, size) = (int.Parse(query["pageNumber"] ?? "1", System.Globalization.CultureInfo.InvariantCulture), int.Parse(query["pageSize"] ?? "10", System.Globalization.CultureInfo.InvariantCulture));
        var all = _lists.GetValueOrDefault(path, []).Concat(query["since"] is null ? [] : _past.GetValueOrDefault(path, [])).ToList();
        var items = all.Skip((page - 1) * size).Take(size).ToList();
        return Json(new { pageSize = size, pageNumber = page, hasPreviousPage = page > 1, hasNextPage = page * size < all.Count, items });
    }

    private static HttpResponseMessage Json(object body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, new MediaTypeHeaderValue("application/json")) };

    // Builders for the resources of the standard.
    public static Dictionary<string, object?> Item(params (string Name, object? Value)[] fields) => fields.ToDictionary(f => f.Name, f => f.Value);

    public static object[] Text(string value) => [new { language = "nl", value }];

    public static object Code(string code) => new { codeType = "identifier", code };

    public static Dictionary<string, object?> Organization(string id, string code, string name = "University", string type = "institute") =>
        Item(("organizationId", id), ("primaryCode", Code(code)), ("organizationType", type), ("name", Text(name)));

    public static Dictionary<string, object?> Session(string id, string code) =>
        Item(("academicSessionId", id), ("primaryCode", Code(code)), ("academicSessionType", "period"), ("name", Text("Period 1")),
            ("startDate", "2026-09-01"), ("endDate", "2026-10-31"));

    public static Dictionary<string, object?> Course(string id, string code, string name = "Cell biology", string? organization = null) =>
        Item(("courseId", id), ("primaryCode", Code(code)), ("name", Text(name)), ("organization", organization));

    public static Dictionary<string, object?> Offering(string id, string code, string session = "s-1") =>
        Item(("offeringId", id), ("primaryCode", Code(code)), ("offeringType", "course"), ("name", Text(code)), ("academicSession", session),
            ("startDate", "2026-09-01"), ("endDate", "2026-10-31"));

    public static Dictionary<string, object?> Person(string id, string? code, string surname = "Student") =>
        Item(("personId", id), ("primaryCode", code is null ? null : Code(code)), ("givenName", "Sanne"), ("surname", surname),
            ("displayName", $"Sanne {surname}"), ("mail", "sanne@example.test"));

    public static Dictionary<string, object?> Association(string id, string person, string role = "student", string type = "courseOfferingAssociation", string state = "associated") =>
        Item(("associationId", id), ("associationType", type), ("role", role), ("state", state), ("person", person));

    /// <summary>One of everything: organization o-1, session s-1, course c-1, offering off-1, person p-1 and association a-1.</summary>
    public FakeOoapiSource Complete() =>
        Add("organizations", Organization("o-1", "UM"))
            .Add("academic-sessions", Session("s-1", "2026-P1"))
            .Add("courses", Course("c-1", "BIO101", organization: "o-1"))
            .Add("courses/c-1/offerings", Offering("off-1", "BIO101-2026-P1"))
            .Add("persons", Person("p-1", "I6000001"))
            .Add("offerings/off-1/associations", Association("a-1", "p-1"));
}

internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };
}

/// <summary>Doctrina with the Ooapi module and a source "ooapi-sis" whose HTTP traffic goes to a <see cref="FakeOoapiSource"/>.</summary>
internal sealed class ConsumerHost : IAsyncDisposable
{
    public const string SourceName = "ooapi-sis";
    public const string Syncer = "test.sync";

    private ConsumerHost(AcademicHost academic, FakeOoapiSource source)
    {
        Academic = academic;
        Source = source;
    }

    public AcademicHost Academic { get; }

    public WebApplicationFactory<Program> Host => Academic.Host;

    public FakeOoapiSource Source { get; }

    public static Task<ConsumerHost> CreateAsync(DoctrinaHostFactory factory, CancellationToken ct, params (string Key, string? Value)[] settings) =>
        CreateAsync(factory, null, ct, settings);

    public static async Task<ConsumerHost> CreateAsync(
        DoctrinaHostFactory factory, Action<IServiceCollection>? configureServices, CancellationToken ct, params (string Key, string? Value)[] settings)
    {
        var source = new FakeOoapiSource();
        var academic = await AcademicHost.CreateAsync(
            factory,
            [
                (AccessHosts.Mapping(Ooapi.Reader, 0), "Ooapi.Read"),
                (AccessHosts.Mapping(Syncer, 0), "Ooapi.Sync"),
                ($"Ooapi:Sources:{SourceName}:BaseUrl", FakeOoapiSource.BaseUrl),
                ($"Ooapi:Sources:{SourceName}:TokenUrl", FakeOoapiSource.TokenUrl),
                ($"Ooapi:Sources:{SourceName}:ClientId", "doctrina"),
                ($"Ooapi:Sources:{SourceName}:ClientSecret", "s3cret-value"),
                ($"Ooapi:Sources:{SourceName}:PageSize", "10"),
                .. settings,
            ],
            services =>
            {
                services.AddSingleton<IHttpClientFactory>(new StubHttpClientFactory(source));
                configureServices?.Invoke(services);
            },
            ct,
            "Ooapi");
        return new ConsumerHost(academic, source);
    }

    /// <summary>Runs a sync for real (or as dry run) and returns its summary; fails the test if the run could not start.</summary>
    public async Task<ImportRunSummary> SyncAsync(CancellationToken ct, bool dryRun = false, string name = SourceName)
    {
        using var scope = Host.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<OoapiSync>().SyncAsync(name, dryRun, "test", ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value!;
    }

    public Task<List<ImportJobRow>> RowsAsync(Guid jobId) =>
        Academic.QueryAsync(db => db.ImportJobRows.AsNoTracking().Where(r => r.ImportJobId == jobId).OrderBy(r => r.File).ThenBy(r => r.Line).ToListAsync());

    public Task<ImportJob> JobAsync(Guid jobId) => Academic.QueryAsync(db => db.ImportJobs.AsNoTracking().SingleAsync(j => j.Id == jobId));

    /// <summary>Waits until the background server has finished the job.</summary>
    public async Task<ImportJob> FinishedAsync(Guid jobId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 120; attempt++)
        {
            var job = await JobAsync(jobId);
            if (job.IsFinished)
            {
                return job;
            }

            await Task.Delay(250, ct);
        }

        throw new TimeoutException($"Job {jobId} did not finish.");
    }

    public async ValueTask DisposeAsync()
    {
        await Academic.DisposeAsync();
        Source.Dispose();
    }
}

internal sealed class FakeEnvironment(string name) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;

    public string ApplicationName { get; set; } = "Doctrina";

    public string ContentRootPath { get; set; } = string.Empty;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public sealed class DeVerbindingMetDeBronIsBeveiligd
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static OoapiSourceOptions Options() => new()
    {
        BaseUrl = FakeOoapiSource.BaseUrl,
        TokenUrl = FakeOoapiSource.TokenUrl,
        ClientId = "doctrina",
        ClientSecret = "s3cret-value",
        PageSize = 10,
    };

    private static async Task<int> CountAsync(OoapiClient client, string path) =>
        await client.ItemsAsync(path, path, Ct).CountAsync(Ct);

    [Fact]
    public async Task TokenWordtHergebruikt()
    {
        using var source = new FakeOoapiSource().Add("courses", FakeOoapiSource.Course("c-1", "A")).Add("persons", FakeOoapiSource.Person("p-1", "P1"));
        var client = new OoapiClient(new HttpClient(source), Options(), new FakeTimeProvider());

        await CountAsync(client, "courses");
        await CountAsync(client, "persons");

        Assert.Equal(1, source.TokenRequests);
        Assert.All(source.Authorizations, a => Assert.Equal("Bearer token-1", a));
        Assert.Contains("grant_type=client_credentials", source.LastTokenBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TweedeAanvraagBijEenVerlopenToken()
    {
        using var source = new FakeOoapiSource { ExpiresIn = 120 }.Add("courses", FakeOoapiSource.Course("c-1", "A"));
        var time = new FakeTimeProvider();
        var client = new OoapiClient(new HttpClient(source), Options(), time);

        await CountAsync(client, "courses");
        time.Advance(TimeSpan.FromSeconds(100)); // within 30 s of the end of the token: it counts as expired
        await CountAsync(client, "courses");

        Assert.Equal(2, source.TokenRequests);
        Assert.Equal(["Bearer token-1", "Bearer token-2"], source.Authorizations);
    }

    [Fact]
    public void AlleenHttpsBuitenDevelopment()
    {
        var options = new OoapiOptions();
        options.Sources["ooapi-sis"] = Options();
        options.Sources["ooapi-sis"].BaseUrl = "http://sis.example.org/ooapi/v5";

        var production = new OoapiOptionsValidator(new FakeEnvironment("Production")).Validate(null, options);
        var development = new OoapiOptionsValidator(new FakeEnvironment("Development")).Validate(null, options);

        Assert.True(production.Failed);
        Assert.Contains(production.Failures!, f => f.Contains("ooapi-sis", StringComparison.Ordinal) && f.Contains("https", StringComparison.Ordinal));
        Assert.True(development.Succeeded);
    }

    [Fact]
    public void EenBronMoetCompleetEnGeldigZijn()
    {
        var options = new OoapiOptions();
        options.Sources["ooapi-sis"] = new OoapiSourceOptions { BaseUrl = "https://sis.example.org", PageSize = 7, Schedule = "elke nacht" };

        var result = new OoapiOptionsValidator(new FakeEnvironment("Production")).Validate(null, options);

        var failures = string.Join('\n', result.Failures!);
        Assert.Contains("PageSize", failures, StringComparison.Ordinal);
        Assert.Contains("Schedule", failures, StringComparison.Ordinal);
        Assert.Contains("TokenUrl", failures, StringComparison.Ordinal);
        Assert.Contains("ClientSecret", failures, StringComparison.Ordinal);
    }
}

public sealed class EenOoapiBronWordtIngelezenInAfhankelijkheidsvolgorde(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AlleTypenInVolgorde()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Complete();

        var summary = await host.SyncAsync(Ct);

        Assert.True(summary.Succeeded, summary.Message);
        Assert.Equal(
            ["organizations", "academic-sessions", "courses", "courses/c-1/offerings", "persons", "offerings/off-1/associations"],
            host.Source.Paths);
        Assert.Equal((6, 0), (summary.Created, summary.Failed));
        Assert.Equal(
            (1, 1, 1, 1, 1, 1),
            await host.Academic.QueryAsync(async db => (
                await db.Organizations.CountAsync(Ct), await db.AcademicSessions.CountAsync(Ct), await db.Courses.CountAsync(Ct),
                await db.CourseOfferings.CountAsync(Ct), await db.Persons.CountAsync(Ct), await db.Associations.CountAsync(Ct))));
    }

    [Fact]
    public async Task MeerderePaginas()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Add("courses", [.. Enumerable.Range(1, 25).Select(i => (object)FakeOoapiSource.Course($"c-{i}", $"C{i:00}"))]);

        var summary = await host.SyncAsync(Ct);

        Assert.Equal(25, summary.Created);
        Assert.Equal(["courses?pageNumber=1&pageSize=10", "courses?pageNumber=2&pageSize=10", "courses?pageNumber=3&pageSize=10"],
            host.Source.Requests.Where(r => r.StartsWith("courses?", StringComparison.Ordinal)));
        Assert.Equal(25, await host.Academic.QueryAsync(db => db.Courses.CountAsync(Ct)));
    }

    [Fact]
    public async Task AfgelopenUitvoeringenWordenOokGelezen()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Add("organizations").Add("academic-sessions", FakeOoapiSource.Session("s-1", "2026-P1"))
            .Add("courses", FakeOoapiSource.Course("c-1", "BIO101"))
            .Add("courses/c-1/offerings", FakeOoapiSource.Offering("off-2", "FUTURE"))
            .AddPast("courses/c-1/offerings", FakeOoapiSource.Offering("off-1", "PAST"));

        var summary = await host.SyncAsync(Ct);

        Assert.Contains(host.Source.Requests, r => r.StartsWith("courses/c-1/offerings?", StringComparison.Ordinal) && r.Contains("since=1900-01-01", StringComparison.Ordinal));
        Assert.True(summary.Succeeded, summary.Message);
        Assert.Equal(["FUTURE", "PAST"], (await host.Academic.QueryAsync(db => db.CourseOfferings.Select(o => o.ExternalId).OrderBy(x => x).ToListAsync())).Order());
    }
}

public sealed class GelezenRecordsGaanViaDeGedeeldeValidatieEnUpsert(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NieuweRecordsZijnAangemaakt()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Complete();

        var summary = await host.SyncAsync(Ct);

        var course = await host.Academic.QueryAsync(db => db.Courses.AsNoTracking().SingleAsync(Ct));
        Assert.Equal(("BIO101", "Cell biology", ConsumerHost.SourceName), (course.ExternalId, course.Name, course.Source));
        var rows = await host.RowsAsync(summary.JobId);
        Assert.Contains(rows, r => r.File == "courses" && r.Key == "BIO101" && r.Outcome == ImportRowOutcome.Created);
        var offering = await host.Academic.QueryAsync(db => db.CourseOfferings.AsNoTracking().SingleAsync(Ct));
        Assert.Equal((course.Id, "BIO101-2026-P1"), (offering.CourseId, offering.ExternalId));
        Assert.Equal(ConsumerHost.SourceName, (await host.JobAsync(summary.JobId)).Source);
    }
}

public sealed class DeSleutelVanEenRecordKomtUitDeBron(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordZonderPrimaryCode()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Add("persons", FakeOoapiSource.Person("p-1", code: null));

        var summary = await host.SyncAsync(Ct);

        Assert.Equal((0, 1), (summary.Created, summary.Failed));
        Assert.Empty(await host.Academic.QueryAsync(db => db.Persons.ToListAsync(Ct)));
        var row = Assert.Single(await host.RowsAsync(summary.JobId));
        Assert.Equal(("persons", "Academic.Person.Invalid"), (row.File, row.Code));
    }

    [Fact]
    public async Task AssociationMetHetIdVanDeBron()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Complete();

        await host.SyncAsync(Ct);

        Assert.Equal("a-1", (await host.Academic.QueryAsync(db => db.Associations.AsNoTracking().SingleAsync(Ct))).ExternalId);
    }
}

public sealed class DeVeldenWordenVertaaldVolgensDeOoapiStandaard(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OnbekendeEnumwaarde()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Add("organizations", FakeOoapiSource.Organization("o-1", "WIZ", type: "faculty-of-wizardry"), FakeOoapiSource.Organization("o-2", "UM"));

        var summary = await host.SyncAsync(Ct);

        Assert.Equal((1, 1), (summary.Created, summary.Failed));
        var failed = Assert.Single(await host.RowsAsync(summary.JobId), r => r.Outcome == ImportRowOutcome.Failed);
        Assert.Equal(("WIZ", "Academic.Organization.Invalid"), (failed.Key, failed.Code));
    }

    [Fact]
    public async Task EenInstituutIsEenInstellingEnTekstenKomenUitDeIngesteldeTaal()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct, ("Ooapi:Language", "en"));
        host.Source.Add("organizations", FakeOoapiSource.Item(
            ("organizationId", "o-1"), ("primaryCode", FakeOoapiSource.Code("UM")), ("organizationType", "institute"),
            ("name", new object[] { new { language = "nl", value = "Universiteit" }, new { language = "en", value = "University" } })));

        await host.SyncAsync(Ct);

        var organization = await host.Academic.QueryAsync(db => db.Organizations.AsNoTracking().SingleAsync(Ct));
        Assert.Equal(("University", Doctrina.Modules.Academic.Domain.OrganizationType.Institution), (organization.Name, organization.OrganizationType));
    }

    [Fact]
    public async Task AssociationVanEenAnderType()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Complete().Add("offerings/off-1/associations", FakeOoapiSource.Association("a-2", "p-1", type: "programOfferingAssociation"));

        var summary = await host.SyncAsync(Ct);

        Assert.Equal(1, await host.Academic.QueryAsync(db => db.Associations.CountAsync(Ct)));
        Assert.Contains("programOfferingAssociation", (await host.JobAsync(summary.JobId)).Warnings, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NietActieveAssociation()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Complete().Add("offerings/off-1/associations", FakeOoapiSource.Association("a-2", "p-1", role: "lecturer", state: "pending"));

        await host.SyncAsync(Ct);

        Assert.Equal(["a-1"], await host.Academic.QueryAsync(db => db.Associations.Select(a => a.ExternalId).ToListAsync(Ct)));
    }

    [Fact]
    public async Task EenTeachingAssistantKrijgtDeRolVanDoctrina()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Complete().Add("offerings/off-1/associations", FakeOoapiSource.Association("a-2", "p-1", role: "teaching assistant"));

        var summary = await host.SyncAsync(Ct);

        Assert.Equal(0, summary.Failed);
        Assert.Contains(AssociationRole.TeachingAssistant, await host.Academic.QueryAsync(db => db.Associations.Select(a => a.Role).ToListAsync(Ct)));
    }
}

public sealed class EenSyncIsIdempotentEnRaaktAndermansRecordsNiet(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TweedeRunGeeftUnchanged()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Complete();
        await host.SyncAsync(Ct);

        var second = await host.SyncAsync(Ct);

        Assert.Equal((0, 0, 6, 0), (second.Created, second.Updated, second.Unchanged, second.Failed));
        Assert.Equal(1, await host.Academic.QueryAsync(db => db.Courses.CountAsync(Ct)));
    }

    [Fact]
    public async Task RecordVanEenAndereBronBlijftOngemoeid()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        await host.Academic.SeedAsync("courses", new Dictionary<string, string?> { ["courseId"] = "BIO101", ["name"] = "Celbiologie" });
        host.Source.Add("courses", FakeOoapiSource.Course("c-1", "BIO101", name: "Cell biology"));

        await host.SyncAsync(Ct);

        var courses = await host.Academic.QueryAsync(db => db.Courses.AsNoTracking().ToListAsync(Ct));
        Assert.Equal(
            [("SIS", "Celbiologie"), (ConsumerHost.SourceName, "Cell biology")],
            courses.Select(c => (c.Source, c.Name)).OrderBy(c => c.Source, StringComparer.Ordinal));
    }

    [Fact]
    public async Task RecordNietMeerInDeBron()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        var (first, second) = (FakeOoapiSource.Course("c-1", "BIO101"), FakeOoapiSource.Course("c-2", "BIO102"));
        host.Source.Add("courses", first, second);
        await host.SyncAsync(Ct);

        host.Source.Clear("courses").Add("courses", first);
        var again = await host.SyncAsync(Ct);

        Assert.Equal((0, 1), (again.Failed, again.Unchanged));
        var courses = await host.Academic.QueryAsync(db => db.Courses.AsNoTracking().OrderBy(c => c.ExternalId).ToListAsync(Ct));
        Assert.Equal(["BIO101", "BIO102"], courses.Select(c => c.ExternalId));
        Assert.All(courses, c => Assert.Equal(Doctrina.Core.Entities.EntityStatus.Active, c.Status));
    }
}

public sealed class EenFoutInDeBronStoptDeRunMetEenDuidelijkeMelding(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ServerfoutNaDriePogingen()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Add("organizations", FakeOoapiSource.Organization("o-1", "UM")).Add("academic-sessions");
        host.Source.Failing["courses"] = HttpStatusCode.ServiceUnavailable;

        var summary = await host.SyncAsync(Ct);

        Assert.False(summary.Succeeded);
        Assert.Contains("courses", summary.Message, StringComparison.Ordinal);
        Assert.Contains("503", summary.Message, StringComparison.Ordinal);
        Assert.Equal(3, host.Source.Requests.Count(r => r.StartsWith("courses?", StringComparison.Ordinal)));
        Assert.Equal(1, await host.Academic.QueryAsync(db => db.Organizations.CountAsync(Ct)));
        Assert.Equal(ImportJobStatus.Failed, (await host.JobAsync(summary.JobId)).Status);
    }

    [Fact]
    public async Task Autorisatiefout()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.TokenStatus = HttpStatusCode.Unauthorized;

        var summary = await host.SyncAsync(Ct);

        Assert.False(summary.Succeeded);
        Assert.Equal(1, host.Source.TokenRequests);
        Assert.DoesNotContain("s3cret", summary.Message, StringComparison.Ordinal);
        Assert.Contains("401", summary.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeVeelRecords()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct, ("Academic:Import:MaxRows", "5"));
        host.Source.Add("courses", [.. Enumerable.Range(1, 6).Select(i => (object)FakeOoapiSource.Course($"c-{i}", $"C{i}"))]);

        var summary = await host.SyncAsync(Ct);

        Assert.False(summary.Succeeded);
        Assert.Contains("maximum of 5", summary.Message, StringComparison.Ordinal);
    }
}

public sealed class EenSyncDraaitOpSchemaEnOpVerzoek(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] RecurringIds(ConsumerHost host)
    {
        using var connection = host.Host.Services.GetRequiredService<Hangfire.JobStorage>().GetConnection();
        return [.. Hangfire.Storage.StorageConnectionExtensions.GetRecurringJobs(connection).Select(j => j.Id)];
    }

    private static Uri Sync(string name = ConsumerHost.SourceName) => new($"/api/v1/ooapi-sources/{name}/syncs", UriKind.Relative);

    [Fact]
    public async Task BronMetSchemaIsGeregistreerd()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct, ($"Ooapi:Sources:{ConsumerHost.SourceName}:Schedule", "0 3 * * *"));

        using var connection = host.Host.Services.GetRequiredService<Hangfire.JobStorage>().GetConnection();
        var job = Assert.Single(Hangfire.Storage.StorageConnectionExtensions.GetRecurringJobs(connection), j => j.Id == "ooapi-sync:ooapi-sis");
        Assert.Equal("0 3 * * *", job.Cron);
    }

    [Fact]
    public async Task BronZonderSchema()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);

        Assert.DoesNotContain("ooapi-sync:ooapi-sis", RecurringIds(host));
    }

    [Fact]
    public async Task UitgeschakeldeBron()
    {
        await using var host = await ConsumerHost.CreateAsync(
            factory, Ct, ($"Ooapi:Sources:{ConsumerHost.SourceName}:Schedule", "0 3 * * *"), ($"Ooapi:Sources:{ConsumerHost.SourceName}:Enabled", "false"));

        Assert.DoesNotContain("ooapi-sync:ooapi-sis", RecurringIds(host));
    }

    [Fact]
    public async Task SyncStartenOpVerzoek()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Complete();
        using var api = host.Academic.Api(factory, "doctrina-sis-sync", ConsumerHost.Syncer);

        using var response = await api.PostAsync(Sync(), null, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.ReadAsync(Ct);
        var jobId = body.GetProperty("jobId").GetGuid();
        Assert.Equal($"/api/v1/imports/{jobId:D}", response.Headers.Location?.OriginalString);
        var job = await host.FinishedAsync(jobId, Ct);
        Assert.Equal((ImportJobStatus.Completed, ConsumerHost.SourceName, 6), (job.Status, job.Source, job.Created));
        Assert.Equal("doctrina-sis-sync", job.StartedBy);
    }

    [Fact]
    public async Task ZonderToken()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        using var anonymous = host.Host.CreateBrowser();

        using var response = await anonymous.PostAsync(Sync(), null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenMetAlleenOoapiRead()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        using var api = host.Academic.Api(factory, "doctrina-sis-sync", Ooapi.Reader);

        using var response = await api.PostAsync(Sync(), null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await host.Academic.QueryAsync(db => db.ImportJobs.ToListAsync(Ct)));
    }

    [Fact]
    public async Task OnbekendeBron()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        using var api = host.Academic.Api(factory, "doctrina-sis-sync", ConsumerHost.Syncer);

        using var response = await api.PostAsync(Sync("bestaat-niet"), null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Ooapi.Source.NotFound", (await response.ReadAsync(Ct)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task LopendeRun()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Complete();
        host.Source.Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var api = host.Academic.Api(factory, "doctrina-sis-sync", ConsumerHost.Syncer);
        using var first = await api.PostAsync(Sync(), null, Ct);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var jobId = (await first.ReadAsync(Ct)).GetProperty("jobId").GetGuid();
        while (host.Source.Requests.Count == 0)
        {
            await Task.Delay(100, Ct); // the run has started: it is waiting for the source
        }

        using var second = await api.PostAsync(Sync(), null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("Ooapi.Sync.AlreadyRunning", (await second.ReadAsync(Ct)).GetProperty("code").GetString());
        Assert.Single(await host.Academic.QueryAsync(db => db.ImportJobs.ToListAsync(Ct)));
        host.Source.Hold.SetResult();
        Assert.Equal(ImportJobStatus.Completed, (await host.FinishedAsync(jobId, Ct)).Status);
    }
}

public sealed class EenSyncLogtNietsPersoonlijksOfGeheims(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LogsBijEenRunMetPersonen()
    {
        var logs = new CapturingLoggerProvider();
        await using var host = await ConsumerHost.CreateAsync(
            factory, services => services.AddSingleton<ILoggerProvider>(logs), Ct, ("Logging:LogLevel:Default", "Trace"));
        var person = FakeOoapiSource.Person("p-1", "I6000777", surname: "Studentachternaam");
        person["givenName"] = "Geheimnaam";
        person["mail"] = "geheim.mailadres@example.test";
        host.Source.Add("persons", person, FakeOoapiSource.Person("p-2", code: null));
        host.Source.Failing["organizations"] = HttpStatusCode.Unauthorized;

        var failed = await host.SyncAsync(Ct);
        host.Source.Failing.Clear();
        var ok = await host.SyncAsync(Ct);

        Assert.False(failed.Succeeded);
        Assert.Equal(1, ok.Created);
        Assert.NotEmpty(logs.Logs);
        Assert.Contains(logs.Logs, l => l.Message.Contains("OOAPI source ooapi-sis", StringComparison.Ordinal));
        foreach (var secret in new[] { "Geheimnaam", "Studentachternaam", "geheim.mailadres", "I6000777", "s3cret-value", "token-1" })
        {
            Assert.DoesNotContain(logs.Logs, l => l.Message.Contains(secret, StringComparison.Ordinal));
        }
    }
}

public sealed class EenSyncIsEenImportjobMetRapportEnDryRun(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DryRunVanEenBronSlaatNietsOp()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        host.Source.Add("courses", FakeOoapiSource.Course("c-1", "BIO101"));

        var summary = await host.SyncAsync(Ct, dryRun: true);

        Assert.Equal(1, summary.Created);
        Assert.Contains(await host.RowsAsync(summary.JobId), r => r.File == "courses" && r.Outcome == ImportRowOutcome.Created);
        Assert.Empty(await host.Academic.QueryAsync(db => db.Courses.ToListAsync(Ct)));
        Assert.True((await host.JobAsync(summary.JobId)).DryRun);
    }
}

public sealed class BovenliggendeRecordsWordenEerstIngevoerd(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BovenliggendeOrganisatieKomtLaterInDeLijst()
    {
        await using var host = await ConsumerHost.CreateAsync(factory, Ct);
        var child = FakeOoapiSource.Organization("o-2", "FSE", type: "faculty");
        child["parent"] = "o-1";
        host.Source.Add("organizations", child, FakeOoapiSource.Organization("o-1", "UM"));

        var summary = await host.SyncAsync(Ct);

        Assert.Equal((2, 0), (summary.Created, summary.Failed));
        var organizations = await host.Academic.QueryAsync(db => db.Organizations.AsNoTracking().ToListAsync(Ct));
        var parent = organizations.Single(o => o.ExternalId == "UM");
        Assert.Equal(parent.Id, organizations.Single(o => o.ExternalId == "FSE").ParentId);
    }
}
