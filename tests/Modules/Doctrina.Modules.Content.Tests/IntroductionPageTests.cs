using Bunit;
using Doctrina.Modules.Content.Application;
using Doctrina.Modules.Content.Components.Pages;
using Doctrina.Modules.Content.Domain;
using Microsoft.AspNetCore.Components;

namespace Doctrina.Modules.Content.Tests;

// Spec course-introduction, tasks 5.2, 5.4 and 5.5 of add-course-introduction: the pages call the service and show the
// outcome (access and the course page are tested against the host).

internal static class IntroductionPages
{
    public static IntroductionService Introductions(this PageFixture f) => new(f.Db.CreateContext());

    public static IRenderedComponent<EditIntroductionPage> RenderEdit(this PageFixture f)
    {
        var page = f.Context.Render<EditIntroductionPage>(p => p.Add(c => c.OfferingId, f.Directory.OfferingId));
        page.WaitForElement("textarea", PageFixture.Wait);
        return page;
    }

    public static async Task<Guid> TemplateAsync(this PageFixture f, string name, IntroductionLayout layout, string startBody)
    {
        var service = f.Introductions();
        Assert.True((await service.SaveTemplateAsync(null, name, layout, startBody, Xunit.TestContext.Current.CancellationToken)).IsSuccess);
        return (await service.TemplatesAsync(Xunit.TestContext.Current.CancellationToken)).Single(t => t.Name == name).Id;
    }
}

public sealed class DocentenBewerkenDeIntroductieInMarkdown_Pagina : IAsyncDisposable
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private PageFixture? _f;

    [Fact]
    public async Task DocentSchrijftEnPubliceert()
    {
        _f = await PageFixture.CreateAsync();
        var page = _f.RenderEdit();

        await page.InvokeAsync(() => page.Find("textarea").ChangeAsync(new ChangeEventArgs { Value = "# Welcome" }));
        await page.InvokeAsync(() => page.Find("input[type=checkbox]").ChangeAsync(new ChangeEventArgs { Value = true }));
        await page.InvokeAsync(() => page.Find("[data-testid=edit-introduction-form]").SubmitAsync());

        var stored = await _f.Introductions().FindAsync(_f.Directory.OfferingId, Ct);
        Assert.Equal(("# Welcome", true), (stored!.Body, stored.Published));
    }

    [Fact]
    public async Task VoorbeeldTijdensHetBewerken()
    {
        _f = await PageFixture.CreateAsync();
        var page = _f.RenderEdit();

        await page.InvokeAsync(() => page.Find("textarea").ChangeAsync(new ChangeEventArgs { Value = "*draft*" }));
        await page.InvokeAsync(() => page.Find("[data-testid=preview-toggle]").ClickAsync());

        Assert.Equal("draft", page.Find("[data-testid=preview] em").TextContent);
        Assert.Null(await _f.Introductions().FindAsync(_f.Directory.OfferingId, Ct)); // a preview does not save
    }

    [Fact]
    public async Task TeLangeTekst()
    {
        _f = await PageFixture.CreateAsync();
        var page = _f.RenderEdit();

        await page.InvokeAsync(() => page.Find("textarea").ChangeAsync(new ChangeEventArgs { Value = new string('x', 50_001) }));
        await page.InvokeAsync(() => page.Find("[data-testid=edit-introduction-form]").SubmitAsync());

        Assert.Contains("The text is longer than 50,000 characters.", page.Markup, StringComparison.Ordinal);
        Assert.Null(await _f.Introductions().FindAsync(_f.Directory.OfferingId, Ct));
    }

    public async ValueTask DisposeAsync()
    {
        if (_f is not null)
        {
            await _f.DisposeAsync();
        }
    }
}

public sealed class EenUitvoeringKiestEenSjabloonDatDeOpmaakBepaalt_Pagina : IAsyncDisposable
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private PageFixture? _f;

    [Fact]
    public async Task StartinhoudBijEenLegeIntroductie()
    {
        _f = await PageFixture.CreateAsync();
        var banner = await _f.TemplateAsync("Course with banner", IntroductionLayout.Banner, "# Welcome");
        var page = _f.RenderEdit();

        await page.InvokeAsync(() => page.Find("select").ChangeAsync(new ChangeEventArgs { Value = banner.ToString("D") }));

        Assert.Equal("# Welcome", page.Find("textarea").GetAttribute("value") ?? page.Find("textarea").TextContent);
    }

    [Fact]
    public async Task SjabloonWisselenBehoudtDeTekst()
    {
        _f = await PageFixture.CreateAsync();
        var banner = await _f.TemplateAsync("Course with banner", IntroductionLayout.Banner, "# Welcome");
        Assert.True((await _f.Introductions().SaveAsync(_f.Directory.OfferingId, null, "Hello", true, Ct)).IsSuccess);
        var page = _f.RenderEdit();

        await page.InvokeAsync(() => page.Find("select").ChangeAsync(new ChangeEventArgs { Value = banner.ToString("D") }));
        await page.InvokeAsync(() => page.Find("[data-testid=edit-introduction-form]").SubmitAsync());

        var stored = await _f.Introductions().FindAsync(_f.Directory.OfferingId, Ct);
        Assert.Equal(("Hello", IntroductionLayout.Banner), (stored!.Body, stored.Layout));
    }

    [Fact]
    public async Task VerwijderdSjabloon()
    {
        _f = await PageFixture.CreateAsync();
        var banner = await _f.TemplateAsync("Course with banner", IntroductionLayout.Banner, string.Empty);
        var service = _f.Introductions();
        await service.SaveAsync(_f.Directory.OfferingId, banner, "Hello", true, Ct);

        Assert.True((await service.DeleteTemplateAsync(banner, Ct)).IsSuccess);

        var stored = await _f.Introductions().FindAsync(_f.Directory.OfferingId, Ct);
        Assert.Equal(("Hello", IntroductionLayout.Plain), (stored!.Body, stored.Layout));
    }

    public async ValueTask DisposeAsync()
    {
        if (_f is not null)
        {
            await _f.DisposeAsync();
        }
    }
}

public sealed class BeheerdersBeherenIntroductiesjablonen_Pagina : IAsyncDisposable
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private PageFixture? _f;

    private IRenderedComponent<ManageTemplatesPage> Render()
    {
        var page = _f!.Context.Render<ManageTemplatesPage>();
        page.WaitForElement("[data-testid=template-form]", PageFixture.Wait);
        return page;
    }

    [Fact]
    public async Task BeheerderMaaktEenSjabloon()
    {
        _f = await PageFixture.CreateAsync();
        var page = Render();

        await page.InvokeAsync(() => page.Find("[data-testid=template-form] input[type=text]").ChangeAsync(new ChangeEventArgs { Value = "Course with banner" }));
        await page.InvokeAsync(() => page.Find("[data-testid=template-form] select").ChangeAsync(new ChangeEventArgs { Value = nameof(IntroductionLayout.Banner) }));
        await page.InvokeAsync(() => page.Find("[data-testid=template-form] textarea").ChangeAsync(new ChangeEventArgs { Value = "# Welcome" }));
        await page.InvokeAsync(() => page.Find("[data-testid=template-form]").SubmitAsync());

        page.WaitForAssertion(() => Assert.Contains("Course with banner", page.Find("[data-testid=templates]").TextContent, StringComparison.Ordinal), PageFixture.Wait);
        var stored = (await _f.Introductions().TemplatesAsync(Ct)).Single(t => t.Name == "Course with banner");
        Assert.Equal((IntroductionLayout.Banner, "# Welcome"), (stored.Layout, stored.StartBody));
    }

    [Fact]
    public async Task SjabloonZonderNaam()
    {
        _f = await PageFixture.CreateAsync();
        var page = Render();

        await page.InvokeAsync(() => page.Find("[data-testid=template-form]").SubmitAsync());

        Assert.Contains("Enter a name.", page.Markup, StringComparison.Ordinal);
        Assert.Equal(["Default"], (await _f.Introductions().TemplatesAsync(Ct)).Select(t => t.Name));
    }

    [Fact]
    public async Task StandaardsjabloonBlijftBestaan()
    {
        _f = await PageFixture.CreateAsync();
        var service = _f.Introductions();
        var defaultTemplate = (await service.TemplatesAsync(Ct)).Single(t => t.IsDefault);

        var result = await service.DeleteTemplateAsync(defaultTemplate.Id, Ct);

        Assert.Equal(ContentErrors.TemplateDefaultProtected, result.Error!.Code);
        Assert.Equal("The default template cannot be deleted.", result.Error.Message);
        Assert.Single(await _f.Introductions().TemplatesAsync(Ct));
        var page = Render();
        Assert.Empty(page.FindAll("[data-testid=delete-template]")); // no delete button for "Default"
    }

    public async ValueTask DisposeAsync()
    {
        if (_f is not null)
        {
            await _f.DisposeAsync();
        }
    }
}
