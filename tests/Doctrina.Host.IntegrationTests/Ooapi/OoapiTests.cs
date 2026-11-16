using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Doctrina.Host.IntegrationTests.Academic;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.Host.IntegrationTests.DataPersistence;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Modules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Doctrina.Host.IntegrationTests.Academic.AcademicHost;

namespace Doctrina.Host.IntegrationTests.Ooapi;

// Tests follow openspec/changes/add-ooapi-interop/specs/ooapi-provider/spec.md.
internal static class Ooapi
{
    public const string Reader = "test.ooapi";

    public static Task<AcademicHost> HostAsync(DoctrinaHostFactory factory, CancellationToken ct, params (string Key, string? Value)[] settings) =>
        AcademicHost.CreateAsync(factory, [(AccessHosts.Mapping(Reader, 0), "Ooapi.Read"), .. settings], ct, "Ooapi");

    public static HttpClient Client(this AcademicHost academic, DoctrinaHostFactory factory) => academic.Api(factory, "doctrina-sis-sync", Reader);

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string path, CancellationToken ct)
    {
        using var response = await client.GetAsync(Json.Url(path), ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync(ct);
    }

    public static Task<Guid> IdOfAsync(this AcademicHost academic, string type, string externalId) => academic.QueryAsync(db => type switch
    {
        "courses" => db.Courses.Where(x => x.ExternalId == externalId).Select(x => x.Id).SingleAsync(),
        "persons" => db.Persons.Where(x => x.ExternalId == externalId).Select(x => x.Id).SingleAsync(),
        "offerings" => db.CourseOfferings.Where(x => x.ExternalId == externalId).Select(x => x.Id).SingleAsync(),
        "sessions" => db.AcademicSessions.Where(x => x.ExternalId == externalId).Select(x => x.Id).SingleAsync(),
        "organizations" => db.Organizations.Where(x => x.ExternalId == externalId).Select(x => x.Id).SingleAsync(),
        _ => db.Associations.Where(x => x.ExternalId == externalId).Select(x => x.Id).SingleAsync(),
    });

    public static string Ids(this JsonElement page, string property) =>
        string.Join(",", page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty(property).GetString()));
}

public sealed class DoctrinaBiedtOoapiResourcesAan(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CursusOpvragenOpId()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        await academic.SeedAsync("courses", Record(("courseId", "BIO101"), ("name", "Cell biology")));
        using var api = academic.Client(factory);

        var id = await academic.IdOfAsync("courses", "BIO101");
        var course = await api.GetJsonAsync($"/ooapi/v5/courses/{id}", Ct);

        Assert.Equal(id.ToString(), course.GetProperty("courseId").GetString());
        Assert.Equal("BIO101", course.GetProperty("primaryCode").GetProperty("code").GetString());
        Assert.Equal("identifier", course.GetProperty("primaryCode").GetProperty("codeType").GetString());
        var name = course.GetProperty("name")[0];
        Assert.Equal(("nl", "Cell biology"), (name.GetProperty("language").GetString(), name.GetProperty("value").GetString()));
    }

    [Fact]
    public async Task OnbekendId()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        using var api = academic.Client(factory);

        using var response = await api.GetAsync(Json.Url($"/ooapi/v5/persons/{Guid.NewGuid()}"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadAsync(Ct);
        Assert.Equal("404", problem.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, problem.GetProperty("title").ValueKind);
    }

    [Fact]
    public async Task SchrijvenWordtNietOndersteund()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        using var api = academic.Client(factory);

        using var response = await api.PostAsJsonAsync(Json.Url("/ooapi/v5/courses"), new { name = "x" }, Ct);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Equal(0, await academic.QueryAsync(db => db.Courses.CountAsync(Ct)));
    }

    [Fact]
    public async Task OrganisatieMetType()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        await academic.SeedAsync("organizations",
            Record(("organizationId", "UM"), ("name", "University"), ("organizationType", "root")),
            Record(("organizationId", "FSE"), ("name", "Faculty of Science"), ("organizationType", "institution"), ("parentOrganizationId", "UM")));
        using var api = academic.Client(factory);

        var org = await api.GetJsonAsync($"/ooapi/v5/organizations/{await academic.IdOfAsync("organizations", "FSE")}", Ct);

        Assert.Equal("institute", org.GetProperty("organizationType").GetString());
        Assert.Equal((await academic.IdOfAsync("organizations", "UM")).ToString(), org.GetProperty("parent").GetString());
        Assert.Equal("Faculty of Science", org.GetProperty("shortName").GetString());
    }
}

public sealed class EenUitvoeringIsEenOoapiOffering(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UitvoeringMetCursusEnPeriode()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        await academic.OfferingAsync();
        using var api = academic.Client(factory);

        var offering = await api.GetJsonAsync($"/ooapi/v5/offerings/{await academic.IdOfAsync("offerings", "BIO101-2026-P1")}", Ct);

        Assert.Equal("course", offering.GetProperty("offeringType").GetString());
        Assert.Equal((await academic.IdOfAsync("courses", "BIO101")).ToString(), offering.GetProperty("course").GetString());
        Assert.Equal((await academic.IdOfAsync("sessions", "2026-P1")).ToString(), offering.GetProperty("academicSession").GetString());
    }

    [Fact]
    public async Task UitvoeringenVanEenCursus()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        await academic.OfferingAsync();
        await academic.OfferingAsync("BIO101", "BIO101-2026-P2", "2026-P1");
        await academic.OfferingAsync("WIS201", "WIS201-2026-P1", "2026-P1");
        using var api = academic.Client(factory);

        var page = await api.GetJsonAsync($"/ooapi/v5/courses/{await academic.IdOfAsync("courses", "BIO101")}/offerings", Ct);

        Assert.Equal("BIO101-2026-P1,BIO101-2026-P2", string.Join(",", page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("primaryCode").GetProperty("code").GetString())));
    }

    [Fact]
    public async Task UitvoeringZonderEigenDatums()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        await academic.OfferingAsync();
        using var api = academic.Client(factory);

        var offering = await api.GetJsonAsync($"/ooapi/v5/offerings/{await academic.IdOfAsync("offerings", "BIO101-2026-P1")}", Ct);

        Assert.Equal(("2026-09-01", "2026-10-31"), (offering.GetProperty("startDate").GetString(), offering.GetProperty("endDate").GetString()));
    }
}

public sealed class EenInschrijvingIsEenOoapiAssociation(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<AcademicHost> SeedAsync(DoctrinaHostFactory factory, CancellationToken ct)
    {
        var academic = await Ooapi.HostAsync(factory, ct);
        await academic.OfferingAsync();
        await academic.SeedAsync("persons", Seeds.Person("I1", "Stu", "Dent1"), Seeds.Person("I2", "Stu", "Dent2"), Seeds.Person("I3", "Stu", "Dent3"),
            Seeds.Person("P1", "Dirk", "Docent"), Seeds.Person("T1", "Tim", "Assistent"), Seeds.Person("C1", "Cor", "Coordinator"));
        foreach (var (id, person, role) in new[] { ("A1", "I1", "student"), ("A2", "I2", "student"), ("A3", "I3", "student"), ("A4", "P1", "lecturer"), ("A5", "T1", "teaching-assistant"), ("A6", "C1", "coordinator") })
        {
            await academic.SeedAsync("associations", Seeds.Enrolment(id, person, "BIO101-2026-P1", role));
        }

        return academic;
    }

    [Fact]
    public async Task DocentInEenUitvoering()
    {
        await using var academic = await SeedAsync(factory, Ct);
        using var api = academic.Client(factory);

        var association = await api.GetJsonAsync($"/ooapi/v5/associations/{await academic.IdOfAsync("associations", "A4")}", Ct);

        Assert.Equal("lecturer", association.GetProperty("role").GetString());
        Assert.Equal(("courseOfferingAssociation", "associated"), (association.GetProperty("associationType").GetString(), association.GetProperty("state").GetString()));
        Assert.Equal((await academic.IdOfAsync("persons", "P1")).ToString(), association.GetProperty("person").GetString());
        Assert.Equal((await academic.IdOfAsync("offerings", "BIO101-2026-P1")).ToString(), association.GetProperty("offering").GetString());
    }

    [Fact]
    public async Task TeachingAssistantEnCoordinator()
    {
        await using var academic = await SeedAsync(factory, Ct);
        using var api = academic.Client(factory);

        var page = await api.GetJsonAsync($"/ooapi/v5/offerings/{await academic.IdOfAsync("offerings", "BIO101-2026-P1")}/associations?pageSize=50", Ct);

        var roles = page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("role").GetString()).Order().ToArray();
        Assert.Equal(["coordinator", "lecturer", "student", "student", "student", "teaching assistant"], roles);
    }

    [Fact]
    public async Task FilterenOpRol()
    {
        await using var academic = await SeedAsync(factory, Ct);
        using var api = academic.Client(factory);

        var page = await api.GetJsonAsync($"/ooapi/v5/offerings/{await academic.IdOfAsync("offerings", "BIO101-2026-P1")}/associations?role=student", Ct);

        Assert.Equal(3, page.GetProperty("items").GetArrayLength());
        Assert.All(page.GetProperty("items").EnumerateArray(), i => Assert.Equal("student", i.GetProperty("role").GetString()));
    }
}

public sealed class LijstenZijnGepagineerdVolgensDeStandaard(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<AcademicHost> With45PersonsAsync()
    {
        var academic = await Ooapi.HostAsync(factory, Ct);
        await academic.SeedAsync("persons", [.. Enumerable.Range(1, 45).Select(i => Seeds.Person($"I{i:D7}", "Stu", $"Dent{i:D2}"))]);
        return academic;
    }

    [Fact]
    public async Task TweedePaginaVanPersonen()
    {
        await using var academic = await With45PersonsAsync();
        using var api = academic.Client(factory);

        var page = await api.GetJsonAsync("/ooapi/v5/persons?pageSize=20&pageNumber=2", Ct);

        Assert.Equal(20, page.GetProperty("items").GetArrayLength());
        Assert.Equal((20, 2, 3), (page.GetProperty("pageSize").GetInt32(), page.GetProperty("pageNumber").GetInt32(), page.GetProperty("totalPages").GetInt32()));
        Assert.True(page.GetProperty("hasPreviousPage").GetBoolean());
        Assert.True(page.GetProperty("hasNextPage").GetBoolean());
    }

    [Fact]
    public async Task StandaardPaginagrootte()
    {
        await using var academic = await With45PersonsAsync();
        using var api = academic.Client(factory);

        var page = await api.GetJsonAsync("/ooapi/v5/persons", Ct);

        Assert.Equal(10, page.GetProperty("items").GetArrayLength());
        Assert.Equal(1, page.GetProperty("pageNumber").GetInt32());
        Assert.False(page.GetProperty("hasPreviousPage").GetBoolean());
    }

    [Theory]
    [InlineData("pageSize=15")]
    [InlineData("pageSize=500")]
    [InlineData("pageNumber=0")]
    public async Task TeGroteOfOngeldigePagina(string query)
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        using var api = academic.Client(factory);

        using var response = await api.GetAsync(Json.Url($"/ooapi/v5/persons?{query}"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class AlleenActieveRecordsWordenGetoond(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task VerwijderdRecordIsOnzichtbaar()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        await academic.SeedAsync("persons", Seeds.Person("I1", "Sanne", "Student"), Record(("personId", "I2"), ("givenName", "Gone"), ("surname", "Person"), ("status", "toBeDeleted")));
        using var api = academic.Client(factory);

        var list = await api.GetJsonAsync("/ooapi/v5/persons", Ct);
        using var byId = await api.GetAsync(Json.Url($"/ooapi/v5/persons/{await academic.IdOfAsync("persons", "I2")}"), Ct);

        Assert.Equal(1, list.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, byId.StatusCode);
    }
}

public sealed class DeSleutelVanHetBronsysteemStaatAlsCode(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FilterenOpCode()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        await academic.SeedAsync("courses", Record(("courseId", "BIO101"), ("name", "Cell biology")), Record(("courseId", "WIS201"), ("name", "Analysis")));
        using var api = academic.Client(factory);

        var page = await api.GetJsonAsync("/ooapi/v5/courses?primaryCode=WIS201", Ct);

        Assert.Equal("WIS201", Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("primaryCode").GetProperty("code").GetString());
    }
}

public sealed class VerplichteOoapiVeldenZonderBronWordenAfgeleidOfIngesteld(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PersoonMetEenInschrijvingAlsStudent()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        await academic.OfferingAsync();
        await academic.SeedAsync("persons", Seeds.Person("I1", "Sanne", "Student"));
        await academic.SeedAsync("associations", Seeds.Enrolment("A1", "I1", "BIO101-2026-P1", "student"));
        using var api = academic.Client(factory);

        var person = await api.GetJsonAsync($"/ooapi/v5/persons/{await academic.IdOfAsync("persons", "I1")}", Ct);

        Assert.Equal(["student"], person.GetProperty("affiliations").EnumerateArray().Select(a => a.GetString()));
        Assert.True(person.GetProperty("activeEnrollment").GetBoolean());
    }

    [Fact]
    public async Task PersoonZonderInschrijving()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        await academic.SeedAsync("persons", Seeds.Person("I1", "Sanne", "Student"));
        using var api = academic.Client(factory);

        var person = await api.GetJsonAsync($"/ooapi/v5/persons/{await academic.IdOfAsync("persons", "I1")}", Ct);

        Assert.Equal(["guest"], person.GetProperty("affiliations").EnumerateArray().Select(a => a.GetString()));
        Assert.False(person.GetProperty("activeEnrollment").GetBoolean());
        Assert.False(person.TryGetProperty("mail", out _));
    }

    [Fact]
    public async Task CursusMetIngesteldeTaalEnNiveau()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct, ("Ooapi:TeachingLanguage", "eng"), ("Ooapi:Level", "master"));
        await academic.SeedAsync("courses", Record(("courseId", "BIO101"), ("name", "Cell biology")));
        using var api = academic.Client(factory);

        var course = await api.GetJsonAsync($"/ooapi/v5/courses/{await academic.IdOfAsync("courses", "BIO101")}", Ct);

        Assert.Equal(("eng", "master"), (course.GetProperty("teachingLanguage").GetString(), course.GetProperty("level").GetString()));
        Assert.Equal("BIO101", course.GetProperty("abbreviation").GetString());
    }
}

public sealed class GetGeeftDeServicegegevens(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Servicegegevens()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct, ("Ooapi:ContactEmail", "servicedesk@example.test"));
        using var api = academic.Client(factory);

        var service = await api.GetJsonAsync("/ooapi/v5", Ct);

        Assert.Equal("servicedesk@example.test", service.GetProperty("contactEmail").GetString());
        Assert.StartsWith("https://", service.GetProperty("specification").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("https://", service.GetProperty("documentation").GetString(), StringComparison.Ordinal);
    }
}

public sealed class HetLezenVanOoapiVraagtDePermissieOoapiRead(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZonderToken()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        using var client = academic.Host.CreateBrowser();

        using var response = await client.GetAsync(Json.Url("/ooapi/v5/courses"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenZonderPermissie()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        using var api = academic.Api(factory, "doctrina-sis-sync", "test.write");

        using var response = await api.GetAsync(Json.Url("/ooapi/v5/courses"), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SessiecookieVolstaatNiet()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        using var browser = academic.Host.CreateBrowser();
        await browser.SignInAndGetAsync("/profile", factory.Idp, StubUser.Beheerder, Ct);

        using var response = await browser.GetAsync(Json.Url("/ooapi/v5/courses"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class DeModuleStaatUitAlsZeNietIsIngeschakeld(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ModuleUitgeschakeld()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, [(AccessHosts.Mapping(Ooapi.Reader, 0), "Ooapi.Read")], Ct);
        using var api = academic.Client(factory);

        using var response = await api.GetAsync(Json.Url("/ooapi/v5/courses"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed class GeenPersoonsgegevensInLogsBijOoapi(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LogsBijHetOpvragenVanPersonen()
    {
        await using var academic = await Ooapi.HostAsync(factory, Ct);
        var person = Seeds.Person("I6000777", "Geheimnaam", "Studentachternaam");
        person["mail"] = "geheim.mailadres@example.test";
        await academic.SeedAsync("persons", person);
        var logs = new CapturingLoggerProvider();
        using var host = academic.Host.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Trace" })).ConfigureServices(s => s.AddSingleton<ILoggerProvider>(logs)));
        using var api = host.CreateBrowser();
        api.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", factory.Idp.CreateAccessToken(DoctrinaHostFactory.ApiAudience, "doctrina-sis-sync", [Ooapi.Reader]));

        var list = await api.GetJsonAsync("/ooapi/v5/persons?primaryCode=I6000777", Ct);
        var id = list.GetProperty("items")[0].GetProperty("personId").GetString();
        await api.GetJsonAsync($"/ooapi/v5/persons/{id}", Ct);

        Assert.NotEmpty(logs.Logs);
        foreach (var secret in new[] { "Geheimnaam", "Studentachternaam", "geheim.mailadres", "I6000777" })
        {
            Assert.DoesNotContain(logs.Logs, l => l.Message.Contains(secret, StringComparison.Ordinal));
        }
    }
}
