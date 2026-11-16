using Bunit;
using Doctrina.UI.Components;
using Doctrina.UI.Icons;
using Microsoft.AspNetCore.Components;

// Spec ui-components, requirement "Knoppen en links hebben altijd een toegankelijke naam"; taak 4.1, design D6/D7.
namespace Doctrina.UI.Tests.Components;

public sealed class KnoppenEnLinksHebbenAltijdEenToegankelijkeNaam : IDisposable
{
    private readonly BunitContext _context = new();

    [Fact]
    public void KnopMetAlleenEenIcoonHeeftEenLabel()
    {
        var cut = _context.Render<DcButton>(p => p.Add(b => b.Icon, "trash-2").Add(b => b.Label, "Verwijderen"));

        var button = cut.Find("button");
        var icon = button.QuerySelector("svg")!;

        // Toegankelijke naam = de (visueel verborgen) tekst; het icoon telt niet mee.
        Assert.Equal("Verwijderen", button.TextContent.Trim());
        Assert.Equal("Verwijderen", button.QuerySelector(".visually-hidden")!.TextContent);
        Assert.Equal("true", icon.GetAttribute("aria-hidden"));
        Assert.Null(icon.GetAttribute("role"));
    }

    [Fact]
    public void KnopZonderNaamWordtGeweigerd()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _context.Render<DcButton>(p => p.Add(b => b.Icon, "trash-2")));

        Assert.Contains("toegankelijke naam", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BezigeKnop()
    {
        var clicks = 0;
        var cut = _context.Render<DcButton>(p => p
            .Add(b => b.Busy, true)
            .Add(b => b.Variant, DcButtonVariant.Primary)
            .Add(b => b.OnClick, () => clicks++)
            .AddChildContent("Opslaan"));

        var button = cut.Find("button");

        Assert.True(button.HasAttribute("disabled"));
        Assert.Equal("true", button.GetAttribute("aria-busy"));
        Assert.Contains("(busy)", button.TextContent, StringComparison.Ordinal);
        button.Click();
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void KnopRoeptOnClickAan()
    {
        var clicks = 0;
        var cut = _context.Render<DcButton>(p => p.Add(b => b.OnClick, () => clicks++).AddChildContent("Opslaan"));

        cut.Find("button").Click();

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void KnopMetTekstToontDieTekstEnHetIcoonIsDecoratief()
    {
        var cut = _context.Render<DcButton>(p => p.Add(b => b.Icon, "plus").AddChildContent("Toevoegen"));

        var button = cut.Find("button");

        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Equal("Toevoegen", button.TextContent.Trim());
        Assert.Empty(button.QuerySelectorAll(".visually-hidden"));
        Assert.Equal("true", button.QuerySelector("svg")!.GetAttribute("aria-hidden"));
        Assert.Contains("btn-secondary", button.ClassList);
    }

    [Theory]
    [InlineData(DcButtonVariant.Primary, "btn-primary")]
    [InlineData(DcButtonVariant.Secondary, "btn-secondary")]
    [InlineData(DcButtonVariant.Ghost, "btn-ghost")]
    [InlineData(DcButtonVariant.Danger, "btn-danger")]
    public void VariantBepaaltDeStijl(DcButtonVariant variant, string expected)
    {
        var cut = _context.Render<DcButton>(p => p.Add(b => b.Variant, variant).AddChildContent("Actie"));

        Assert.Contains(expected, cut.Find("button").ClassList);
    }

    [Fact]
    public void KnopMetHrefIsEenLink()
    {
        var cut = _context.Render<DcButton>(p => p.Add(b => b.Href, "/cursussen/nieuw").AddChildContent("Nieuwe cursus"));

        var link = cut.Find("a");

        Assert.Equal("/cursussen/nieuw", link.GetAttribute("href"));
        Assert.Equal("Nieuwe cursus", link.TextContent.Trim());
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void LinkZonderTekstWordtGeweigerd() =>
        Assert.Throws<InvalidOperationException>(() => _context.Render<DcLink>(p => p.Add(l => l.Href, "/")));

    [Fact]
    public void ExterneLinkZegtDatHijEenNieuwVensterOpent()
    {
        var cut = _context.Render<DcLink>(p => p
            .Add(l => l.Href, "https://www.surf.nl/")
            .Add(l => l.External, true)
            .Add(l => l.ChildContent, (RenderFragment)(b => b.AddContent(0, "SURF"))));

        var link = cut.Find("a");

        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", link.GetAttribute("rel"));
        Assert.Contains("SURF", link.TextContent, StringComparison.Ordinal);
        Assert.Contains("(opens in a new window)", link.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void IcoonIsInlineSvgZonderExterneVerwijzing()
    {
        var cut = _context.Render<DcIcon>(p => p.Add(i => i.Name, "check"));

        var svg = cut.Find("svg");

        Assert.Equal("0 0 24 24", svg.GetAttribute("viewBox"));
        Assert.Equal("currentColor", svg.GetAttribute("stroke"));
        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        Assert.Equal("false", svg.GetAttribute("focusable"));
        Assert.NotEmpty(svg.Children);
        Assert.DoesNotContain("http", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void BetekenisvolIcoonHeeftEenNaam()
    {
        var cut = _context.Render<DcIcon>(p => p.Add(i => i.Name, "triangle-alert").Add(i => i.Label, "Waarschuwing"));

        var svg = cut.Find("svg");

        Assert.Equal("img", svg.GetAttribute("role"));
        Assert.Equal("Waarschuwing", svg.GetAttribute("aria-label"));
        Assert.Null(svg.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void OnbekendIcoonGeeftEenFoutAanDeOntwikkelaar()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _context.Render<DcIcon>(p => p.Add(i => i.Name, "bestaat-niet")));

        Assert.Contains("bestaat-niet", exception.Message, StringComparison.Ordinal);
        Assert.Contains("trash-2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IconenStaanAlsIngeslotenBronInDeBibliotheek() =>
        Assert.Superset(
            new HashSet<string>(["menu", "x", "sun", "moon", "monitor", "user", "log-in", "log-out", "loader-circle"], StringComparer.Ordinal),
            new HashSet<string>(DcIconSet.Names, StringComparer.Ordinal));

    public void Dispose() => _context.Dispose();
}
