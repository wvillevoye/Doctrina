# Proposal: add-markdown-toolbar

## Why

Docenten schrijven de introductie en contentpagina's nu in een kaal tekstveld en moeten de Markdown-syntax uit hun hoofd
kennen (`## kop`, `**vet**`). Ook is er geen manier om iets een kleur te geven, terwijl HTML bewust uit staat (veilige
weergave). Een werkbalk met iconen en een veilige, thema-vaste kleurnotatie maakt de editor bruikbaar voor wie geen
Markdown kent, zonder de beveiliging te verzwakken.

## What Changes

- Nieuwe herbruikbare component `MarkdownEditor` in de Content-module: een werkbalk boven het tekstveld met knoppen
  (Lucide-icoon, toegankelijke naam) voor kop 1-3, vet, cursief, opsomming, link, uitlijning (links, midden, rechts) en kleur. Elke knop zet de
  Markdown-syntax om de selectie, of een plaatshouder op de cursor; de focus blijft in het tekstveld.
- De tekstverwerking is een pure C#-functie (testbaar); een klein JS-module doet alleen het lezen en zetten van tekst en
  selectie in de `textarea`.
- `EditIntroductionPage`, `EditContentPage` en het veld "Starting text" op `ManageTemplatesPage` gebruiken de
  `MarkdownEditor`; "Preview" blijft werken zoals nu.
- Veilige kleurnotatie in `Markdown.ToSafeHtml`: `{accent}tekst{/accent}`, ook `success`, `warning`, `danger` en `muted`,
  wordt een `span` met een CSS-klasse op `--dc-*`-tokens. Geen vrije kleurcodes, geen `style`-attribuut, HTML blijft uit.
  Onbekende namen en ongesloten markeringen blijven gewone tekst.
- Kleurkeuze met de UM-kleuren (oranje, donkerblauw, lichtblauw) naast Success/Warning/Danger/Muted; uitlijning via
  `{left}`, `{center}`, `{right}` (een `span` met klasse `md-align--naam`, `display: block`, geen `style`-attribuut).
- Nieuwe tekstkleur-tokens (`--dc-md-*`) in licht en donker, met contrasttests (WCAG AA) op `--dc-surface`.
- Nieuwe Lucide-iconen in `Doctrina.UI/Icons`: `heading-1`, `heading-2`, `heading-3`, `bold`, `italic`, `list`, `link`,
  `palette`, `align-left`, `align-center`, `align-right`.

Buiten de scope:
- Een echte WYSIWYG-/HTML-editor, afbeeldingen uploaden of tabellen/code-knoppen in de werkbalk.
- Sneltoetsen (Ctrl+B enzovoort) en ongedaan maken via de werkbalk buiten het gedrag van de browser.
- Vrije kleuren, achtergrondkleuren of lettergroottes.
- Het externe pakket `UL.MarkdownViewer`: dat is beoordeeld en bewust niet gebruikt.

Afhankelijkheid: bouwt voort op `add-course-introduction` (de pagina's `EditIntroductionPage` en `ManageTemplatesPage`);
bouw deze change pas als die change is afgerond of in dezelfde branch zit.

## Capabilities

### New Capabilities
- `markdown-editor`: de Markdown-editor met werkbalk (opmaakknoppen, kleurkeuze, toegankelijkheid) en de kleurtokens
  waarmee de kleuren worden weergegeven.

### Modified Capabilities
- `course-content`: de eis "Pagina's worden in Markdown geschreven en veilig weergegeven" krijgt de kleurnotatie en de
  regels daarvoor.

## Impact

- `src/Modules/Content`: `MarkdownEditor` (component, scoped CSS, JS-module in `wwwroot`), `MarkdownFormatter`, uitbreiding
  van `Markdown.ToSafeHtml`, aanpassing van drie pagina's.
- `src/Doctrina.UI`: acht nieuwe iconen, tokens in `_tokens.scss`, contrasttest uitgebreid.
- Tests in `tests/Modules/Doctrina.Modules.Content.Tests` en `tests/Doctrina.UI.Tests`; `docs/ontwikkelen/vormgeving.md`
  krijgt een korte alinea (Engels).
- Geen nieuwe packages, geen wijziging van `.Contracts`-projecten of de database.
