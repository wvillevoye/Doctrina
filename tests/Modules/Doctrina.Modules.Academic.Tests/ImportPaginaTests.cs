using Bunit;
using Doctrina.Modules.Academic.Application;
using Doctrina.Modules.Academic.Components.Pages;
using Doctrina.Modules.Academic.Domain;
using Hangfire;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Academic.Tests;

/// <summary>The import pages with a real LocalDB database and a recording job client (task 4.1).</summary>
public sealed class ImportPaginas : IAsyncDisposable
{
    private readonly BunitContext _context = new();
    private readonly FakeJobClient _jobs = new();
    private AcademicDatabase? _db;

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    internal async Task<AcademicDatabase> SetUpAsync()
    {
        _db = await AcademicDatabase.CreateAsync(Ct);
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
        _context.AddAuthorization().SetAuthorized("Beheer Doctrina");
        _context.Services.AddScoped(_ => _db.CreateContext());
        _context.Services.AddSingleton<IBackgroundJobClient>(_jobs);
        _context.Services.AddSingleton<TimeProvider>(_db.Time);
        _context.Services.AddSingleton(Options.Create(new AcademicImportOptions()));
        _context.Services.AddScoped<ImportJobs>();
        return _db;
    }

    internal IRenderedComponent<TPage> Render<TPage>(Action<ComponentParameterCollectionBuilder<TPage>>? parameters = null)
        where TPage : IComponent => _context.Render(parameters ?? (_ => { }));

    internal string NavigatedTo => _context.Services.GetRequiredService<NavigationManager>().Uri;

    internal int Queued => _jobs.Created.Count;

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}

// Spec academic-csv-import, requirement "Beheerders importeren één CSV-bestand per type" (the pages; the permission is
// tested against the host).
public sealed class BeheerdersImporterenEenCsvBestandPerType : IAsyncDisposable
{
    private readonly ImportPaginas _pages = new();

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task PersonenImporteren()
    {
        var db = await _pages.SetUpAsync();
        var upload = _pages.Render<Import>();

        await upload.InvokeAsync(() => upload.Find("select").ChangeAsync(new ChangeEventArgs { Value = "persons" }));
        upload.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(
            "personId,givenName,surname\nI6000001,Sanne,Student\nP7000002,Dirk,Docent\nP7000001,Bea,Beheerder\n", "persons.csv"));
        await upload.InvokeAsync(() => upload.Find("form").SubmitAsync());

        Guid jobId;
        await using (var context = db.CreateContext())
        {
            jobId = (await context.ImportJobs.SingleAsync(Ct)).Id;
        }

        Assert.EndsWith($"/academic/imports/{jobId:D}", _pages.NavigatedTo, StringComparison.Ordinal);
        Assert.Equal(1, _pages.Queued);

        await Imports.RunAsync(db, jobId); // what Hangfire does
        var page = _pages.Render<ImportJobPage>(p => p.Add(c => c.Id, jobId));

        page.WaitForAssertion(() => Assert.Contains("3 created", page.Find("[data-testid=job-summary]").TextContent, StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        Assert.Equal("completed", page.Find("[data-testid=job-status]").TextContent);
        await using var read = db.CreateContext();
        Assert.Equal(3, await read.Persons.CountAsync(p => p.Source == "SIS", Ct));
    }

    [Fact]
    public async Task MislukteRegelsOpDeJobpagina()
    {
        var db = await _pages.SetUpAsync();
        var job = await Imports.RunAsync(db, "persons.csv", Imports.Csv("personId,givenName,surname\nI6000001,Sanne,\n"), "persons");

        var page = _pages.Render<ImportJobPage>(p => p.Add(c => c.Id, job.Id));

        page.WaitForAssertion(() => Assert.Contains("1 failed", page.Find("[data-testid=job-summary]").TextContent, StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        var row = page.Find("[data-testid=failed-rows] tbody tr");
        Assert.Equal("Academic.Person.Invalid", row.Children[3].TextContent);
        Assert.Contains("surname: Required.", row.Children[4].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MislukteJob()
    {
        var db = await _pages.SetUpAsync();
        var job = await Imports.RunAsync(db, "persons.csv", Imports.Csv("personId,givenName\nI6000001,Sanne\n"), "persons");

        var page = _pages.Render<ImportJobPage>(p => p.Add(c => c.Id, job.Id));

        page.WaitForAssertion(() => Assert.Contains("surname", page.Find("[data-testid=job-message]").TextContent, StringComparison.Ordinal), TimeSpan.FromSeconds(10));
    }

    public ValueTask DisposeAsync() => _pages.DisposeAsync();
}

// Spec academic-csv-import, requirement "Een dry-run laat zien wat er zou gebeuren zonder op te slaan" (the page).
public sealed class EenDryRunOpDeJobpagina : IAsyncDisposable
{
    private readonly ImportPaginas _pages = new();

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task NaEenDryRunEchtImporteren()
    {
        var db = await _pages.SetUpAsync();
        var dryRun = await Imports.RunAsync(db, "courses.csv", Imports.Csv(Imports.Courses), "courses", dryRun: true);
        var page = _pages.Render<ImportJobPage>(p => p.Add(c => c.Id, dryRun.Id));
        page.WaitForAssertion(() => Assert.Contains("nothing was saved", page.Find("[data-testid=job-summary]").TextContent, StringComparison.Ordinal), TimeSpan.FromSeconds(10));

        await page.InvokeAsync(() => page.Find("[data-testid=import-for-real]").ClickAsync(new()));

        await using var context = db.CreateContext();
        var real = await context.ImportJobs.AsNoTracking().SingleAsync(j => j.RepeatOfId == dryRun.Id, Ct);
        Assert.False(real.DryRun);
        Assert.EndsWith($"/academic/imports/{real.Id:D}", _pages.NavigatedTo, StringComparison.Ordinal);
        await Imports.RunAsync(db, real.Id); // what Hangfire does
        Assert.Equal(1, await context.Courses.CountAsync(Ct));
    }

    public ValueTask DisposeAsync() => _pages.DisposeAsync();
}
