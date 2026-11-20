using Bunit;
using Doctrina.UI.Components;
using Doctrina.UI.Theming;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

// Spec app-shell; task 6.1 of add-design-system (design D3, D9). Class = requirement, method = scenario.
namespace Doctrina.UI.Tests.Components;

/// <summary>Antiforgery token as the host supplies it in static rendering.</summary>
internal sealed class TestAntiforgery : AntiforgeryStateProvider
{
    public override AntiforgeryRequestToken? GetAntiforgeryToken() => new("test-token", "__RequestVerificationToken");
}

public sealed class HetHoofdmenuWerktOpElkSchermformaat : IDisposable
{
    private static readonly DcNavItem[] Items = [new("Start", "/", MatchAll: true), new("Cursussen", "/cursussen"), new("Mijn profiel", "/profile")];

    private readonly BunitContext _context = new();

    [Fact]
    public void MenuknopOpSmalleSchermen()
    {
        var cut = RenderShell(_context);

        var toggle = cut.Find("header button[popovertarget]");
        var panel = cut.Find($"#{toggle.GetAttribute("popovertarget")}");

        // Native Popover API: opening/closing, Escape and aria-expanded are handled by the browser, without script (design D2).
        Assert.Equal("button", toggle.GetAttribute("type"));
        Assert.Equal("Menu", toggle.TextContent.Trim());
        Assert.True(panel.HasAttribute("popover"));
        Assert.Equal("ASIDE", panel.TagName);
        Assert.Equal("Main menu", panel.QuerySelector("nav")!.GetAttribute("aria-label"));
        Assert.Equal(3, panel.QuerySelectorAll("nav a").Length);
        Assert.Empty(cut.FindAll("[onclick]"));
    }

    [Fact]
    public void ZijbalkOpBredeSchermen()
    {
        var css = File.ReadAllText(Path.Combine(RepoPaths.Ui, "Components", "DcAppShell.razor.css"));
        var wide = css[css.IndexOf("@media (min-width: 64rem)", StringComparison.Ordinal)..];

        Assert.Matches(@"\.dc-shell__toggle\s*\{\s*display:\s*none;", wide);
        Assert.Matches(@"\.dc-shell__sidebar\s*\{[^}]*position:\s*static;[^}]*display:\s*block;", wide);
        Assert.Matches(@"\.dc-shell__sidebar-inner\s*\{[^}]*position:\s*sticky;", wide);
    }

    internal static IRenderedComponent<DcAppShell> RenderShell(BunitContext context)
    {
        context.Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgery>();
        return context.Render<DcAppShell>(p => p
            .Add(x => x.ReturnUrl, "/")
            .Add(x => x.SignInHref, "/sign-in")
            .Add(x => x.SignOutAction, "/sign-out")
            .Add(x => x.Navigation, (RenderFragment)(b =>
            {
                b.OpenComponent<DcNavMenu>(0);
                b.AddComponentParameter(1, nameof(DcNavMenu.Items), Items);
                b.CloseComponent();
            })));
    }

    [Theory]
    [InlineData("/profile", "Mijn profiel")]
    [InlineData("/cursussen/BIO101", "Cursussen")]
    [InlineData("/", "Start")]
    public void HuidigePaginaInHetMenu(string path, string expected)
    {
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        var cut = _context.Render<DcNavMenu>(p => p.Add(n => n.Items, Items));

        var current = Assert.Single(cut.FindAll("a[aria-current]"));
        Assert.Equal("page", current.GetAttribute("aria-current"));
        Assert.Equal(expected, current.TextContent.Trim());
    }

    [Fact]
    public void HuidigePaginaIsNietAlleenMetKleurGemarkeerd()
    {
        var css = File.ReadAllText(Path.Combine(RepoPaths.Ui, "Components", "DcNavMenu.razor.css"));
        var current = css[css.IndexOf("[aria-current=\"page\"]", StringComparison.Ordinal)..];
        current = current[..current.IndexOf('}', StringComparison.Ordinal)];

        // WCAG 1.4.1: besides colour a bar (box-shadow) and a bolder weight.
        Assert.Contains("box-shadow", current, StringComparison.Ordinal);
        Assert.Contains("font-weight", current, StringComparison.Ordinal);
    }

    public void Dispose() => _context.Dispose();
}

public sealed class MenuItemsKunnenEenIcoonHebben : IDisposable
{
    private readonly BunitContext _context = new();

    [Fact]
    public void ItemMetIcoon()
    {
        var cut = _context.Render<DcNavMenu>(p => p.Add(n => n.Items, [new DcNavItem("Home", "/", Icon: "house")]));

        var link = cut.Find("a");

        Assert.Equal("Home", link.QuerySelector("span")!.TextContent);
        Assert.Equal("true", link.QuerySelector("svg")!.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void ItemZonderIcoon()
    {
        var cut = _context.Render<DcNavMenu>(p => p.Add(n => n.Items, [new DcNavItem("Home", "/")]));

        Assert.Empty(cut.FindAll("svg"));
        Assert.Equal("Home", cut.Find("a").TextContent.Trim());
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
        var cut = _context.Render<DcThemeSwitcher>(p => p.Add(t => t.Current, DcTheme.Dark).Add(t => t.ReturnUrl, "/profile"));

        var buttons = cut.FindAll("button[name=theme]");

        Assert.Equal(["light", "dark", "system"], buttons.Select(b => b.GetAttribute("value")));
        Assert.Equal(["Light", "Dark", "System"], buttons.Select(b => b.TextContent.Trim()));
        Assert.Equal(["false", "true", "false"], buttons.Select(b => b.GetAttribute("aria-pressed")));
        Assert.Equal("Theme", cut.Find("[role=group]").GetAttribute("aria-label"));
    }

    [Fact]
    public void ThemakeuzeIsEenGewoonFormulier()
    {
        var cut = _context.Render<DcThemeSwitcher>(p => p.Add(t => t.ReturnUrl, "/profile?tab=2"));

        var form = cut.Find("form");

        Assert.Equal("post", form.GetAttribute("method"));
        Assert.Equal(DcThemes.EndpointPath, form.GetAttribute("action"));
        Assert.Equal("/profile?tab=2", cut.Find("input[name=returnUrl]").GetAttribute("value"));
        Assert.Equal("test-token", cut.Find("input[name=__RequestVerificationToken]").GetAttribute("value"));
        Assert.All(cut.FindAll("button"), b => Assert.Equal("submit", b.GetAttribute("type")));
        Assert.Equal("true", cut.FindAll("button")[2].GetAttribute("aria-pressed")); // default: system
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
        var cut = _context.Render<DcAccountMenu>(p => p.Add(a => a.SignInHref, "/sign-in?returnUrl=%2Fcursussen").Add(a => a.SignOutAction, "/sign-out"));

        var signIn = cut.Find("a");

        Assert.Equal("Sign in", signIn.TextContent.Trim());
        Assert.Equal("/sign-in?returnUrl=%2Fcursussen", signIn.GetAttribute("href"));
        Assert.Empty(cut.FindAll("[popover]"));
    }

    [Fact]
    public void IngelogdeGebruiker()
    {
        var cut = _context.Render<DcAccountMenu>(p => p
            .Add(a => a.UserName, "Sanne Student")
            .Add(a => a.SignInHref, "/sign-in")
            .Add(a => a.SignOutAction, "/sign-out"));

        var toggle = cut.Find("button[popovertarget]");
        var menu = cut.Find($"#{toggle.GetAttribute("popovertarget")}");

        Assert.Equal("Sanne Student", cut.Find("[data-testid=account-name]").TextContent);
        Assert.True(menu.HasAttribute("popover"));
        Assert.Equal("/profile", menu.QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("My profile", menu.QuerySelector("a")!.TextContent.Trim());
        Assert.Equal("/sign-out", menu.QuerySelector("form")!.GetAttribute("action"));
        Assert.Equal("Sign out", menu.QuerySelector("form button[type=submit]")!.TextContent.Trim());
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
            .Add(s => s.SignInHref, "/sign-in")
            .Add(s => s.SignOutAction, "/sign-out")
            .Add(s => s.Navigation, (RenderFragment)(b => b.AddMarkupContent(0, "<nav aria-label=\"Main menu\"></nav>")))
            .AddChildContent("<h1>Welkom</h1>"));

        var first = cut.Nodes.OfType<AngleSharp.Dom.IElement>().First();

        Assert.Equal("A", first.TagName);
        Assert.Equal("#content", first.GetAttribute("href"));
        Assert.Equal("Skip to content", first.TextContent);
        Assert.NotNull(cut.Find("aside nav[aria-label='Main menu']"));
        Assert.Equal("content", cut.Find("main").Id);
        Assert.Equal("Welkom", cut.Find("main h1").TextContent);
        Assert.Equal("Doctrina", cut.Find("header a[href='/'] .dc-shell__wordmark").TextContent);
    }

    [Fact]
    public void HetHoofdmenuStaatInDeZijbalk()
    {
        var cut = HetHoofdmenuWerktOpElkSchermformaat.RenderShell(_context);

        Assert.NotNull(cut.Find("aside nav[aria-label='Main menu']"));
        Assert.Empty(cut.FindAll("header nav"));
    }

    public void Dispose() => _context.Dispose();
}
