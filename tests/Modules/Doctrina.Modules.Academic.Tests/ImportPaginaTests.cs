using Bunit;
using Doctrina.Modules.Academic.Application;
using Doctrina.Modules.Academic.Components.Pages;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Academic.Tests;

// Spec academic-csv-import, requirement "Beheerders importeren één CSV-bestand per type" (the page; the permission
// check is tested against the host in Doctrina.Host.IntegrationTests).
public sealed class BeheerdersImporterenEenCsvBestandPerType : IAsyncDisposable
{
    private readonly BunitContext _context = new();
    private AcademicDatabase? _db;

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task PersonenImporteren()
    {
        var page = await RenderAsync();

        Upload(page, "persons", """
            personId,givenName,surname
            I6000001,Sanne,Student
            P7000002,Dirk,Docent
            P7000001,Bea,Beheerder
            """);

        page.WaitForAssertion(() => Assert.Contains("3 created", page.Find("[data-testid=import-summary]").TextContent, StringComparison.Ordinal));
        Assert.Equal(["created", "created", "created"], page.FindAll("[data-testid=import-report] tbody tr").Select(r => r.Children[2].TextContent));
        await using var read = _db!.CreateContext();
        Assert.Equal(["I6000001", "P7000001", "P7000002"], await read.Persons.Where(p => p.Source == "SIS").OrderBy(p => p.ExternalId).Select(p => p.ExternalId).ToListAsync(Ct));
    }

    [Fact]
    public async Task RapportToontDeFoutcode()
    {
        var page = await RenderAsync();

        Upload(page, "persons", "personId,givenName,surname\nI6000001,Sanne,\n");

        page.WaitForAssertion(() => Assert.Contains("1 failed", page.Find("[data-testid=import-summary]").TextContent, StringComparison.Ordinal));
        var row = page.Find("[data-testid=import-report] tbody tr");
        Assert.Equal("Academic.Person.Invalid", row.Children[3].TextContent);
        Assert.Contains("surname: Required.", row.Children[4].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OntbrekendeKolomWordtGemeld()
    {
        var page = await RenderAsync();

        Upload(page, "persons", "personId,givenName\nI6000001,Sanne\n");

        page.WaitForAssertion(() => Assert.Contains("Required column(s) missing: surname.", page.Find("[data-testid=result-error]").TextContent, StringComparison.Ordinal));
        Assert.Empty(page.FindAll("[data-testid=import-report]"));
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }

    private async Task<IRenderedComponent<Import>> RenderAsync()
    {
        _db = await AcademicDatabase.CreateAsync(Ct);
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
        _context.Services.AddScoped(_ => _db.CreateContext());
        _context.Services.AddScoped<AcademicImporter>();
        _context.Services.AddSingleton(Options.Create(new AcademicImportOptions()));
        return _context.Render<Import>();
    }

    private static void Upload(IRenderedComponent<Import> page, string type, string csv)
    {
        page.Find("select").Change(type);
        page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(csv, $"{type}.csv"));
        page.Find("form").Submit();
    }
}
