# Voorstel

## Why

Doctrina heeft nu een werkend fundament, maar een bewust kale UI: semantische HTML zonder vormgeving. Elke volgende
module (te beginnen met `add-academic-core`) bouwt schermen; zonder gedeelde componenten en tokens gaat elke module zelf
Bootstrap-classes en kleuren gebruiken, wat huisregel 4 schendt en later niet meer terug te draaien is. Daarom eerst
een eigen design system: één bron van waarheid voor de vormgeving, toegankelijke `Dc*`-componenten en een echte
app-shell. Roadmap: `docs/Doctrina-voorstel.md` §8, rij 2 (afhankelijk van rij 1, `add-modular-foundation`).

## What Changes

- **Design tokens** (`--dc-*`) als enige bron voor kleur, typografie, spacing, radius, schaduw en motion, volgens de
  richting "Doctrina" uit voorstel §7.0 (inktblauw, terracotta-accent, saliegroen, papierwit, warme grijzen; serif-koppen
  met een strakke sans voor tekst). Bootstrap 5.3 wordt vanuit SCSS opgebouwd met variabelen die van de tokens komen,
  zodat het er niet als Bootstrap uitziet.
- **Licht en donker thema:** standaard volgens de systeeminstelling; een keuze licht/donker/systeem in de header wordt
  onthouden in een cookie, zodat de eerste weergave al het juiste thema heeft.
- **White-label (eenvoudig):** een instelling kan via configuratie een eigen stylesheet met alleen token-waarden en een
  eigen logo laden, zonder code te wijzigen.
- **Componentenbibliotheek `Doctrina.UI`** met de eerste set `Dc*`-componenten (knoppen, links, iconen, cards,
  paginakop, meldingen, badges, tabellen, dialoog, formuliervelden, weergave van een `Result`-fout), toegankelijk volgens
  WCAG 2.2 AA en gestyled met alleen tokens.
- **App-shell:** de host-layout wordt een echte Doctrina-layout (woordmerk of logo, hoofdmenu, accountmenu, themakeuze,
  inklapbaar menu op smalle schermen); de bestaande hostpagina's (start, profiel, geen toegang, fout) gebruiken de
  componenten.
- **Stijlgids** in de app die elk component in licht en donker toont.
- **Bewaking:** de architectuurtests weigeren naast ruwe Bootstrap-classes ook hardcoded kleuren in modules.
- **Build zonder Node.js:** SCSS wordt tijdens `dotnet build` gecompileerd (AspNetCore.SassCompiler, MIT); Bootstrap-SCSS,
  lettertypen en iconen worden in de repo meegeleverd en zelf gehost (AVG: geen verzoeken naar externe domeinen).

## Out of scope

- Domeinschermen (cursussen, personen, groepen) → `add-academic-core` en later.
- Een toast- of notificatieservice, grafieken, datum-/tijdkiezers, rich-text-editor, bestandsupload-component.
- PurgeCSS (vereist Node.js); in plaats daarvan worden alleen de gebruikte Bootstrap-onderdelen geïmporteerd.
- Thema per gebruiker, per faculteit of per tenant tijdens runtime; white-label is één thema per installatie.
- Een uitgewerkt logo of symbool; alleen het woordmerk "Doctrina".
- Rechts-naar-links-talen, e-mailsjablonen, printopmaak.
- Geautomatiseerde visuele regressietests of browsertests (axe/Playwright); toegankelijkheid wordt getest op structuur
  (bUnit) en op contrastwaarden van de tokens.

## Capabilities

### New Capabilities
- `theming`: design tokens, licht/donker thema met keuze van de gebruiker, white-label-thema, zelf gehoste bronnen en
  beperkte beweging.
- `ui-components`: de `Dc*`-componentenbibliotheek, haar toegankelijkheidsgedrag, de stijlgids en de regel dat modules
  alleen via `Doctrina.UI` vormgeven.
- `app-shell`: de layout van de host (merk, hoofdmenu, accountmenu, themakeuze, gedrag op smalle schermen) en de
  hostpagina's.

### Modified Capabilities
<!-- geen: welke menu-items zichtbaar zijn, blijft in module-system; app-shell gaat alleen over de weergave -->

## Impact

- `src/Doctrina.UI`: tokens, SCSS, componenten, lettertypen, iconen; `src/Doctrina.Host`: layout en pagina's omgezet,
  themakeuze-endpoint, white-label-instellingen.
- Nieuwe afhankelijkheid: `AspNetCore.SassCompiler` (MIT). In de repo meegeleverd: Bootstrap 5.3-SCSS (MIT), lettertypen
  Fraunces en Inter (SIL OFL 1.1), Lucide-iconen (ISC); licentiebestanden gaan mee.
- Nieuw testproject `tests/Doctrina.UI.Tests` (bUnit); uitbreiding van `Doctrina.Architecture.Tests`.
- Geen wijziging aan API, database of authenticatie. Bestaande tests die op de kale HTML controleren, worden bijgewerkt.
- Geen nieuwe vereisten voor ontwikkelaars: alleen de .NET SDK (geen Node.js).
