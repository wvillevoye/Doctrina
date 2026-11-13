# Vormgeving

Doctrina heeft één design system: design tokens (`--dc-*`), een licht en een donker thema, en de componentenbibliotheek
`Doctrina.UI` met `Dc*`-componenten. Modules geven alleen vorm via die componenten en tokens (huisregel 4). Achtergrond:
`docs/Doctrina-voorstel.md` §7 en design D1–D13 van change `add-design-system`.

## Overzicht

| Wat | Waar |
|---|---|
| Design tokens | `src/Doctrina.UI/Styles/_tokens.scss` |
| Bootstrap 5.3-instellingen en de brug naar de tokens | `src/Doctrina.UI/Styles/_bootstrap-config.scss`, `_bootstrap-bridge.scss` |
| Basis (typografie, links, focus, beweging) en formuliervelden | `src/Doctrina.UI/Styles/_base.scss`, `_forms.scss` |
| Ingang van de stylesheet | `src/Doctrina.UI/Styles/doctrina.scss` → `wwwroot/css/doctrina.css` (gegenereerd) |
| Bootstrap-SCSS, lettertypen, iconen (met licenties) | `Styles/vendor/bootstrap/`, `wwwroot/fonts/`, `Icons/` in `src/Doctrina.UI` |
| Componenten | `src/Doctrina.UI/Components/` (namespace `Doctrina.UI.Components`) |
| Stijlgids | https://localhost:5001/style-guide (`src/Doctrina.Host/Components/Pages/StyleGuide/`) |

De SCSS wordt tijdens `dotnet build` gecompileerd door `AspNetCore.SassCompiler` (`src/Doctrina.UI/sasscompiler.json`);
Node.js is niet nodig. `doctrina.css` staat niet in git. Na een wijziging in een `.scss`-bestand is een build genoeg.

## Tokens

Elke kleur, maat en duur komt uit een token. Gebruik in CSS altijd `var(--dc-…)`, nooit een kleurwaarde.

| Groep | Tokens (selectie) | Gebruik |
|---|---|---|
| Vlakken | `--dc-bg`, `--dc-surface`, `--dc-surface-sunken`, `--dc-neutral-subtle` | achtergrond van de pagina, cards, verzonken vlakken, hover |
| Tekst | `--dc-text`, `--dc-text-muted`, `--dc-heading`, `--dc-link` | gewone tekst (≥ 4,5:1), bijzaken, koppen, links |
| Randen | `--dc-border`, `--dc-border-strong` | decoratieve randen; randen van bedieningselementen (≥ 3:1) |
| Accent en focus | `--dc-accent`, `--dc-accent-contrast`, `--dc-focus` | hoofdacties; tekst op accent; focus-ring |
| Status | `--dc-success`, `--dc-warning`, `--dc-danger`, `--dc-info`, elk met `-contrast` en `-subtle` | meldingen, badges, fouten |
| Palet | `--dc-ink`, `--dc-sage`, `--dc-paper`, `--dc-stone-50` … `--dc-stone-900` | alleen als een semantisch token niet past |
| Typografie | `--dc-font-body` (Inter), `--dc-font-heading` (Fraunces), `--dc-font-size-*`, `--dc-font-weight-*` | |
| Ruimte en vorm | `--dc-space-1` … `--dc-space-7`, `--dc-radius-sm`/`--dc-radius`/`--dc-radius-lg`, `--dc-shadow-float` | |
| Beweging | `--dc-duration` (180 ms), `--dc-duration-fast` (150 ms), `--dc-easing` | worden 0 bij "minder beweging" |

Gebruik semantische tokens (`--dc-text`, `--dc-surface`) en niet het palet: alleen semantische tokens wisselen mee met
het thema. De contrasttests (`tests/Doctrina.UI.Tests/Theming/KleurcontrastVoldoetAanWcag22AaInBeideThemas.cs`)
controleren een vaste lijst tokenparen in beide thema's; voeg daar een paar toe als je een nieuwe combinatie gebruikt.

## Thema's

- **Licht, donker, systeem.** Het thema staat als `data-dc-theme="light|dark|system"` op `<html>`; bij licht of donker
  ook `data-bs-theme`. Zonder keuze volgt Doctrina de systeeminstelling (`prefers-color-scheme`).
- **Keuze van de gebruiker.** De themakeuze in de header (`DcThemeSwitcher`) is een gewoon formulier naar
  `POST /theme`. De keuze staat in de functionele cookie `doctrina.theme` (één jaar); "systeem" verwijdert de cookie.
  Omdat de server het attribuut al in de HTML zet, is de eerste weergave meteen goed, zonder script en zonder flits.
- **Thema op een deel van de pagina.** `data-dc-theme` werkt ook op een container: alles daarbinnen krijgt de tokens
  van dat thema. De stijlgids gebruikt dit om licht en donker naast elkaar te tonen.
- **Minder beweging.** Bij `prefers-reduced-motion: reduce` worden animaties en overgangen uitgeschakeld.

## Componenten

Alle componenten staan in de stijlgids, in hun varianten en in beide thema's. Voeg in een module
`@using Doctrina.UI.Components` toe aan `_Imports.razor`.

| Component | Waarvoor |
|---|---|
| `DcPageHeader` | Kop van elke pagina: de enige `h1`, beschrijving, acties; zet de venstertitel "<titel> · Doctrina". |
| `DcButton` | Actie (`Primary`, `Secondary`, `Ghost`, `Danger`), met `Href` een link in knopvorm, `Icon`, `Busy`. |
| `DcLink` | Tekstlink; `External` opent in een nieuw venster en zegt dat. |
| `DcIcon` | Lucide-icoon (inline SVG). Decoratief, tenzij `Label` is gezet. |
| `DcCard` | Vlak met rand; met `Title` een benoemde sectie, met `Footer` acties. |
| `DcAlert` | Melding (`Info`, `Success`, `Warning`, `Danger`). |
| `DcBadge` | Label voor status of categorie. |
| `DcTable<TItem>` | Tabel met bijschrift, kolommen (`DcTableColumn<TItem>`) en een tekst voor de lege toestand. |
| `DcForm` | Formulier rond `EditForm`, met DataAnnotations; toont een fout uit een `Result` en zet meldingen per veld bij de velden. |
| `DcTextField`, `DcSelect<TValue>`, `DcCheckbox` | Velden met label, verplicht-markering, toelichting (`Hint`) en foutmelding. |
| `DcFieldError` | Foutmelding van één veld (de velden gebruiken hem al; zelf nodig voor een eigen invoerelement). |
| `DcResult` | Verwachte fout uit een `Result`: vertaalde titel bij de foutcode. |
| `DcModal` | Dialoog (native `<dialog>`); openen met `ShowAsync()`. Alleen in een interactieve pagina. |
| `DcAppShell`, `DcNavMenu`, `DcAccountMenu`, `DcThemeSwitcher` | De layout; gebruikt door de host, niet door modules. |

### Voorbeelden

Een overzichtspagina:

```razor
@page "/cursussen"
@attribute [RequirePermission("Academic.Read")]

<DcPageHeader Title="Cursussen">
  <Description>Alle cursussen van je faculteit.</Description>
  <Actions>
    <DcButton Href="/cursussen/nieuw" Variant="DcButtonVariant.Primary" Icon="plus">Nieuwe cursus</DcButton>
  </Actions>
</DcPageHeader>

<DcTable TItem="CourseDto" Caption="Cursussen" HideCaption Items="_courses" Columns="Columns" EmptyText="Nog geen cursussen." />

@code {
    private static readonly DcTableColumn<CourseDto>[] Columns =
    [
        new("Code", c => c.Code),
        new("Naam", c => c.Name),
        new("Status", Template: c => @<DcBadge>@c.Status</DcBadge>),
    ];

    private IReadOnlyList<CourseDto> _courses = [];
}
```

Een formulier dat dezelfde upsert-service gebruikt als de API (huisregel 12) en de fout uit het `Result` toont:

```razor
<DcForm Model="_model" Error="_error" FormName="cursus" OnValidSubmit="SaveAsync">
  <DcTextField Label="Code" Required @bind-Value="_model.Code" />
  <DcTextField Label="Naam" Required @bind-Value="_model.Name" />
  <div class="dc-form__actions">
    <DcButton Type="submit" Variant="DcButtonVariant.Primary" Busy="_saving">Opslaan</DcButton>
  </div>
</DcForm>

@code {
    private async Task SaveAsync()
    {
        var result = await Courses.UpsertAsync(_model, CancellationToken.None);
        _error = result.IsFailure ? result.Error : null;
    }
}
```

`DcForm` vergelijkt de sleutels van `Error.Details` hoofdletterongevoelig met de eigenschappen van het model
(`code` uit de validator hoort bij `Code`); meldingen zonder bijbehorend veld staan in de melding bovenaan.

## Regels voor modules

- Gebruik `Dc*`-componenten. Geen ruwe Bootstrap-classes (`btn`, `card`, `mt-3`, …) en geen letterlijke kleuren
  (`#c2573a`, `rgb(…)`, `hsl(…)`) in `.razor`, `.razor.css` en `.css`.
- Eigen opmaak van een module in een `.razor.css` mag, met alleen `var(--dc-…)` voor kleuren en maten.
- Eén `h1` per pagina (via `DcPageHeader`); koppen daaronder op volgorde.
- Elk invoerveld heeft een zichtbaar label; elke knop een naam.

De architectuurtests (`tests/Doctrina.Architecture.Tests`) controleren de eerste regel voor alle modules en voor de
host, en noemen bestand en regel van een overtreding.

## Een component toevoegen

1. Maak het component in `src/Doctrina.UI/Components/` (`DcNaam.razor`, eventueel `DcNaam.razor.css` met alleen tokens).
   Fouten van ontwikkelaars (geen naam, geen label) geven een `InvalidOperationException`, niet een stille fout.
2. Schrijf bUnit-tests in `tests/Doctrina.UI.Tests/Components/`, genoemd naar requirement en scenario.
3. Voeg een sectie toe aan de stijlgids (`src/Doctrina.Host/Components/Pages/StyleGuide/StyleGuide.razor`) met een
   `StijlgidsSectie` voor het component. De test `DeStijlgidsToontElkComponent` faalt zolang die ontbreekt.
4. Een nieuw icoon: kopieer `<naam>.svg` uit het pakket `lucide-static` naar `src/Doctrina.UI/Icons/` (zie `VERSION`).

## De stijlgids

https://localhost:5001/style-guide toont elk component in het lichte en het donkere thema naast elkaar. De pagina is
beschikbaar in `Development`; daarbuiten alleen met `Ui:StyleGuide:Enabled` op `true` (anders 404). Gebruik haar
ook voor de handmatige controle per release: dialoog (focus, Escape, focus terug), menu's op een smal venster en met
het toetsenbord, themawissel, en "minder beweging".

## White-label: een eigen thema voor een instelling

Een installatie kan de kleuren en het logo aanpassen zonder code te wijzigen. Er is één thema per installatie.

### Instellingen

| Instelling | Betekenis |
|---|---|
| `Theme:StylesheetPath` | Lokaal pad van een stylesheet met alleen `--dc-*`-waarden, bv. `/theme/instelling.css`. Wordt ná `doctrina.css` geladen. |
| `Theme:LogoPath` | Lokaal pad van een logo dat het woordmerk "Doctrina" in de header vervangt, bv. `/theme/logo.svg`. |
| `Theme:LogoAlt` | Alternatieve tekst van het logo (verplicht bij `Theme:LogoPath`), bv. de naam van de instelling. |

```json
{
  "Theme": {
    "StylesheetPath": "/theme/instelling.css",
    "LogoPath": "/theme/logo.svg",
    "LogoAlt": "Universiteit Voorbeeld"
  }
}
```

Paden moeten met één `/` beginnen: bestanden van andere domeinen zijn niet toegestaan (AVG: geen verzoeken naar
derden). Een ongeldig pad, of een logo zonder alternatieve tekst, laat het opstarten mislukken met een melding die de
instelling noemt.

### Waar de bestanden staan

Zet de stylesheet en het logo in `wwwroot/theme/` van `Doctrina.Host`. Bij een container of een gepubliceerde installatie
koppel (mount) je die map vanuit de configuratie van de instelling; de bestanden horen niet in de repository.

### De stylesheet

De stylesheet bevat **alleen** waarden voor bestaande `--dc-*`-tokens (zie `src/Doctrina.UI/Styles/_tokens.scss`), geen
selectors van componenten. Herhaal de selectors van de thema's, zodat licht en donker elk hun eigen waarde houden:

```css
/* wwwroot/theme/instelling.css — alleen --dc-*-waarden */
:root,
[data-dc-theme="light"] {
  --dc-accent: #00594f;
  --dc-link: #00594f;
  --dc-focus: #00594f;
}

[data-dc-theme="dark"] {
  --dc-accent: #5cc8b8;
  --dc-link: #5cc8b8;
  --dc-focus: #5cc8b8;
}

@media (prefers-color-scheme: dark) {
  [data-dc-theme="system"] {
    --dc-accent: #5cc8b8;
    --dc-link: #5cc8b8;
    --dc-focus: #5cc8b8;
  }
}
```

Controleer het contrast van eigen kleuren: tekst minstens 4,5:1 op haar achtergrond, focus en randen van
bedieningselementen minstens 3:1 (WCAG 2.2 AA). `--dc-accent-contrast` is de tekstkleur op een accentvlak (knoppen);
pas die mee aan als de nieuwe accentkleur dat vraagt.
