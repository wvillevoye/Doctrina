# Design

## Context

Zie `proposal.md`. Observaties in de huidige code:

- `Markdown.ToSafeHtml` (Content, Markdig met `DisableHtml`, tabellen, autolinks) loopt na het parsen over de inlines en
  maakt ongeldige adressen leeg. `MarkdownView` rendert het resultaat als `MarkupString`.
- `EditContentPage` en (uit `add-course-introduction`) `EditIntroductionPage` gebruiken `DcTextField Rows="16"`
  (`InputTextArea`, bindt op het `change`-event) plus een knop "Preview" met `MarkdownView`. `ManageTemplatesPage` heeft
  hetzelfde veld voor de startinhoud.
- Het enige script in `Doctrina.UI` is `dc-modal.js` en `dc-download.js` (ES-modules via `IJSRuntime`). Content heeft geen
  `wwwroot`; het is wel een Razor-bibliotheek en kan dus statische bestanden leveren (`_content/<project>/...`).
- Tokens staan in `Doctrina.UI/Styles/_tokens.scss`. De contrasttest
  `KleurcontrastVoldoetAanWcag22AaInBeideThemas` leest `_tokens.scss`.
- Iconen: Lucide-bestanden in `Doctrina.UI/Icons` (zie `VERSION`); de set heeft nu geen kop-, vet- of lijsticonen.

## Goals / Non-Goals

**Goals:**
- Een werkbalk die de Markdown-syntax invoegt en veilig blijft: geen HTML, geen vrije kleuren.
- Alle opmaaklogica testbaar in .NET.

**Non-Goals:**
- Geen WYSIWYG, sneltoetsen, afbeeldingen, tabellen of eigen kleuren (zie proposal).

## Decisions

### D1: Opmaaklogica in C#, JS alleen voor tekst en selectie

`MarkdownFormatter.Apply(MarkdownAction, EditorState) -> EditorState` is een pure functie op
`(Text, SelectionStart, SelectionEnd)`. Het JS-module `md-editor.js` heeft twee functies: `getState(container)` leest
waarde en selectie uit de `textarea`; `setState(container, state)` zet waarde en selectie, geeft de focus terug aan de
`textarea` en vuurt `input` en `change` af, zodat `InputTextArea` de nieuwe waarde bindt. De component haalt de staat
**uit de browser** op voordat hij rekent: de server kent tekst die getypt is maar nog geen `change` heeft gehad niet
(scenario "Tekst die al getypt was gaat niet verloren"). Alternatief: de logica in JS: afgewezen, niet te testen met de
bestaande testset (xUnit/bUnit) en dubbele regels naast de server. Kosten: één rondreis per klik; verwaarloosbaar.

### D2: Kleurnotatie als AST-nabewerking in `ToSafeHtml`

Na het parsen loopt een extra stap per container-inline over de `LiteralInline`s: de markeringen `{naam}` en `{/naam}`
(alleen de vijf vaste namen) worden uit de literals gesneden en alles ertussen, in dezelfde container, wordt omgeven door
een `ColorInline` (een `ContainerInline`) met een eigen renderer die `<span class="md-color md-color--naam">` schrijft.
Daardoor werkt het binnen vet of een koppen, blijven codeblokken en code-inlines onaangeroerd (dat zijn geen literals) en
blijven onbekende of ongesloten markeringen gewone tekst. Een markering die over een grens van een container heen
reikt (bijvoorbeeld open binnen `**` en gesloten buiten) blijft tekst; dat is bewust en wordt in de documentatie vermeld.
Alternatief: tekstvervanging op de HTML-uitvoer: afgewezen, kan niet-gebalanceerde `span`s maken en raakt codeblokken.
Alternatief: een Markdig-`InlineParser` op `{`: afgewezen voor nu, de nabewerking past in de bestaande lus en is eenvoudiger
te controleren. De uitvoer bevat nooit een `style`-attribuut en alleen deze vijf klassennamen.

### D3: Tekstkleuren als eigen tokens

Nieuwe tokens `--dc-md-accent`, `--dc-md-success`, `--dc-md-warning`, `--dc-md-danger`, `--dc-md-muted` in licht en donker,
verwijzen naar de bestaande semantische tokens (`var(--dc-accent)`, `--dc-success`, `--dc-warning`, `--dc-danger`,
`--dc-text-muted`), die al in beide thema's als tekstkleur zijn gekozen; een white-label thema kan ze los overschrijven.
Gemeten bleken ze al 4,5:1 op `--dc-surface` te halen, dus geen nieuwe kleurwaarden. (Eerdere aanname dat
`--dc-warning` te zwak zou zijn als tekst klopte niet: de waarde is al voor tekst gekozen.) De klassen `.md-color--*` staan in een scoped stylesheet van `MarkdownView`
(`::deep`) en gebruiken alleen deze tokens (architectuurtest `LiteralColors` geldt). De contrasttest krijgt vijf paren
per thema.

### D3b: UM-kleuren en uitlijning

`accent` is al UM-oranje. Nieuw: `blue` (`--dc-md-blue` = `var(--dc-heading)`: UM-donkerblauw in licht, de lichte tekstkleur
in donker, anders onleesbaar op de donkere achtergrond) en `lightblue` (`--dc-md-lightblue` = `var(--dc-link)`). Het
palet toont "UM orange", "UM dark blue" en "UM light blue" voor `accent`, `blue`, `lightblue`. Uitlijning gebruikt dezelfde
nabewerking als kleur (D2) met een tweede klassenvoorvoegsel: `<span class="md-align md-align--center">` met
`display: block; text-align: ...` in `MarkdownView`. Beperking: een markering pakt één alinea (net als kleur); een
selectie over een lege regel heen blijft tekst.

### D4: De werkbalk

`MarkdownEditor` (`Value`/`ValueChanged`, `Label`, `Rows`, `Hint`) rendert een `div role="toolbar" aria-label="Formatting"`
met `DcButton`s (`Variant=Ghost`, `Icon`, `Label` voor de toegankelijke naam; type `button`) boven een `DcTextField`. De
knop "Color" opent met het native popover-attribuut (`popover`/`popovertarget`, zoals de zijbalk, werkt zonder script) een
lijst van vijf knoppen; de keuze sluit de popover. `DcButton` krijgt geen nieuwe parameters. Elke knop is een gewone
tab-stop (geen roving tabindex): acht knoppen is nog te overzien en dit houdt de component klein.

Gedrag van `MarkdownFormatter`:
- Kop 1-3: werkt op de regel(s) van de selectie; een bestaand `#`-voorvoegsel wordt vervangen, hetzelfde niveau opnieuw
  verwijdert het (wisselknop).
- Vet/cursief/kleur: omgeeft de selectie; zonder selectie een plaatshouder (`text`) die geselecteerd wordt.
- Opsomming: `- ` voor elke geselecteerde regel; staat er al overal een voorvoegsel, dan wordt het verwijderd.
- Link: `[selectie](https://)` met `https://` geselecteerd; zonder selectie `[text](https://)` met `text` geselecteerd.

### D5: Welke pagina's

`EditContentPage`, `EditIntroductionPage` en het veld "Starting text" van `ManageTemplatesPage` vervangen `DcTextField Rows`
door `MarkdownEditor`. Validatiefouten per veld blijven werken omdat `MarkdownEditor` het `ValueExpression` doorgeeft aan
`DcTextField`. De iconen (`heading-1..3`, `bold`, `italic`, `list`, `link`, `palette`) worden toegevoegd aan
`Doctrina.UI/Icons` volgens `VERSION`.

### D6: Verschuifbaar voorbeeldvenster

`PreviewWindow` (fixed, `resize: both`, geen modal: het formulier blijft bruikbaar) vervangt de vaste kaart onder het
formulier. `md-editor.js` krijgt `makeDraggable(venster, greep)` met pointer-events (muis en aanraking, `touch-action: none`)
en pijltjestoetsen op de focusbare greep; het venster blijft binnen het beeld. De positie wordt niet onthouden.

## Risks / Trade-offs

- JS-interop werkt alleen in een interactieve pagina → alle drie de pagina's hebben al `InteractiveServer`; zonder JS
  blijft het tekstveld bruikbaar, alleen doen de knoppen niets (de werkbalk is een extra, geen vereiste).
- Kleur binnen een container-grens blijft tekst → vermeld in de documentatie; geen foutmelding.
- Een nieuwe afhankelijkheid van de volgorde: de pagina's bestaan pas na `add-course-introduction`.
