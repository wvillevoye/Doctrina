using System.Net;
using Doctrina.TestKit.Authentication;

namespace Doctrina.Host.IntegrationTests.Academic;

// Spec academic-csv-import, requirement "Beheerders importeren één CSV-bestand per type" (permission; the upload itself
// is tested with bUnit in Doctrina.Modules.Academic.Tests).
public sealed class BeheerdersImporterenEenCsvBestandPerType(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ImportZonderPermissie()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);

        var (status, html) = await academic.GetAsync(factory, StubUser.Student, "/academic/import", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains(">Access denied</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportpaginaVoorBeheerders()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);

        var (status, html) = await academic.GetAsync(factory, StubUser.Beheerder, "/academic/import", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Import · Doctrina", html, StringComparison.Ordinal);
        Assert.Contains("type=\"file\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecenteImportsVoorBeheerders()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var api = academic.Api(factory, roles: "test.import");
        using var upload = await api.UploadAsync("courses.csv", "courseId,name\nBIO101,Cell biology\n", "courses", dryRun: false, Ct);
        await api.WaitForAsync(upload.Headers.Location!, Ct);

        var (status, html) = await academic.GetAsync(factory, StubUser.Beheerder, "/academic/imports", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var row = Doctrina.Host.IntegrationTests.HostUi.Html.Parse(html).QuerySelector("[data-testid=import-jobs] tbody tr")!;
        Assert.Contains("1 created", row.TextContent, StringComparison.Ordinal);
    }
}
