using Bunit;
using Doctrina.Core.Results;
using Doctrina.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// Spec ui-components; taken 5.1–5.4 van add-design-system (design D7, D8, D10). Klasse = requirement, methode = scenario.
namespace Doctrina.UI.Tests.Components;

public sealed class TabellenZijnSemantischEnHebbenEenLegeToestand : IDisposable
{
    private static readonly DcTableColumn<(string Code, string Name)>[] Columns = [new("Code", c => c.Code), new("Naam", c => c.Name)];

    private readonly BunitContext _context = new();

    [Fact]
    public void KolomkoppenEnBijschrift()
    {
        var cut = _context.Render<DcTable<(string Code, string Name)>>(p => p
            .Add(t => t.Caption, "Cursussen")
            .Add(t => t.Columns, Columns)
            .Add(t => t.Items, [("BIO101", "Celbiologie"), ("WIS201", "Analyse")])
            .Add(t => t.EmptyText, "Nog geen cursussen."));

        var headers = cut.FindAll("thead th");
        var region = cut.Find("[role=region]");

        Assert.Equal("Cursussen", cut.Find("table > caption").TextContent);
        Assert.Equal(["Code", "Naam"], headers.Select(h => h.TextContent));
        Assert.All(headers, h => Assert.Equal("col", h.GetAttribute("scope")));
        Assert.Equal(2, cut.FindAll("tbody tr").Count);
        Assert.Equal("Celbiologie", cut.FindAll("tbody tr")[0].Children[1].TextContent);
        Assert.Equal(cut.Find("caption").Id, region.GetAttribute("aria-labelledby"));
        Assert.Equal("0", region.GetAttribute("tabindex"));
    }

    [Fact]
    public void LegeTabel()
    {
        var cut = _context.Render<DcTable<(string Code, string Name)>>(p => p
            .Add(t => t.Caption, "Cursussen")
            .Add(t => t.Columns, Columns)
            .Add(t => t.Items, [])
            .Add(t => t.EmptyText, "Nog geen cursussen."));

        Assert.Empty(cut.FindAll("table"));
        Assert.Equal("Nog geen cursussen.", cut.Find("[data-testid=table-empty]").TextContent);
    }

    [Fact]
    public void CelMetEigenWeergave()
    {
        var columns = new DcTableColumn<string>[] { new("Status", Template: s => b => b.AddMarkupContent(0, $"<strong>{s}</strong>")) };

        var cut = _context.Render<DcTable<string>>(p => p
            .Add(t => t.Caption, "Statussen")
            .Add(t => t.Columns, columns)
            .Add(t => t.Items, ["actief"])
            .Add(t => t.EmptyText, "Geen."));

        Assert.Equal("actief", cut.Find("td strong").TextContent);
    }

    [Fact]
    public void TabelZonderBijschriftWordtGeweigerd() =>
        Assert.Throws<InvalidOperationException>(() => _context.Render<DcTable<string>>(p => p
            .Add(t => t.Columns, [])
            .Add(t => t.Items, ["x"])
            .Add(t => t.EmptyText, "Geen.")));

    public void Dispose() => _context.Dispose();
}

public sealed class FormulierveldenZijnGekoppeldAanLabelEnFoutmelding : IDisposable
{
    private readonly BunitContext _context = new();

    [Fact]
    public void LabelHoortBijHetInvoerveld()
    {
        var cut = _context.Render<PersoonFormulier>();

        foreach (var label in cut.FindAll("label"))
        {
            var input = cut.Find($"#{label.GetAttribute("for")}");
            Assert.True(input.TagName is "INPUT" or "SELECT", $"label verwijst naar {input.TagName}");
        }

        var email = cut.FindAll("label").Single(l => l.TextContent.Contains("E-mailadres", StringComparison.Ordinal));
        Assert.Equal("email", cut.Find($"#{email.GetAttribute("for")}").GetAttribute("type"));
    }

    [Fact]
    public void VerplichtVeld()
    {
        var cut = _context.Render<PersoonFormulier>();

        var label = cut.FindAll("label").Single(l => l.TextContent.Contains("E-mailadres", StringComparison.Ordinal));
        var input = cut.Find($"#{label.GetAttribute("for")}");
        var optional = cut.Find($"#{cut.FindAll("label").Single(l => l.TextContent.Contains("Voornaam", StringComparison.Ordinal)).GetAttribute("for")}");

        Assert.True(input.HasAttribute("required"));
        Assert.Equal("true", input.GetAttribute("aria-required"));
        Assert.Contains("(required)", label.TextContent, StringComparison.Ordinal);
        Assert.False(optional.HasAttribute("required"));
    }

    [Fact]
    public void VeldMetEenValidatiefout()
    {
        var cut = _context.Render<PersoonFormulier>(p => p.Add(f => f.Model, new PersoonFormulier.Persoon { Email = "geen-adres" }));

        cut.Find("form").Submit();

        var input = cut.Find("input[type=email]");
        var describedBy = input.GetAttribute("aria-describedby")!.Split(' ');
        var error = cut.Find($"#{describedBy.Last()}");

        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.Contains("is-invalid", input.ClassList);
        Assert.Equal("Ongeldig e-mailadres.", error.TextContent.Trim());
        Assert.Contains(describedBy, id => cut.Find($"#{id}").TextContent == "Je instellingsadres."); // toelichting blijft gekoppeld
        Assert.Equal(0, cut.Instance.Submitted);
    }

    [Fact]
    public void GeldigFormulierWordtVerstuurdZonderFoutmeldingen()
    {
        var cut = _context.Render<PersoonFormulier>(p => p.Add(f => f.Model, new PersoonFormulier.Persoon { Email = "sanne@example.org" }));

        cut.Find("form").Submit();

        Assert.Equal(1, cut.Instance.Submitted);
        Assert.Empty(cut.FindAll("[data-testid=field-error]"));
        Assert.Null(cut.Find("input[type=email]").GetAttribute("aria-invalid"));
    }

    [Fact]
    public void VeldZonderLabelWordtGeweigerd() =>
        Assert.Throws<InvalidOperationException>(() => _context.Render<DcTextField>(p => p.Add(f => f.ValueExpression, () => "x")));

    public void Dispose() => _context.Dispose();
}

public sealed class FoutenUitEenResultWordenEenduidigGetoond : IDisposable
{
    private readonly BunitContext _context = new();

    public FoutenUitEenResultWordenEenduidigGetoond() =>
        _context.Services.AddSingleton<IErrorTitles>(new Titles(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Sample.Item.NotFound"] = "Voorbeelditem niet gevonden",
            ["Sample.Item.Invalid"] = "Controleer de invoer",
        }));

    [Fact]
    public void FoutWordtAlsMeldingGetoond()
    {
        var cut = _context.Render<DcResult>(p => p.Add(r => r.Error, Error.NotFound("Sample.Item.NotFound", "Item bestaat niet.")));

        var alert = cut.Find("[role=alert]");

        Assert.Equal("Voorbeelditem niet gevonden", alert.QuerySelector(".dc-alert__title")!.TextContent);
        Assert.Equal("Sample.Item.NotFound", alert.GetAttribute("data-error-code"));
        Assert.DoesNotContain("Item bestaat niet.", alert.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ZonderVertalingDeMeldingVanDeFout()
    {
        var cut = _context.Render<DcResult>(p => p.Add(r => r.Error, Error.Conflict("Sample.Item.Exists", "Bestaat al.")));

        Assert.Equal("Bestaat al.", cut.Find(".dc-alert__title").TextContent);
    }

    [Fact]
    public void ZonderFoutGeenMelding() =>
        Assert.Empty(_context.Render<DcResult>().FindAll("[role=alert]"));

    [Fact]
    public void ValidatiefoutenVerschijnenBijDeVelden()
    {
        var error = Error.Validation("Sample.Item.Invalid", "Ongeldige invoer.", new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["email"] = ["Ongeldig e-mailadres."],
            ["givenName"] = ["Verplicht."],
            ["onbekend"] = ["Algemene melding."],
        });

        var cut = _context.Render<PersoonFormulier>(p => p.Add(f => f.Error, error));

        string ErrorOf(string label)
        {
            var input = cut.Find($"#{cut.FindAll("label").Single(l => l.TextContent.Contains(label, StringComparison.Ordinal)).GetAttribute("for")}");
            Assert.Equal("true", input.GetAttribute("aria-invalid"));
            return cut.Find($"#{input.GetAttribute("aria-describedby")!.Split(' ').Last()}").TextContent.Trim();
        }

        Assert.Equal("Ongeldig e-mailadres.", ErrorOf("E-mailadres"));
        Assert.Equal("Verplicht.", ErrorOf("Voornaam"));
        var alert = cut.Find("[role=alert]");
        Assert.Equal("Controleer de invoer", alert.QuerySelector(".dc-alert__title")!.TextContent);
        Assert.Equal(["Algemene melding."], alert.QuerySelectorAll("li").Select(li => li.TextContent));
    }

    [Fact]
    public void GewijzigdVeldVerliestDeMeldingVanDeServer()
    {
        var error = Error.Validation("Sample.Item.Invalid", "Ongeldige invoer.", new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["givenName"] = ["Verplicht."],
        });
        var cut = _context.Render<PersoonFormulier>(p => p.Add(f => f.Error, error));
        var input = cut.Find($"#{cut.FindAll("label").Single(l => l.TextContent.Contains("Voornaam", StringComparison.Ordinal)).GetAttribute("for")}");

        input.Change("Sanne");

        Assert.Empty(cut.FindAll("[data-testid=field-error]"));
    }

    public void Dispose() => _context.Dispose();

    private sealed class Titles(Dictionary<string, string> titles) : IErrorTitles
    {
        public string For(string code, string fallback) => titles.GetValueOrDefault(code, fallback);
    }
}

public sealed class DialogenHoudenDeFocusVast : IDisposable
{
    private const string Module = "./_content/Doctrina.UI/js/dc-modal.js";

    private readonly BunitContext _context = new();

    [Fact]
    public async Task DialoogOpenenEnMetEscapeSluiten()
    {
        var module = _context.JSInterop.SetupModule(Module);
        module.SetupVoid("show", _ => true).SetVoidResult();
        module.SetupVoid("close", _ => true).SetVoidResult();
        var cut = _context.Render<DialoogMetKnop>();
        var modal = cut.FindComponent<DcModal>();

        // Naam: de dialoog verwijst met aria-labelledby naar zijn titel.
        var dialog = cut.Find("dialog");
        Assert.Equal("Bevestigen", cut.Find($"#{dialog.GetAttribute("aria-labelledby")}").TextContent);

        // Openen: showModal() via het script, met de dialoog en een terugroep-referentie (focus vasthouden en onthouden).
        cut.Find("[data-testid=open]").Click();
        var show = Assert.Single(module.Invocations["show"]);
        Assert.IsType<ElementReference>(show.Arguments[0]);
        Assert.IsType<DotNetObjectReference<DcModal>>(show.Arguments[1]);
        Assert.True(modal.Instance.IsOpen);

        // Escape: de browser sluit de dialoog en het script meldt "close" (na focusherstel) via OnClosed.
        await cut.InvokeAsync(modal.Instance.OnClosed);
        Assert.False(modal.Instance.IsOpen);
        Assert.Equal(1, cut.Instance.Closed);

        // De sluitknop heeft een naam en sluit via het script.
        var close = cut.Find("dialog .dc-modal__close");
        Assert.Equal("Close", close.TextContent.Trim());
        close.Click();
        Assert.Single(module.Invocations["close"]);
    }

    [Fact]
    public void ScriptZetDeFocusTerugNaSluiten()
    {
        // Het gedrag in de browser zelf wordt handmatig gecontroleerd (taak 8.2); hier: het script doet wat design D10 zegt.
        var script = File.ReadAllText(Path.Combine(RepoPaths.Ui, "wwwroot", "js", "dc-modal.js"));

        Assert.Contains("dialog.showModal()", script, StringComparison.Ordinal);
        Assert.Contains("dcReturnFocus = document.activeElement", script, StringComparison.Ordinal);
        Assert.Contains("addEventListener(\"close\"", script, StringComparison.Ordinal);
        Assert.Contains("target.focus()", script, StringComparison.Ordinal);
        Assert.Contains("invokeMethodAsync(\"OnClosed\")", script, StringComparison.Ordinal);
    }

    [Fact]
    public void DialoogZonderTitelWordtGeweigerd() =>
        Assert.Throws<InvalidOperationException>(() => _context.Render<DcModal>());

    public void Dispose() => _context.Dispose();
}
