using Bunit;
using Doctrina.UI.Components;
using Doctrina.UI.Theming;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

// Spec app-shell; taak 6.1 van add-design-system (design D3, D9). Klasse = requirement, methode = scenario.
namespace Doctrina.UI.Tests.Components;

/// <summary>Antiforgery-token zoals de host dat bij statische weergave levert.</summary>
internal sealed class TestAntiforgery : AntiforgeryStateProvider
{
    public override AntiforgeryRequestToken? GetAntiforgeryToken() => new("test-token", "__RequestVerificationToken");
}

public sealed class HetHoofdmenuWerktOpElkSchermformaat : IDisposable
{
    private static readonly DcNavItem[] Items = [new("Start", "/", MatchAll: true), new("Cursussen", "/cursussen"), new("Mijn profiel", "/profiel")];

    private readonly BunitContext _context = new();

    [Fact]
    public void MenuknopOpSmalleSchermen()
    {
        var cut = _context.Render<DcNavMenu>(p => p.Add(n => n.Items, Items));

        var nav = cut.Find("nav");
        var toggle = cut.Find("nav > button");
        var panel = cut.Find($"#{toggle.GetAttribute("popovertarget")}");

        // Native Popover API: openen/sluiten, Escape en aria-expanded regelt de browser, zonder script (design D9).
        Assert.Equal("Hoofdmenu", nav.GetAttribute("aria-label"));
        Assert.Equal("button", toggle.GetAttribute("type"));
        Assert.Equal("Menu", toggle.TextContent.Trim());
        Assert.True(panel.HasAttribute("popover"));
        Assert.Equal(3, panel.QuerySelectorAll("a").Length);
        Assert.Empty(cut.FindAll("[onclick]"));
    }

    [Fact]
    public void OpBredeSchermenIsHetMenuAltijdZichtbaar()
    {
        var css = File.ReadAllText(Path.Combine(RepoPaths.Ui, "Components", "DcNavMenu.razor.css"));
        var wide = css[css.IndexOf("@media (min-width: 48rem)", StringComparison.Ordinal)..];

        Assert.Matches(@"\.dc-nav__toggle\s*\{\s*display:\s*none;", wide);
        Assert.Matches(@"\.dc-nav__panel\s*\{[^}]*position:\s*static;[^}]*display:\s*block;", wide);
    }

    [Theory]
    [InlineData("/profiel", "Mijn profiel")]
    [InlineData("/cursussen/BIO101", "Cursussen")]
    [InlineData("/", "Start")]
    public void HuidigePaginaInHetMenu(string path, string expected)
    {
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        var cut = _context.Render<DcNavMenu>(p => p.Add(n => n.Items, Items));

        var current = Assert.Single(cut.FindAll("a[aria-current]"));
        Assert.Equal("page", current.GetAttribute("aria-current"));
        Assert.Equal(expected, current.TextContent);
    }

    public void Dispose() => _context.Dispose();
}

public sealed class DeThemakeuzeStaatInDeHeader : IDisposable
{
    private readonly BunitContext _context = new();

    public DeThemakeuzeStaatInDeHeader() => _context.Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgery>();

    [Fact]
    public void ActieveThemakeuze()
    {
        var cut = _context.Render<DcThemeSwitcher>(p => p.Add(t => t.Current, DcTheme.Dark).Add(t => t.ReturnUrl, "/profiel"));

        var buttons = cut.FindAll("button[name=theme]");

        Assert.Equal(["light", "dark", "system"], buttons.Select(b => b.GetAttribute("value")));
        Assert.Equal(["Licht", "Donker", "Systeem"], buttons.Select(b => b.TextContent.Trim()));
        Assert.Equal(["false", "true", "false"], buttons.Select(b => b.GetAttribute("aria-pressed")));
        Assert.Equal("Thema", cut.Find("[role=group]").GetAttribute("aria-label"));
    }

    [Fact]
    public void ThemakeuzeIsEenGewoonFormulier()
    {
        var cut = _context.Render<DcThemeSwitcher>(p => p.Add(t => t.ReturnUrl, "/profiel?tab=2"));

        var form = cut.Find("form");

        Assert.Equal("post", form.GetAttribute("method"));
        Assert.Equal(DcThemes.EndpointPath, form.GetAttribute("action"));
        Assert.Equal("/profiel?tab=2", cut.Find("input[name=returnUrl]").GetAttribute("value"));
        Assert.Equal("test-token", cut.Find("input[name=__RequestVerificationToken]").GetAttribute("value"));
        Assert.All(cut.FindAll("button"), b => Assert.Equal("submit", b.GetAttribute("type")));
        Assert.Equal("true", cut.FindAll("button")[2].GetAttribute("aria-pressed")); // standaard: systeem
    }

    public void Dispose() => _context.Dispose();
}

public sealed class HetAccountgedeelteToontWieErIsIngelogd : IDisposable
{
    private readonly BunitContext _context = new();

    public HetAccountgedeelteToontWieErIsIngelogd() => _context.Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgery>();

    [Fact]
    public void AnoniemeBezoeker()
    {
        var cut = _context.Render<DcAccountMenu>(p => p.Add(a => a.SignInHref, "/inloggen?returnUrl=%2Fcursussen").Add(a => a.SignOutAction, "/uitloggen"));

        var signIn = cut.Find("a");

        Assert.Equal("Inloggen", signIn.TextContent.Trim());
        Assert.Equal("/inloggen?returnUrl=%2Fcursussen", signIn.GetAttribute("href"));
        Assert.Empty(cut.FindAll("[popover]"));
    }

    [Fact]
    public void IngelogdeGebruiker()
    {
        var cut = _context.Render<DcAccountMenu>(p => p
            .Add(a => a.UserName, "Sanne Student")
            .Add(a => a.SignInHref, "/inloggen")
            .Add(a => a.SignOutAction, "/uitloggen"));

        var toggle = cut.Find("button[popovertarget]");
        var menu = cut.Find($"#{toggle.GetAttribute("popovertarget")}");

        Assert.Equal("Sanne Student", cut.Find("[data-testid=account-name]").TextContent);
        Assert.True(menu.HasAttribute("popover"));
        Assert.Equal("/profiel", menu.QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("Mijn profiel", menu.QuerySelector("a")!.TextContent.Trim());
        Assert.Equal("/uitloggen", menu.QuerySelector("form")!.GetAttribute("action"));
        Assert.Equal("Uitloggen", menu.QuerySelector("form button[type=submit]")!.TextContent.Trim());
    }

    public void Dispose() => _context.Dispose();
}

public sealed class ElkePaginaHeeftDezelfdeToegankelijkeOpbouw : IDisposable
{
    private readonly BunitContext _context = new();

    public ElkePaginaHeeftDezelfdeToegankelijkeOpbouw() => _context.Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgery>();

    [Fact]
    public void ShellHeeftSkiplinkLandmarksEnMerk()
    {
        var cut = _context.Render<DcAppShell>(p => p
            .Add(s => s.ReturnUrl, "/")
            .Add(s => s.SignInHref, "/inloggen")
            .Add(s => s.SignOutAction, "/uitloggen")
            .Add(s => s.Navigation, (RenderFragment)(b => b.AddMarkupContent(0, "<nav aria-label=\"Hoofdmenu\"></nav>")))
            .AddChildContent("<h1>Welkom</h1>"));

        var first = cut.Nodes.OfType<AngleSharp.Dom.IElement>().First();

        Assert.Equal("A", first.TagName);
        Assert.Equal("#inhoud", first.GetAttribute("href"));
        Assert.Equal("Naar de inhoud", first.TextContent);
        Assert.NotNull(cut.Find("header nav[aria-label=Hoofdmenu]"));
        Assert.Equal("inhoud", cut.Find("main").Id);
        Assert.Equal("Welkom", cut.Find("main h1").TextContent);
        Assert.Equal("Doctrina", cut.Find("header a[href='/'] .dc-shell__wordmark").TextContent);
    }

    public void Dispose() => _context.Dispose();
}
