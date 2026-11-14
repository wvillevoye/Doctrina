using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Doctrina.Host.IntegrationTests.ApiConventions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Host.IntegrationTests.Academic;

internal static class ImportApi
{
    public static Task<HttpResponseMessage> UploadAsync(this HttpClient api, string name, string content, string? type, bool dryRun, CancellationToken ct)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", name);
        if (type is not null)
        {
            form.Add(new StringContent(type), "type");
        }

        return api.PostAsync(Json.Url("/api/v1/imports" + (dryRun ? "?dryRun=true" : "")), form, ct);
    }

    /// <summary>Polls the job until it is completed or failed (at most 30 s); the Hangfire server runs it.</summary>
    public static async Task<JsonElement> WaitForAsync(this HttpClient api, Uri location, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var job = await (await api.GetAsync(location, ct)).ReadAsync(ct);
            if (job.GetProperty("status").GetString() is "completed" or "failed")
            {
                return job;
            }

            Assert.True(DateTime.UtcNow < deadline, $"The import did not finish within 30 s: {job}");
            await Task.Delay(250, ct);
        }
    }
}

// Spec academic-csv-import, requirement "Systemen importeren via de API".
public sealed class SystemenImporterenViaDeApi(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ImportViaDeApi()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory, roles: "test.import");

        using var response = await api.UploadAsync("courses.csv", "courseId,name\nBIO101,Cell biology\nWIS201,Calculus\n", "courses", dryRun: false, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.StartsWith("/api/v1/imports/", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        var job = await api.WaitForAsync(response.Headers.Location!, Ct);
        Assert.Equal(("completed", 2, "doctrina-sis-sync"), (job.GetProperty("status").GetString(), job.GetProperty("created").GetInt32(), job.GetProperty("startedBy").GetString()));
        Assert.Equal(2, await academic.QueryAsync(db => db.Courses.CountAsync(Ct)));
    }

    [Fact]
    public async Task ZonderAcademicImport()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory, roles: "test.write");

        using var response = await api.UploadAsync("courses.csv", "courseId,name\nBIO101,Cell biology\n", "courses", dryRun: false, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OnbekendeJob()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory, roles: "test.import");

        using var response = await api.GetAsync(Json.Url($"/api/v1/imports/{Guid.Empty}"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Academic.Import.NotFound", (await response.ProblemAsync(Ct)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task DryRunViaDeApiSlaatNietsOp()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory, roles: "test.import");

        using var response = await api.UploadAsync("courses.csv", "courseId,name\nBIO101,Cell biology\n", "courses", dryRun: true, Ct);
        var job = await api.WaitForAsync(response.Headers.Location!, Ct);

        Assert.Equal((true, 1), (job.GetProperty("dryRun").GetBoolean(), job.GetProperty("created").GetInt32()));
        Assert.Equal(0, await academic.QueryAsync(db => db.Courses.CountAsync(Ct)));
    }
}

// Spec academic-csv-import, requirement "De import-API vraagt Academic.Import en gebruikt de bron van de client".
public sealed class DeImportApiVraagtAcademicImportEnGebruiktDeBronVanDeClient(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BronVanDeClient()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, [("Api:Sources:roosterpakket", "Rooster")], Ct);
        using var api = academic.Api(factory, "roosterpakket", "test.import");

        using var response = await api.UploadAsync("courses.csv", "courseId,name\nBIO101,Cell biology\n", "courses", dryRun: false, Ct);
        await api.WaitForAsync(response.Headers.Location!, Ct);

        Assert.Equal("Rooster", await academic.QueryAsync(db => db.Courses.Select(c => c.Source).SingleAsync(Ct)));
    }
}

// Spec academic-csv-import: report through the API (JSON and CSV) and templates.
public sealed class HetRapportEnDeSjablonenViaDeApi(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RapportAlsJsonEnCsv()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory, roles: "test.import");
        using var upload = await api.UploadAsync("persons.csv", "personId,givenName,surname\nI6000001,Sanne,Student\nI6000002,Sam,\n", "persons", dryRun: false, Ct);
        await api.WaitForAsync(upload.Headers.Location!, Ct);

        var json = await (await api.GetAsync(Json.Url($"{upload.Headers.Location}/report"), Ct)).ReadAsync(Ct);
        using var csv = await api.GetAsync(Json.Url($"{upload.Headers.Location}/report?format=csv"), Ct);

        Assert.Equal(["created", "failed"], json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("result").GetString()));
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        var lines = (await csv.Content.ReadAsStringAsync(Ct)).TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("file,line,key,result,code,message", lines[0]);
        Assert.StartsWith("persons.csv,3,I6000002,failed,Academic.Person.Invalid,", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SjabloonViaDeApi()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory, roles: "test.import");

        using var response = await api.GetAsync(Json.Url("/api/v1/imports/templates/persons.csv"), Ct);

        Assert.Equal("personId,givenName,surnamePrefix,surname,displayName,mail,status\n", await response.Content.ReadAsStringAsync(Ct));
    }
}

// add-bulk-import task 2.4: the retention job is registered at start-up.
public sealed class DeBewaartermijnIsIngepland(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TerugkerendeTaakIsGeregistreerd()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);

        using var connection = academic.Host.Services.GetRequiredService<JobStorage>().GetConnection();
        var recurring = Hangfire.Storage.StorageConnectionExtensions.GetRecurringJobs(connection);

        Assert.Contains(recurring, j => j.Id == "academic-import-retention");
    }
}
