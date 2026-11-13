using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Doctrina.Host.IntegrationTests.ApiConventions;
using Doctrina.Host.IntegrationTests.DataPersistence;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Host.IntegrationTests.Academic;

internal static class Json
{
    public static Uri Url(string path) => new(path, UriKind.Relative);

    public static async Task<JsonElement> ReadAsync(this HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ct);

    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string path, object body, CancellationToken ct) =>
        client.PatchAsync(Url(path), JsonContent.Create(body), ct);

    public static object Person(string? id, string given = "Sanne", string? surname = "Student") =>
        new Dictionary<string, string?> { ["personId"] = id, ["givenName"] = given, ["surname"] = surname };
}

// Spec academic-api, requirement "Elk Academic-type heeft REST-endpoints onder /api/v1".
public sealed class ElkAcademicTypeHeeftRestEndpointsOnderApiV1(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OnbekendId()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory);

        using var response = await api.GetAsync(Json.Url($"/api/v1/persons/{Guid.Empty}"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Academic.Person.NotFound", (await response.ProblemAsync(Ct)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("organizations")]
    [InlineData("academic-sessions")]
    [InlineData("courses")]
    [InlineData("course-offerings")]
    [InlineData("persons")]
    [InlineData("associations")]
    public async Task ElkTypeHeeftEenLijst(string type)
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory);

        using var response = await api.GetAsync(Json.Url($"/api/v1/{type}"), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await response.ReadAsync(Ct)).GetProperty("items").GetArrayLength());
    }
}

// Spec academic-api, requirement "Records gebruiken de veldnamen van de import".
public sealed class RecordsGebruikenDeVeldnamenVanDeImport(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PersoonOpvragenOpId()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.SeedAsync("persons", Seeds.Person("I6000001", "Sanne", "Student"));
        var id = await academic.PersonIdAsync("I6000001");
        using var api = academic.Api(factory, roles: "test.read");

        using var response = await api.GetAsync(Json.Url($"/api/v1/persons/{id}"), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var person = await response.ReadAsync(Ct);
        Assert.Equal(id, person.GetProperty("id").GetGuid());
        Assert.Equal(("I6000001", "Sanne", "Student", "SIS", "active"),
            (person.GetProperty("personId").GetString(), person.GetProperty("givenName").GetString(), person.GetProperty("surname").GetString(),
             person.GetProperty("source").GetString(), person.GetProperty("status").GetString()));
        Assert.True(person.GetProperty("dateLastModified").GetDateTimeOffset() > DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public async Task VerwijzingenAlsExternalId()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.OfferingAsync();
        var id = await academic.QueryAsync(db => db.CourseOfferings.Select(o => o.Id).SingleAsync(Ct));
        using var api = academic.Api(factory);

        var offering = await (await api.GetAsync(Json.Url($"/api/v1/course-offerings/{id}"), Ct)).ReadAsync(Ct);

        Assert.Equal(("BIO101", "2026-P1"), (offering.GetProperty("courseId").GetString(), offering.GetProperty("academicSessionId").GetString()));
    }
}

// Spec academic-api, requirement "Lijsten zijn gepagineerd en ondersteunen delta's".
public sealed class LijstenZijnGepagineerdEnOndersteunenDeltas(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeltaSindsEenMoment()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.SeedAsync("courses", AcademicHost.Record(("courseId", "BIO101"), ("name", "Cell biology")));
        var bio = await academic.QueryAsync(db => db.Courses.Select(c => c.DateLastModified).SingleAsync(Ct));
        await Task.Delay(50, Ct);
        await academic.SeedAsync("courses", AcademicHost.Record(("courseId", "WIS201"), ("name", "Calculus")));
        using var api = academic.Api(factory);

        var since = Uri.EscapeDataString(bio.AddMilliseconds(10).ToString("O"));
        var page = await (await api.GetAsync(Json.Url($"/api/v1/courses?modifiedSince={since}"), Ct)).ReadAsync(Ct);

        Assert.Equal(["WIS201"], page.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("courseId").GetString()));
    }

    [Fact]
    public async Task Paginering()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.SeedAsync("persons", [.. Enumerable.Range(1, 120).Select(i => Seeds.Person($"I6{i:D6}", "Student", $"{i}"))]);
        using var api = academic.Api(factory);

        var page = await (await api.GetAsync(Json.Url("/api/v1/persons?pageSize=50&pageNumber=3"), Ct)).ReadAsync(Ct);

        Assert.Equal(20, page.GetProperty("items").GetArrayLength());
        Assert.Equal((3, true, false), (page.GetProperty("totalPages").GetInt32(), page.GetProperty("hasPreviousPage").GetBoolean(), page.GetProperty("hasNextPage").GetBoolean()));
    }

    [Fact]
    public async Task FiltersOpBronEnStatus()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.SeedAsync("courses",
            AcademicHost.Record(("courseId", "A"), ("name", "A")),
            AcademicHost.Record(("courseId", "B"), ("name", "B"), ("status", "toBeDeleted")));
        using var api = academic.Api(factory);

        var deleted = await (await api.GetAsync(Json.Url("/api/v1/courses?status=toBeDeleted&source=SIS"), Ct)).ReadAsync(Ct);
        var other = await (await api.GetAsync(Json.Url("/api/v1/courses?source=Rooster"), Ct)).ReadAsync(Ct);
        using var invalid = await api.GetAsync(Json.Url("/api/v1/courses?pageSize=0&status=gone"), Ct);

        Assert.Equal(["B"], deleted.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("courseId").GetString()));
        Assert.Equal(0, other.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await invalid.ProblemAsync(Ct);
        Assert.Equal("Academic.Api.InvalidQuery", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("pageSize", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("status", out _));
    }
}

// Spec academic-api, requirement "Systemen schrijven met hun eigen bron".
public sealed class SystemenSchrijvenMetHunEigenBron(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BronUitDeConfiguratie()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, [("Api:Sources:roosterpakket", "Rooster")], Ct);
        using var api = academic.Api(factory, "roosterpakket");

        using var response = await api.PostAsJsonAsync(Json.Url("/api/v1/courses"), new { courseId = "BIO101", name = "Cell biology", source = "SIS" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Rooster", (await response.ReadAsync(Ct)).GetProperty("source").GetString());
    }

    [Fact]
    public async Task RecordVanEenAndereBron()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, [("Api:Sources:roosterpakket", "Rooster")], Ct);
        await academic.SeedAsync("persons", Seeds.Person("I6000001", "Sanne", "Student"));
        var id = await academic.PersonIdAsync("I6000001");
        using var api = academic.Api(factory, "roosterpakket");

        using var patch = await api.PatchJsonAsync($"/api/v1/persons/{id}", new { givenName = "Hacked" }, Ct);
        using var delete = await api.DeleteAsync(Json.Url($"/api/v1/persons/{id}"), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, patch.StatusCode);
        Assert.Equal("Academic.Record.OtherSource", (await patch.ProblemAsync(Ct)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        var person = await academic.QueryAsync(db => db.Persons.SingleAsync(Ct));
        Assert.Equal(("Sanne", Core.Entities.EntityStatus.Active), (person.GivenName, person.Status));
    }
}

// Spec academic-api, requirement "Aanmaken geeft een conflict bij een bestaande sleutel".
public sealed class AanmakenGeeftEenConflictBijEenBestaandeSleutel(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AanmakenVanEenNieuwePersoon()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory);

        using var response = await api.PostAsJsonAsync(Json.Url("/api/v1/persons"), Json.Person("I6000001"), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.ReadAsync(Ct)).GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/persons/{id:D}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AanmakenVanEenBestaandeSleutel()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.SeedAsync("persons", Seeds.Person("I6000001", "Sanne", "Student"));
        using var api = academic.Api(factory);

        using var response = await api.PostAsJsonAsync(Json.Url("/api/v1/persons"), Json.Person("I6000001"), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Academic.Person.DuplicateExternalId", (await response.ProblemAsync(Ct)).GetProperty("code").GetString());
    }
}

// Spec academic-api, requirement "Upsert op de sleutel van het bronsysteem".
public sealed class UpsertOpDeSleutelVanHetBronsysteem(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UpsertVanEenNieuwePersoon()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory);

        using var response = await api.PutAsJsonAsync(Json.Url("/api/v1/persons/by-external-id/I6000001"), new { givenName = "Sanne", surname = "Student" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var person = await academic.QueryAsync(db => db.Persons.SingleAsync(Ct));
        Assert.Equal(("I6000001", "SIS"), (person.ExternalId, person.Source));
    }

    [Fact]
    public async Task UpsertVanEenBestaandePersoon()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory);
        using var first = await api.PutAsJsonAsync(Json.Url("/api/v1/persons/by-external-id/I6000001"), new { givenName = "Sanne", surname = "Student" }, Ct);

        using var response = await api.PutAsJsonAsync(Json.Url("/api/v1/persons/by-external-id/I6000001"), new { givenName = "Sanna", surname = "Student" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Sanna", (await response.ReadAsync(Ct)).GetProperty("givenName").GetString());
    }

    [Fact]
    public async Task OngeldigeInvoer()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory);

        using var response = await api.PutAsJsonAsync(Json.Url("/api/v1/persons/by-external-id/I6000001"), new { givenName = "Sanne" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ProblemAsync(Ct);
        Assert.Equal("Academic.Person.Invalid", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("surname", out _));
    }

    [Fact]
    public async Task AfwijkendeSleutelInDeBody()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory);

        using var response = await api.PutAsJsonAsync(Json.Url("/api/v1/persons/by-external-id/I6000001"), Json.Person("I6000002"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Academic.Api.KeyMismatch", (await response.ProblemAsync(Ct)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task VerwijzingNaarEenOnbekendRecord()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory);

        using var response = await api.PutAsJsonAsync(Json.Url("/api/v1/course-offerings/by-external-id/X"),
            new { name = "X", courseId = "NOPE", academicSessionId = "NOPE" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Academic.CourseOffering.ReferenceNotFound", (await response.ProblemAsync(Ct)).GetProperty("code").GetString());
    }
}

// Spec academic-api, requirement "Gedeeltelijk bijwerken behoudt de overige velden".
public sealed class GedeeltelijkBijwerkenBehoudtDeOverigeVelden(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GedeeltelijkBijwerken()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.SeedAsync("persons", AcademicHost.Record(("personId", "I6000001"), ("givenName", "Sanne"), ("surname", "Student"), ("mail", "sanne@example.test")));
        var id = await academic.PersonIdAsync("I6000001");
        using var api = academic.Api(factory);

        using var response = await api.PatchJsonAsync($"/api/v1/persons/{id}", new { mail = "s.student@example.test" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var person = await academic.QueryAsync(db => db.Persons.SingleAsync(Ct));
        Assert.Equal(("Sanne", "Student", "s.student@example.test"), (person.GivenName, person.Surname, person.Mail));
    }

    [Fact]
    public async Task SleutelWijzigenKanNiet()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.SeedAsync("persons", Seeds.Person("I6000001", "Sanne", "Student"));
        var id = await academic.PersonIdAsync("I6000001");
        using var api = academic.Api(factory);

        using var response = await api.PatchJsonAsync($"/api/v1/persons/{id}", new { personId = "I6999999" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Academic.Api.KeyMismatch", (await response.ProblemAsync(Ct)).GetProperty("code").GetString());
    }
}

// Spec academic-api, requirement "Verwijderen is zacht".
public sealed class VerwijderenIsZacht(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InschrijvingVerwijderen()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        await academic.WithStudentAsync();
        var id = await academic.QueryAsync(db => db.Associations.Select(a => a.Id).SingleAsync(Ct));
        var before = await academic.QueryAsync(db => db.Associations.Select(a => a.DateLastModified).SingleAsync(Ct));
        using var api = academic.Api(factory);

        using var delete = await api.DeleteAsync(Json.Url($"/api/v1/associations/{id}"), Ct);
        var association = await (await api.GetAsync(Json.Url($"/api/v1/associations/{id}"), Ct)).ReadAsync(Ct);
        var since = Uri.EscapeDataString(before.AddTicks(1).ToString("O"));
        var delta = await (await api.GetAsync(Json.Url($"/api/v1/associations?modifiedSince={since}"), Ct)).ReadAsync(Ct);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal("toBeDeleted", association.GetProperty("status").GetString());
        Assert.Equal(1, delta.GetProperty("items").GetArrayLength());
    }
}

// Spec academic-api, requirement "Een batch verwerkt tot 500 records met een resultaat per record".
public sealed class EenBatchVerwerktTot500RecordsMetEenResultaatPerRecord(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BatchMetEenFoutRecord()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory);

        using var response = await api.PostAsJsonAsync(Json.Url("/api/v1/persons/batch"),
            new[] { Json.Person("I6000001"), Json.Person("I6000002", surname: null), Json.Person("I6000003") }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.ReadAsync(Ct)).EnumerateArray().ToList();
        Assert.Equal(["created", "failed", "created"], items.Select(i => i.GetProperty("result").GetString()));
        Assert.Equal("Academic.Person.Invalid", items[1].GetProperty("code").GetString());
        Assert.Equal("I6000002", items[1].GetProperty("personId").GetString());
        Assert.Equal(2, await academic.QueryAsync(db => db.Persons.CountAsync(Ct)));
    }

    [Fact]
    public async Task TeGroteBatch()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory);

        using var response = await api.PostAsJsonAsync(Json.Url("/api/v1/persons/batch"),
            Enumerable.Range(1, 501).Select(i => Json.Person($"I6{i:D6}")).ToArray(), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Academic.Api.BatchTooLarge", (await response.ProblemAsync(Ct)).GetProperty("code").GetString());
        Assert.Equal(0, await academic.QueryAsync(db => db.Persons.CountAsync(Ct)));
    }
}

// Spec academic-api, requirement "Batches zijn per client begrensd".
public sealed class BatchesZijnPerClientBegrensd(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TeVeelBatches()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, [("Api:BatchesPerMinute", "2")], Ct);
        using var api = academic.Api(factory);
        using var other = academic.Api(factory, "roosterpakket");
        var empty = Array.Empty<object>();

        using var first = await api.PostAsJsonAsync(Json.Url("/api/v1/courses/batch"), empty, Ct);
        using var second = await api.PostAsJsonAsync(Json.Url("/api/v1/courses/batch"), empty, Ct);
        using var third = await api.PostAsJsonAsync(Json.Url("/api/v1/courses/batch"), empty, Ct);
        using var fromOther = await other.PostAsJsonAsync(Json.Url("/api/v1/courses/batch"), empty, Ct);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.StatusCode, second.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("Api.TooManyRequests", (await third.ProblemAsync(Ct)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, fromOther.StatusCode);
    }
}

// Spec academic-api, requirement "Lezen en schrijven vragen aparte permissies".
public sealed class LezenEnSchrijvenVragenApartePermissies(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AlleenLeesrechten()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory, roles: "test.read");

        using var write = await api.PutAsJsonAsync(Json.Url("/api/v1/persons/by-external-id/I6000001"), new { givenName = "Sanne", surname = "Student" }, Ct);
        using var read = await api.GetAsync(Json.Url("/api/v1/persons"), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal("Auth.Forbidden", (await write.ProblemAsync(Ct)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }
}

// add-rest-api task 2.6: the OpenAPI description of write endpoints lists the fields of the type.
public sealed class DeApiBeschrijftDeVeldenPerType(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UpsertBeschrijftDeVerplichteVelden()
    {
        using var host = factory.WithModules([], "Academic").InEnvironment(Microsoft.Extensions.Hosting.Environments.Development);
        using var client = host.CreateBrowser();

        var document = await client.GetFromJsonAsync<JsonElement>(Json.Url("/openapi/v1.json"), Ct);

        var put = document.GetProperty("paths").GetProperty("/api/v1/persons/by-external-id/{externalId}").GetProperty("put");
        Assert.Contains("surname (required)", put.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("mail", put.GetProperty("description").GetString(), StringComparison.Ordinal);
    }
}
