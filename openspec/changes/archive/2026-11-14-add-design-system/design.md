# Ontwerp

## Context

Na `add-modular-foundation` heeft de host een kale layout (`MainLayout`, `NavMenu`, pagina's start/profiel/geen
toegang/fout) met een tijdelijke `wwwroot/app.css` die letterlijke kleuren bevat. `Doctrina.UI` is een lege Razor Class
Library. De architectuurtests weigeren al ruwe Bootstrap-classes in modules (`RawBootstrap`), nog niet letterlijke
kleuren. Ontwikkelaars hebben alleen de .NET SDK, LocalDB en Docker nodig; dat blijft zo (keuze producteigenaar
2026-11-12: geen Node.js). Motivatie en reikwijdte: `proposal.md`; gedrag: de specs `theming`, `ui-components` en
`app-shell`. Visuele richting: `docs/Doctrina-voorstel.md` §7.

## Doelen / Geen doelen

**Doelen:**
- Eén tokenbestand als bron; Bootstrap volgt de tokens tijdens runtime, zodat een white-label-stylesheet met alleen
  `--dc-*`-waarden genoeg is.
- Alles werkt bij statische server-rendering (SSR) zonder scripts: themakeuze, menu op smalle schermen, accountmenu.
  Scripts alleen waar de browser het gedrag niet zelf biedt (dialoog openen/sluiten).
- `Doctrina.UI` hangt alleen af van `Doctrina.Core` en ASP.NET Core, nooit van `Doctrina.Infrastructure` of de host.

**Geen doelen:**
- Bootstrap-JavaScript (Popper, collapse, modal); componenten gebruiken native HTML (`<dialog>`, Popover API).
- Een tokenpijplijn of generator (Style Dictionary e.d.); de tokens zijn gewone SCSS/CSS.

## Besluiten

### D1 — SCSS-build met AspNetCore.SassCompiler, Bootstrap-SCSS in de repo
`AspNetCore.SassCompiler` (MIT; levert Dart Sass mee voor Windows, Linux en macOS) compileert `Styles/doctrina.scss` in
`Doctrina.UI` tijdens `dotnet build` naar `wwwroot/css/doctrina.css` (statische asset van de RCL:
`_content/Doctrina.UI/css/doctrina.css`). De gegenereerde CSS staat niet in git. Bootstrap 5.3.x (laatste patch bij het
bouwen) wordt als bronbestanden opgenomen in `Styles/vendor/bootstrap/` met `LICENSE`; alleen de benodigde onderdelen
worden geïmporteerd (functions, variables, maps, mixins, utilities, root, reboot, type, containers, grid, buttons,
forms, tables, card, alert, badge, close, helpers, utilities/api).
*Afwijking van voorstel §7.4:* geen PurgeCSS, omdat dat Node.js vereist; de selectieve import houdt de CSS klein.
*Alternatieven:* npm-stap met PurgeCSS (afgewezen: Node.js als extra vereiste); NuGet-pakket `bootstrap` (afgewezen:
levert de SCSS-bronnen niet betrouwbaar mee).

### D2 — Tokens als CSS-variabelen; Bootstrap volgt ze tijdens runtime
`Styles/_tokens.scss` definieert alle `--dc-*`-tokens: palet (`ink`, `accent`, `sage`, `paper`, `stone-50…900`),
semantisch (`text`, `text-muted`, `surface`, `border`, `focus`, `success`, `warning`, `danger`, met `-contrast` voor
tekst op die kleur), typografie (families, schaal), spacing, radius (`0.75rem` cards, `0.5rem` knoppen), schaduw
(alleen zwevende lagen) en motion (150–200 ms). Elke tokenregel staat als `--dc-naam: waarde;` op één regel, zodat tests
ze kunnen lezen.

Bootstrap rekent kleuren tijdens het compileren uit (Sass-functies), dus Sass-variabelen alleen kunnen niet
white-labelen. Daarom: Sass-variabelen krijgen de standaardwaarden (voor de compile-time-wiskunde), en een eigen laag
`_bootstrap-bridge.scss` zet Bootstrap's runtime-variabelen (`--bs-body-bg`, `--bs-btn-bg`, `--bs-border-color`, …) op
`var(--dc-…)`. Afgeleide toestanden (hover, active) gebruiken `color-mix(in srgb, var(--dc-accent) 85%, black)`, zodat
ze ook meebewegen met een white-label-accent. Componentstijlen (`.razor.css`) gebruiken uitsluitend `var(--dc-…)`.

### D3 — Thema: attribuut op `<html>`, cookie, geen script nodig
Het thema heeft drie standen: `light`, `dark`, `system`. De keuze staat in de cookie `doctrina.theme` (1 jaar,
`SameSite=Lax`, `Secure`, `HttpOnly`; functioneel, geen toestemming nodig). `App.razor` zet bij elke weergave
`data-dc-theme="light|dark|system"` op `<html>`, en bij `light`/`dark` ook `data-bs-theme` voor Bootstrap's eigen
onderdelen. De donkere tokens gelden voor `[data-dc-theme=dark]` en, via
`@media (prefers-color-scheme: dark)`, voor `[data-dc-theme=system]`; `color-scheme` volgt mee voor native
formulierelementen. Zo is de eerste weergave altijd goed, zonder script en zonder flits.
Kiezen gebeurt met een formulier (`POST /thema`, antiforgery, `theme` + lokale `returnUrl`) dat de cookie zet en terugstuurt
naar dezelfde pagina; dat werkt ook zonder scripts en in interactieve pagina's (volledige herlaadbeurt is acceptabel
voor deze zeldzame actie). Het thema staat ook op geneste elementen (`data-dc-theme` op een container), zodat de
stijlgids licht en donker naast elkaar kan tonen.

### D4 — White-label via configuratie
`Theme:StylesheetPath` (lokaal pad, bv. `/theme/um.css`, bestand in de `wwwroot/theme` van de host of daar gemount),
`Theme:LogoPath` en `Theme:LogoAlt`. De stylesheet wordt ná `doctrina.css` geladen en hoort alleen `--dc-*`-waarden te
bevatten. Paden moeten met `/` beginnen en mogen niet met `//` beginnen (geen externe domeinen, theming-spec "zelf
gehost"); een ongeldig pad of een logo zonder alt-tekst laat het opstarten mislukken met een duidelijke melding.

### D5 — Lettertypen Fraunces en Inter, zelf gehost
Koppen: *Fraunces* (variabel, SIL OFL 1.1); tekst en UI: *Inter* (variabel, SIL OFL 1.1). Beide als WOFF2 (Latijnse
subset) in `Doctrina.UI/wwwroot/fonts/` met `OFL.txt`, `font-display: swap`. *Keuze uit voorstel §7.0:* Fraunces boven
Source Serif 4 om het eigen karakter; wisselen raakt alleen het token `--dc-font-heading`.

### D6 — Iconen: Lucide, als inline SVG
Een vaste subset Lucide-iconen (ISC) staat als `.svg` in `Doctrina.UI/Icons/` (ingesloten resources, met `LICENSE`).
`DcIcon Name="…"` rendert inline SVG met `currentColor`, standaard `aria-hidden="true"`; een onbekende naam geeft een
fout aan de ontwikkelaar. Inline in plaats van een icon font of sprite: geen extra verzoek, kleurt mee met tekst.

### D7 — Componenten in `Doctrina.UI`
Eerste set (namen volgen voorstel §7.3): `DcButton` (varianten primary/secondary/ghost/danger, `Href` maakt er een link
van, `Icon`, `Label` voor alleen-icoon, `Busy`), `DcLink`, `DcIcon`, `DcCard`, `DcPageHeader` (h1, beschrijving,
acties; zet ook de paginatitel "<titel> · Doctrina"), `DcAlert` (info/success/warning/danger; `role="status"` resp.
`role="alert"`), `DcBadge`, `DcTable<TItem>` (bijschrift, kolommen, lege toestand), `DcModal` (native `<dialog>`),
formulieren: `DcForm` (rond `EditForm`), `DcTextField`, `DcSelect`, `DcCheckbox`, `DcFieldError`, en `DcResult`
(toont een `Error`). Daarnaast de shell-bouwstenen `DcAppShell`, `DcNavMenu`, `DcAccountMenu`, `DcThemeSwitcher`.
Binnen `Doctrina.UI` mogen componenten Bootstrap-classes gebruiken; daarbuiten niet (architectuurtest).
Een knop zonder tekst en zonder `Label` gooit bij weergave een `InvalidOperationException` (spec "Knop zonder naam
wordt geweigerd"): fouten van ontwikkelaars komen zo in tests en de stijlgids boven, niet bij gebruikers.

### D8 — Fouttitels via een abstractie in `Doctrina.Core`
`DcResult` heeft de vertaalde titel nodig, maar `ErrorTitles` staat in `Doctrina.Infrastructure`, waar `Doctrina.UI`
niet naar mag verwijzen. Daarom komt een kleine interface `IErrorTitles` (`string For(string code, string fallback)`) in
`Doctrina.Core/Results`; `ErrorTitles` in Infrastructure implementeert haar. Validatiefouten (`Error.Details`) worden in
`DcForm` aan de velden gekoppeld door de sleutels hoofdletterongevoelig te vergelijken met de veldnamen van het
`EditContext` (API-sleutels zijn camelCase, eigenschappen PascalCase).

### D9 — App-shell zonder scripts: Popover API
`DcAppShell` levert skiplink, header (merk/logo, menu, accountgedeelte, themakeuze), `<main id="inhoud">`. Het hoofdmenu
en het accountmenu gebruiken de native Popover API (`popover` + `popovertarget`): openen/sluiten, Escape, focus en de
"uitgeklapt"-status regelt de browser, zonder script. Op brede schermen toont CSS het hoofdmenu altijd (een media query
overschrijft de verberging). `Doctrina.UI` kent de modulecatalogus niet: de host geeft de menu-items en accountgegevens
mee via parameters; de filtering op permissie blijft in de host (spec module-system).

### D10 — Dialoog met native `<dialog>`
`DcModal` gebruikt `<dialog>` met `showModal()`/`close()` via een klein JS-module in `Doctrina.UI` (de enige scriptcode).
`showModal()` maakt de rest van de pagina onbereikbaar, verplaatst de focus en sluit met Escape; het script onthoudt het
element dat de focus had en zet die bij sluiten terug. De titel krijgt een id waarnaar `aria-labelledby` verwijst.
`DcModal` vereist een interactieve render-modus.

### D11 — Stijlgids op `/stijlgids`
Een pagina in de host die elk component in zijn varianten toont, steeds in een lichte en een donkere container naast
elkaar (D3). Beschikbaar in Development of met `Ui:StyleGuide:Enabled`; anders roept de pagina
`NavigationManager.NotFound()` aan (404). Elke sectie heeft id `dc-<component>`; een test vergelijkt de lijst publieke
`Dc*`-componenten in `Doctrina.UI` met de secties, zodat een nieuw component niet ongemerkt ontbreekt.

### D12 — Teststrategie
- **Componenten:** nieuw project `tests/Doctrina.UI.Tests` (bUnit, verwijst alleen naar `Doctrina.UI` en `Doctrina.Core`)
  voor naam, labels, aria-attributen, lege toestanden, `DcResult`/`DcForm` en de JS-aanroepen van `DcModal`.
- **Tokens en CSS:** tests lezen `_tokens.scss` en de gecompileerde `doctrina.css`: contrast volgens de WCAG-formule voor
  een vaste lijst tokenparen per thema (tekst/achtergrond ≥ 4,5:1, focus en randen van bedieningselementen ≥ 3:1);
  knoppen gebruiken `var(--dc-accent)`; `prefers-reduced-motion` zet duur op (bijna) nul; geen letterlijke kleuren in
  `.razor.css` van `Doctrina.UI` buiten `_tokens.scss`.
- **Host:** integratietests voor themacookie en eerste weergave, white-label, externe bronnen (HTML en CSS bevatten geen
  `http(s)://` naar andere domeinen), paginatitel, accountmenu, stijlgids.
- **Architectuur:** de regel voor letterlijke kleuren (`#rgb`/`#rrggbb`, `rgb(`, `hsl(`) naast ruwe Bootstrap, voor
  modules én de host (spec app-shell); `Doctrina.UI` verwijst niet naar Infrastructure, de host of modules.
- **Niet geautomatiseerd** (geen browsertests, zie proposal): het echte gedrag van `<dialog>` en popovers in een browser.
  Dat wordt per release handmatig gecontroleerd in de stijlgids (taak in tasks.md).

### D13 — Afwijkingen van het voorstel, samengevat
PurgeCSS vervalt (D1); Bootstrap-JS vervalt ten gunste van native HTML (D9, D10); white-label is één thema per
installatie (proposal); `IErrorTitles` verhuist naar Core (D8); thema-attribuut `data-dc-theme` naast `data-bs-theme` (D3).

## Risico's / Afwegingen

- [Popover API en `color-mix()` zijn relatief nieuw (Baseline 2024/2023)] → doelgroep gebruikt actuele browsers;
  zonder popover-ondersteuning blijft het menu op brede schermen werken; vastgelegd in de stijlgids-controle.
- [Bootstrap in de repo veroudert] → versie en bron in `Styles/vendor/bootstrap/VERSION`; bijwerken is één map
  vervangen plus de stijlgids nalopen.
- [AspNetCore.SassCompiler levert een binary per platform] → ondersteunt Windows/Linux/macOS (x64/arm64); de CI-runner
  (Windows, vanwege LocalDB) is gedekt.
- [Bestaande hosttests controleren kale HTML] → `data-testid`-attributen blijven gelijk; tests op exacte tags worden
  bijgewerkt in deze change.
- [Interactieve pagina's en het thema-formulier] → een themawissel herlaadt de pagina; onvoltooide invoer kan verloren
  gaan. Acceptabel voor een zeldzame actie; de knop staat buiten formulieren.

## Migratieplan

Geen data of API's. Na deze change gebruiken alle hostpagina's `Doctrina.UI`; `wwwroot/app.css` van de host verdwijnt.
Terugdraaien = de commit terugdraaien.

## Open vragen

- Definitieve tint van de palettokens (§7.0 noemt richtwaarden). De contrasttests bewaken de ondergrens; fijnafstemming
  kan later zonder de specs te raken.
