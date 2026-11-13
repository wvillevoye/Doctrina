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
}
