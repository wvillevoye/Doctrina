using Bunit;
using Doctrina.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

// Taak 4.2 van add-design-system: opbouw, rollen en titel van DcCard, DcPageHeader, DcAlert en DcBadge (design D7).
namespace Doctrina.UI.Tests.Components;

public sealed class DcPageHeaderTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Fact]
    public void KopBeschrijvingEnActies()
    {
        var cut = _context.Render<DcPageHeader>(p => p
            .Add(h => h.Title, "Cursussen")
            .Add(h => h.Description, (RenderFragment)(b => b.AddContent(0, "Alle cursussen van de faculteit.")))
            .Add(h => h.Actions, (RenderFragment)(b => b.AddContent(0, "actie"))));

        Assert.Equal("Cursussen", cut.Find("h1").TextContent);
        Assert.Single(cut.FindAll("h1"));
        Assert.Equal("Alle cursussen van de faculteit.", cut.Find(".dc-page-header__description").TextContent);
        Assert.Equal("actie", cut.Find(".dc-page-header__actions").TextContent);
    }

    [Theory]
    [InlineData(null, "Mijn profiel · Doctrina")]
    [InlineData("Start", "Start · Doctrina")]
    public void ZetDeTitelVanHetBrowservenster(string? documentTitle, string expected)
    {
        var cut = _context.Render<DcPageHeader>(p => p.Add(h => h.Title, "Mijn profiel").Add(h => h.DocumentTitle, documentTitle));

        var title = cut.FindComponent<PageTitle>().Instance.ChildContent!;

        Assert.Equal(expected, _context.Render(title).Markup);
    }

    public void Dispose() => _context.Dispose();
}

public sealed class DcCardTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Fact]
    public void CardMetTitelIsEenBenoemdeSectie()
    {
        var cut = _context.Render<DcCard>(p => p.Add(c => c.Title, "Gegevens").AddChildContent("<p>Inhoud</p>"));

        var section = cut.Find("section");
        var heading = cut.Find("h2");

        Assert.Equal(heading.Id, section.GetAttribute("aria-labelledby"));
        Assert.Equal("Gegevens", heading.TextContent);
        Assert.Equal("Inhoud", cut.Find("p").TextContent);
    }

    [Fact]
    public void KopniveauIsInTeStellen()
    {
        var cut = _context.Render<DcCard>(p => p.Add(c => c.Title, "Detail").Add(c => c.HeadingLevel, 3));

        Assert.Equal("Detail", cut.Find("h3").TextContent);
        Assert.Empty(cut.FindAll("h2"));
    }

    [Fact]
    public void CardZonderTitelIsGeenSectie()
    {
        var cut = _context.Render<DcCard>(p => p.AddChildContent("Inhoud").Add(c => c.Footer, (RenderFragment)(b => b.AddContent(0, "Voet"))));

        Assert.Empty(cut.FindAll("section"));
        Assert.Equal("Voet", cut.Find(".dc-card__footer").TextContent);
    }

    [Fact]
    public void TweeCardsHebbenElkEenEigenId()
    {
        var first = _context.Render<DcCard>(p => p.Add(c => c.Title, "A"));
        var second = _context.Render<DcCard>(p => p.Add(c => c.Title, "B"));

        Assert.NotEqual(first.Find("h2").Id, second.Find("h2").Id);
    }

    public void Dispose() => _context.Dispose();
}

public sealed class DcAlertTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Theory]
    [InlineData(DcSeverity.Info, "status", "Information")]
    [InlineData(DcSeverity.Success, "status", "Success")]
    [InlineData(DcSeverity.Warning, "alert", "Warning")]
    [InlineData(DcSeverity.Danger, "alert", "Error")]
    public void RolEnErnstPerSoort(DcSeverity severity, string role, string severityText)
    {
        var cut = _context.Render<DcAlert>(p => p.Add(a => a.Severity, severity).Add(a => a.Title, "Titel").AddChildContent("Tekst"));

        var alert = cut.Find("div.dc-alert");

        Assert.Equal(role, alert.GetAttribute("role"));
        Assert.Contains($"alert-{severity.ToString().ToLowerInvariant()}", alert.ClassList);
        Assert.Equal($"{severityText}: ", cut.Find(".visually-hidden").TextContent);
        Assert.Equal("Titel", cut.Find(".dc-alert__title").TextContent);
        Assert.Equal("true", cut.Find("svg").GetAttribute("aria-hidden"));
    }

    public void Dispose() => _context.Dispose();
}

public sealed class DcBadgeTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Theory]
    [InlineData(DcBadgeVariant.Neutral, "dc-badge--neutral")]
    [InlineData(DcBadgeVariant.Success, "dc-badge--success")]
    [InlineData(DcBadgeVariant.Danger, "dc-badge--danger")]
    public void BadgeToontTekstMetVariant(DcBadgeVariant variant, string expected)
    {
        var cut = _context.Render<DcBadge>(p => p.Add(b => b.Variant, variant).AddChildContent("Actief"));

        var badge = cut.Find("span.dc-badge");

        Assert.Equal("Actief", badge.TextContent);
        Assert.Contains(expected, badge.ClassList);
    }

    public void Dispose() => _context.Dispose();
}
