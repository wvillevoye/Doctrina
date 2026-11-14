using System.Text.Json;
using Doctrina.Modules.Academic.Application;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Academic.Tests;

// Task 7.1 (design D9): the CSV files that dev-stack/scripts/generate-realm.ps1 writes import without errors, and the
// persons have the same personId as the users in the Keycloak realm.
public sealed class DeTestdataUitDeDevStack
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string DevStack(params string[] parts) => Path.Combine([AcademicDatabase.RepoRoot(), "dev-stack", .. parts]);

    [Fact]
    public async Task ImporteertFoutloosInDeVasteVolgorde()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        // The zip the generator writes next to the six CSV files (change add-bulk-import), as a user would upload it.
        var job = await Imports.RunAsync(db, "academic-testdata.zip", await File.ReadAllBytesAsync(DevStack("data", "academic-testdata.zip"), Ct));

        Assert.Equal(Domain.ImportJobStatus.Completed, job.Status);
        Assert.Null(job.Warnings);
        Assert.All(await Imports.RowsAsync(db, job.Id), r => Assert.True(r.Outcome == Domain.ImportRowOutcome.Created, $"{r.File} line {r.Line}: {r.Message}"));
        await using var context = db.CreateContext();
        Assert.Equal(3, await context.Associations.CountAsync(a => a.Person.ExternalId == "I6000001", Ct));
    }

    [Fact]
    public async Task PersonenHebbenDezelfdePersonIdAlsDeRealm()
    {
        using var realm = JsonDocument.Parse(await File.ReadAllTextAsync(DevStack("realm", "doctrina-realm.json"), Ct));
        var realmIds = realm.RootElement.GetProperty("users").EnumerateArray()
            .Where(u => u.TryGetProperty("attributes", out var a) && a.TryGetProperty("personId", out _))
            .Select(u => u.GetProperty("attributes").GetProperty("personId")[0].GetString())
            .Order(StringComparer.Ordinal);

        var csvIds = (await File.ReadAllLinesAsync(DevStack("data", "persons.csv"), Ct)).Skip(1)
            .Where(l => l.Length > 0).Select(l => l.Split(',')[0]).Order(StringComparer.Ordinal);

        Assert.Equal(realmIds, csvIds);
        Assert.Contains("I6000001", csvIds);
        Assert.Contains("P7000002", csvIds);
        Assert.Contains("P7000001", csvIds);
    }
}
