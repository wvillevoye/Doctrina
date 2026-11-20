using Bunit;
using Microsoft.AspNetCore.Components;
using Doctrina.Modules.Content.Application;
using Doctrina.Modules.Content.Components;
using Doctrina.Modules.Content.Components.Pages;

namespace Doctrina.Modules.Content.Tests;

// Spec markdown-editor (change add-markdown-toolbar): the rules of the toolbar in MarkdownFormatter.
public sealed class DeWerkbalkVoegtMarkdownSyntaxIn
{
    private static EditorState Apply(MarkdownActionKind kind, string text, int start, int end, string? color = null) =>
        MarkdownFormatter.Apply(new MarkdownAction(kind, color), new EditorState(text, start, end));

    [Fact]
    public void KopMakenVanEenRegel()
    {
        var s = Apply(MarkdownActionKind.Heading2, "Title\nBody", 2, 2);
        Assert.Equal("## Title\nBody", s.Text);
    }

    [Fact]
    public void KopVervangtEenBestaandeKop()
    {
        Assert.Equal("## Title", Apply(MarkdownActionKind.Heading2, "# Title", 0, 0).Text);
        Assert.Equal("Title", Apply(MarkdownActionKind.Heading1, "# Title", 0, 0).Text); // same level again removes it
    }

    [Fact]
    public void VetOmDeSelectie()
    {
        var s = Apply(MarkdownActionKind.Bold, "Hello", 0, 5);
        Assert.Equal(("**Hello**", 2, 7), (s.Text, s.Start, s.End));
    }

    [Fact]
    public void PlaatshouderZonderSelectie()
    {
        var s = Apply(MarkdownActionKind.Italic, "a b", 1, 1);
        Assert.Equal(("a*text* b", 2, 6), (s.Text, s.Start, s.End));
    }

    [Fact]
    public void OpsommingOverMeerdereRegels()
    {
        Assert.Equal("- one\n- two", Apply(MarkdownActionKind.BulletedList, "one\ntwo", 0, 7).Text);
        Assert.Equal("one\ntwo", Apply(MarkdownActionKind.BulletedList, "- one\n- two", 0, 11).Text);
    }

    [Fact]
    public void LinkMaken()
    {
        var s = Apply(MarkdownActionKind.Link, "see docs", 4, 8);
        Assert.Equal("see [docs](https://)", s.Text);
        Assert.Equal("https://", s.Text[s.Start..s.End]);
    }

    [Fact]
    public void TekstCentreren()
    {
        Assert.Equal("{center}Welcome{/center}", Apply(MarkdownActionKind.Align, "Welcome", 0, 7, "center").Text);
        Assert.Equal("x", Apply(MarkdownActionKind.Align, "x", 0, 1, "danger").Text); // a colour is not an alignment
    }

    [Fact]
    public void KleurEnUitlijningCombineren()
    {
        var aligned = Apply(MarkdownActionKind.Align, "Hi", 0, 2, "center"); // the text stays selected inside the markers
        var both = MarkdownFormatter.Apply(new MarkdownAction(MarkdownActionKind.Color, "danger"), aligned);

        Assert.Equal("{center}{danger}Hi{/danger}{/center}", both.Text);
    }

    [Fact]
    public void KleurOmDeSelectieEnOnbekendeNaamDoetNiets()
    {
        Assert.Equal("{danger}x{/danger}", Apply(MarkdownActionKind.Color, "x", 0, 1, "danger").Text);
        Assert.Equal("x", Apply(MarkdownActionKind.Color, "x", 0, 1, "cyan").Text);
    }
}

// Spec markdown-editor: the component, with the script replaced by a test double.
public sealed class DeWerkbalkIsToegankelijkEnHoudtDeFocusInHetTekstveld : IDisposable
{
    private readonly BunitContext _ctx = new();
    private readonly BunitJSModuleInterop _module;
    private string? _value = "typed";

    public DeWerkbalkIsToegankelijkEnHoudtDeFocusInHetTekstveld()
    {
        _module = _ctx.JSInterop.SetupModule("./_content/Doctrina.Modules.Content/md-editor.js");
        _module.Setup<EditorState>("getState", _ => true).SetResult(new EditorState("typed", 0, 5)); // as in the browser
        _module.SetupVoid("setState", _ => true);
        _module.SetupVoid("keepSelection", _ => true); // buttons do not take the focus, so the selection stays visible
    }

    private IRenderedComponent<MarkdownEditor> Render() => _ctx.Render<MarkdownEditor>(p => p
        .Add(c => c.Label, "Text").Add(c => c.Value, _value).Add(c => c.ValueExpression, () => _value));

    [Fact]
    public void NamenEnRol()
    {
        var page = Render();

        Assert.Equal("Formatting", page.Find("[role=toolbar]").GetAttribute("aria-label"));
        var names = page.FindAll("[role=toolbar] > button .visually-hidden").Select(n => n.TextContent).ToList();
        Assert.Equal(["Heading 1", "Heading 2", "Heading 3", "Bold", "Italic", "Bulleted list", "Link", "Align left", "Align center", "Align right"], names);
        Assert.All(page.FindAll("[role=toolbar] button"), b => Assert.Equal("button", b.GetAttribute("type")));
    }

    [Fact]
    public async Task FocusBlijftInHetVeld()
    {
        var page = Render();

        await page.InvokeAsync(() => page.Find("button:has(.visually-hidden)").ClickAsync()); // Heading 1
        var call = _module.Invocations["setState"].Single();

        Assert.Equal("# typed", ((EditorState)call.Arguments[1]!).Text); // setState puts back focus and selection in the textarea
    }

    [Fact]
    public async Task TekstDieAlGetypteWasGaatNietVerloren()
    {
        var page = Render();
        _value = "stale"; // what the server knows; the browser state ("typed") wins

        await page.InvokeAsync(() => page.FindAll("[role=toolbar] > button")[3].ClickAsync()); // Bold

        Assert.Equal("**typed**", ((EditorState)_module.Invocations["setState"].Single().Arguments[1]!).Text);
    }

    [Fact]
    public async Task KleurKiezen()
    {
        var page = Render();

        await page.InvokeAsync(() => page.Find("[data-color=warning]").ClickAsync());

        Assert.Equal("{warning}typed{/warning}", ((EditorState)_module.Invocations["setState"].Single().Arguments[1]!).Text);
    }

    [Fact]
    public void KleurkeuzeZonderEigenKleurcode()
    {
        var page = Render();

        var colors = page.FindAll("[data-color]").Select(b => b.GetAttribute("data-color")!).ToList();

        Assert.Equal(Markdown.ColorNames, colors);
        Assert.Empty(page.FindAll("input[type=color]"));
    }

    public void Dispose() => _ctx.Dispose();
}

// Spec markdown-editor, requirement "Pagina's voor Markdown gebruiken dezelfde editor".
public sealed class PaginasVoorMarkdownGebruikenDezelfdeEditor : IAsyncDisposable
{
    private PageFixture? _f;

    [Fact]
    public async Task EditorBijDeIntroductieEnSjablonen()
    {
        _f = await PageFixture.CreateAsync();

        var intro = _f.RenderEdit();
        Assert.NotNull(intro.Find("[data-testid=markdown-editor] [role=toolbar]"));

        var templates = _f.Context.Render<ManageTemplatesPage>();
        templates.WaitForElement("[data-testid=templates]", PageFixture.Wait);
        Assert.NotNull(templates.Find("[data-testid=template-form] [role=toolbar]"));
    }

    [Fact]
    public async Task VoorbeeldOpenenEnSluiten_HetVoorbeeldIsEenVerschuifbaarVenster()
    {
        _f = await PageFixture.CreateAsync();
        var page = _f.RenderEdit();
        await page.InvokeAsync(() => page.Find("textarea").ChangeAsync(new ChangeEventArgs { Value = "*draft*" }));

        await page.InvokeAsync(() => page.Find("[data-testid=preview-toggle]").ClickAsync());
        Assert.Equal("draft", page.Find("[data-testid=preview] em").TextContent);
        Assert.NotNull(page.Find("[data-testid=preview-handle]"));

        await page.InvokeAsync(() => page.Find("[data-testid=preview-close]").ClickAsync());
        Assert.Empty(page.FindAll("[data-testid=preview]"));
    }

    [Fact]
    public async Task EditorOpDeContentpagina()
    {
        _f = await PageFixture.CreateAsync();
        var section = await _f.Service().SectionAsync(_f.Directory, "Week 1");
        var item = (await _f.Service().AddPageAsync(section.Id, "Introduction", Xunit.TestContext.Current.CancellationToken)).Value!;

        var page = _f.Context.Render<EditContentPage>(p => p.Add(c => c.OfferingId, _f.Directory.OfferingId).Add(c => c.ItemId, item.Id));
        page.WaitForElement("textarea", PageFixture.Wait);

        Assert.NotNull(page.Find("[data-testid=markdown-editor] [role=toolbar]"));
    }

    public async ValueTask DisposeAsync()
    {
        if (_f is not null)
        {
            await _f.DisposeAsync();
        }
    }
}
