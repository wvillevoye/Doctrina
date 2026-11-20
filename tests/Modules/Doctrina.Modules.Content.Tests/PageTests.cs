using System.Security.Claims;
using Bunit;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Application;
using Doctrina.Modules.Content.Components;
using Doctrina.Modules.Content.Components.Pages;
using Doctrina.Modules.Content.Domain;
using Doctrina.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace Doctrina.Modules.Content.Tests;

// Spec course-content; tasks 5.2 and 5.3 of add-content: the pages call the service and show the outcome (access is
// tested against the host).

/// <summary>A LocalDB database, a fake directory with a lecturer and a student, and the services of the module in a bUnit context.</summary>
internal sealed class PageFixture : IAsyncDisposable
{
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(10); // LocalDB round trips take longer than bUnit's default of 1 s.

    private PageFixture(ContentDatabase db) => Db = db;

    public ContentDatabase Db { get; }

    public FakeDirectory Directory { get; } = new();

    public BunitContext Context { get; } = new();

    public OfferingMember Lecturer { get; private set; } = null!;

    public OfferingMember Student { get; private set; } = null!;

    public ContentService Service() => Db.CreateService(Directory);

    public static async Task<PageFixture> CreateAsync(OfferingRole signedInAs = OfferingRole.Lecturer, int maxSizeMb = 100)
    {
        var f = new PageFixture(await ContentDatabase.CreateAsync(Xunit.TestContext.Current.CancellationToken));
        f.Lecturer = f.Directory.Add("P7000002", OfferingRole.Lecturer);
        f.Student = f.Directory.Add("I6000001");
        var me = signedInAs == OfferingRole.Lecturer ? f.Lecturer : f.Student;
        f.Context.JSInterop.Mode = JSRuntimeMode.Loose;
        f.Context.AddAuthorization().SetAuthorized(me.Name).SetClaims(new Claim(FakeDirectory.PersonClaim, me.PersonId.ToString()));
        f.Context.Services.AddSingleton<IAcademicDirectory>(f.Directory);
        f.Context.Services.AddScoped(_ => f.Db.CreateContext());
        f.Context.Services.AddSingleton(f.Db.CreateFileStore(maxSizeMb));
        f.Context.Services.AddScoped<ContentService>();
        f.Context.Services.AddScoped<ContentAccess>();
        f.Context.Services.AddScoped<IntroductionService>();
        return f;
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await Db.DisposeAsync();
    }
}

public sealed class HetContentbeheerOpDePagina : IAsyncDisposable
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private PageFixture? _f;

    [Fact]
    public async Task SectieLinkEnBestandToevoegenEnPubliceren()
    {
        _f = await PageFixture.CreateAsync();
        var page = Render();

        await SubmitAsync(page, "[data-testid=new-section]", ("input[type=text]", "Week 1"));
        Assert.Equal("Section “Week 1” created as a draft.", page.Find("[data-testid=content-message] p").TextContent.Trim());
        var sectionId = (await _f.Service().SectionsAsync(_f.Directory.OfferingId, true, Ct)).Single().Id.ToString("D");

        await SubmitAsync(page, "[data-testid=add-link]", ("select", sectionId), ("input[type=text]", "Course guide"), ("input[type=url]", "https://example.test/guide"));
        Assert.Equal("Link “Course guide” added as a draft.", page.Find("[data-testid=content-message] p").TextContent.Trim());

        await UploadAsync(page, sectionId, "syllabus.pdf", [1, 2, 3]);
        Assert.Equal("File “syllabus.pdf” added as a draft.", page.Find("[data-testid=content-message] p").TextContent.Trim());

        Assert.Equal(["Course guide", "syllabus.pdf"], ItemTitles(page));
        Assert.Contains("File: syllabus.pdf, 1 KB", page.FindAll("[data-testid=item]")[1].TextContent, StringComparison.Ordinal);
        Assert.All(page.FindAll("[data-testid=item]"), item => Assert.Contains("Draft", item.TextContent, StringComparison.Ordinal));

        await page.InvokeAsync(() => page.FindAll("[data-testid=publish-item]")[0].ClickAsync());
        Assert.Equal("“Course guide” is published.", page.Find("[data-testid=content-message] p").TextContent.Trim());
        Assert.Contains("Published", page.FindAll("[data-testid=item]")[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("Hide", page.FindAll("[data-testid=publish-item]")[0].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EenLinkZonderGeldigAdresToontDeMelding()
    {
        _f = await PageFixture.CreateAsync();
        var section = await _f.Service().SectionAsync(_f.Directory, "Week 1");
        var page = Render();

        await SubmitAsync(page, "[data-testid=add-link]", ("select", section.Id.ToString("D")), ("input[type=text]", "Bad"), ("input[type=url]", "javascript:alert(1)"));

        Assert.Contains(ContentService.LinkMessage, page.Find("[data-testid=result-error]").TextContent, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("[data-testid=item]"));
    }

    [Fact]
    public async Task EenBestandDatTeGrootIsToontDeMelding()
    {
        _f = await PageFixture.CreateAsync(maxSizeMb: 1);
        var section = await _f.Service().SectionAsync(_f.Directory, "Week 1");
        var page = Render();

        await UploadAsync(page, section.Id.ToString("D"), "big.bin", new byte[2 * 1024 * 1024]);

        Assert.Contains("The file is larger than 1 MB.", page.Find("[data-testid=result-error]").TextContent, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("[data-testid=item]"));
        Assert.Empty((await _f.Service().SectionsAsync(_f.Directory.OfferingId, true, Ct)).Single().Items);
    }

    [Fact]
    public async Task VerplaatsenEnVerwijderen()
    {
        _f = await PageFixture.CreateAsync();
        var section = await _f.Service().SectionAsync(_f.Directory, "Week 1");
        var service = _f.Service();
        await service.LinkAsync(section.Id, "A");
        await service.LinkAsync(section.Id, "B");
        await service.LinkAsync(section.Id, "C");
        var page = Render();
        page.WaitForElement("[data-testid=item]", PageFixture.Wait);

        await page.InvokeAsync(() => page.FindAll("[data-testid=item-up]")[2].ClickAsync());
        Assert.Equal(["A", "C", "B"], ItemTitles(page));

        await page.InvokeAsync(() => page.FindAll("[data-testid=delete-item]")[0].ClickAsync()); // A
        await page.InvokeAsync(() => page.Find("[data-testid=confirm-delete]").ClickAsync());

        Assert.Equal(["C", "B"], ItemTitles(page));
        Assert.Equal("“A” deleted.", page.Find("[data-testid=content-message] p").TextContent.Trim());
    }

    [Fact]
    public async Task EenSectieVerbergenEnVerplaatsen()
    {
        _f = await PageFixture.CreateAsync();
        var service = _f.Service();
        await service.SectionAsync(_f.Directory, "Week 1", published: true);
        await service.SectionAsync(_f.Directory, "Week 2");
        var page = Render();
        page.WaitForElement("[data-testid=section]", PageFixture.Wait);

        await page.InvokeAsync(() => page.FindAll("[data-testid=section-down]")[0].ClickAsync());
        Assert.Equal(["Week 2", "Week 1"], page.FindAll("[data-testid=section] h2").Select(e => e.TextContent.Trim()));

        await page.InvokeAsync(() => page.FindAll("[data-testid=publish-section]")[1].ClickAsync()); // Week 1: Hide
        Assert.Equal("“Week 1” is hidden.", page.Find("[data-testid=content-message] p").TextContent.Trim());
        Assert.Empty(await _f.Service().TitlesAsync(_f.Directory.OfferingId, drafts: false));
    }

    public async ValueTask DisposeAsync()
    {
        if (_f is not null)
        {
            await _f.DisposeAsync();
        }
    }

    /// <summary>Item titles; an external link has a hidden "(opens in a new window)" for screen readers.</summary>
    private static string[] ItemTitles(IRenderedComponent<ManageContent> page) =>
        [.. page.FindAll("[data-testid=item-title]").Select(e => e.TextContent.Replace("(opens in a new window)", string.Empty, StringComparison.Ordinal).Trim())];

    private IRenderedComponent<ManageContent> Render() =>
        _f!.Context.Render<ManageContent>(p => p.Add(c => c.OfferingId, _f.Directory.OfferingId));

    private static async Task UploadAsync(IRenderedComponent<ManageContent> page, string sectionId, string fileName, byte[] bytes)
    {
        page.WaitForElement("[data-testid=add-file]", PageFixture.Wait);
        await page.InvokeAsync(() => page.Find("[data-testid=add-file] select").ChangeAsync(new ChangeEventArgs { Value = sectionId }));
        await page.InvokeAsync(() => page.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().UploadFiles(InputFileContent.CreateFromBinary(bytes, fileName, contentType: "application/pdf")));
        await page.InvokeAsync(() => page.Find("[data-testid=add-file]").SubmitAsync());
    }

    /// <summary>Fills a form and submits it, awaiting the (async) event handlers; null values are skipped.</summary>
    private static async Task SubmitAsync(IRenderedComponent<ManageContent> page, string formSelector, params (string Selector, object? Value)[] fields)
    {
        page.WaitForElement(formSelector, PageFixture.Wait); // the page loads its data asynchronously on the first render
        foreach (var (selector, value) in fields)
        {
            // Find the field again every time (each change re-renders the form), inside the renderer's dispatcher.
            await page.InvokeAsync(() => page.Find($"{formSelector} {selector}").ChangeAsync(new ChangeEventArgs { Value = value }));
        }

        await page.InvokeAsync(() => page.Find(formSelector).SubmitAsync());
    }
}

public sealed class PaginasWordenInMarkdownGeschrevenEnVeiligWeergegeven_Bewerken : IAsyncDisposable
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private PageFixture? _f;

    [Fact]
    public async Task VoorbeeldTijdensHetBewerken()
    {
        _f = await PageFixture.CreateAsync();
        var section = await _f.Service().SectionAsync(_f.Directory, "Week 1");
        var item = (await _f.Service().AddPageAsync(section.Id, "Introduction", Ct)).Value!;
        var page = _f.Context.Render<EditContentPage>(p => p.Add(c => c.OfferingId, _f.Directory.OfferingId).Add(c => c.ItemId, item.Id));
        page.WaitForElement("textarea", PageFixture.Wait);

        await page.InvokeAsync(() => page.Find("textarea").ChangeAsync(new ChangeEventArgs { Value = "*draft*" }));
        Assert.Empty(page.FindAll("[data-testid=preview]"));
        await page.InvokeAsync(() => page.Find("[data-testid=preview-toggle]").ClickAsync());

        Assert.Equal("draft", page.Find("[data-testid=preview] em").TextContent);
        Assert.Equal("Hide preview", page.Find("[data-testid=preview-toggle]").TextContent.Trim());
        var stored = (await _f.Service().FindItemAsync(_f.Directory.OfferingId, item.Id, includeDrafts: true, Ct))!;
        Assert.Equal(string.Empty, stored.Body); // a preview doesn't save
    }

    [Fact]
    public async Task OpslaanBewaartTitelEnTekst()
    {
        _f = await PageFixture.CreateAsync();
        var section = await _f.Service().SectionAsync(_f.Directory, "Week 1");
        var item = (await _f.Service().AddPageAsync(section.Id, "Introduction", Ct)).Value!;
        var page = _f.Context.Render<EditContentPage>(p => p.Add(c => c.OfferingId, _f.Directory.OfferingId).Add(c => c.ItemId, item.Id));
        page.WaitForElement("textarea", PageFixture.Wait);

        await page.InvokeAsync(() => page.Find("textarea").ChangeAsync(new ChangeEventArgs { Value = "# Welcome" }));
        await page.InvokeAsync(() => page.Find("[data-testid=edit-page-form]").SubmitAsync());

        var stored = (await _f.Service().FindItemAsync(_f.Directory.OfferingId, item.Id, includeDrafts: true, Ct))!;
        Assert.Equal(("Introduction", "# Welcome"), (stored.Title, stored.Body));
    }

    public async ValueTask DisposeAsync()
    {
        if (_f is not null)
        {
            await _f.DisposeAsync();
        }
    }
}

public sealed class BestandenWordenGeuploadEnGedownload_Knop : IAsyncDisposable
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private PageFixture? _f;

    [Fact]
    public async Task DownloadRoeptDeGedeeldeDownloadAanMetDeBestandsnaam()
    {
        _f = await PageFixture.CreateAsync(OfferingRole.Student);
        var section = await _f.Service().SectionAsync(_f.Directory, "Week 1", published: true);
        var file = (await _f.Service().AddFileAsync(section.Id, null, "syllabus.pdf", "application/pdf", new MemoryStream([1, 2, 3]), Ct)).Value!;
        await _f.Service().SetItemPublishedAsync(file.Id, true, Ct);
        var module = _f.Context.JSInterop.SetupModule(Downloads.ModulePath);
        module.SetupVoid("save", _ => true).SetVoidResult();
        var button = _f.Context.Render<DownloadButton>(p => p.Add(c => c.OfferingId, _f.Directory.OfferingId).Add(c => c.ItemId, file.Id));

        await button.InvokeAsync(() => button.Find("[data-testid=download]").ClickAsync());

        var save = Assert.Single(module.Invocations["save"]);
        Assert.Equal("syllabus.pdf", save.Arguments[0]);
        Assert.IsType<DotNetStreamReference>(save.Arguments[1]);
        Assert.Empty(button.FindAll("[data-testid=result-error]"));
    }

    [Fact]
    public async Task EenStudentKanEenConceptNietDownloaden()
    {
        _f = await PageFixture.CreateAsync(OfferingRole.Student);
        var section = await _f.Service().SectionAsync(_f.Directory, "Week 1", published: true);
        var file = (await _f.Service().AddFileAsync(section.Id, null, "syllabus.pdf", "application/pdf", new MemoryStream([1, 2, 3]), Ct)).Value!;
        var module = _f.Context.JSInterop.SetupModule(Downloads.ModulePath);
        module.SetupVoid("save", _ => true).SetVoidResult();
        var button = _f.Context.Render<DownloadButton>(p => p.Add(c => c.OfferingId, _f.Directory.OfferingId).Add(c => c.ItemId, file.Id));

        await button.InvokeAsync(() => button.Find("[data-testid=download]").ClickAsync());

        Assert.Empty(module.Invocations["save"]);
        Assert.Contains("The item is not available.", button.Find("[data-testid=result-error]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EenOntbrekendBestandToontDeMelding()
    {
        _f = await PageFixture.CreateAsync(OfferingRole.Student);
        var section = await _f.Service().SectionAsync(_f.Directory, "Week 1", published: true);
        var file = (await _f.Service().AddFileAsync(section.Id, null, "syllabus.pdf", null, new MemoryStream([1]), Ct)).Value!;
        await _f.Service().SetItemPublishedAsync(file.Id, true, Ct);
        Directory.Delete(_f.Db.FilesPath, recursive: true);
        var button = _f.Context.Render<DownloadButton>(p => p.Add(c => c.OfferingId, _f.Directory.OfferingId).Add(c => c.ItemId, file.Id));

        await button.InvokeAsync(() => button.Find("[data-testid=download]").ClickAsync());

        Assert.Equal(ContentErrors.FileMissing, button.Find("[data-testid=result-error]").GetAttribute("data-error-code"));
    }

    public async ValueTask DisposeAsync()
    {
        if (_f is not null)
        {
            await _f.DisposeAsync();
        }
    }
}
