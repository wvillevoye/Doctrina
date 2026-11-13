# Taken

## 1. Build en bronnen

- [x] 1.1 Voeg `AspNetCore.SassCompiler` (MIT, laatste stabiele versie) toe aan `Directory.Packages.props` en `Doctrina.UI`; configureer compilatie van `Styles/doctrina.scss` naar `wwwroot/css/doctrina.css` en zet die uitvoer in `.gitignore`; controleer dat `dotnet build` op een schone checkout `doctrina.css` maakt zonder Node.js
- [x] 1.2 Neem Bootstrap 5.3.x-SCSS op in `src/Doctrina.UI/Styles/vendor/bootstrap/` met `LICENSE` en `VERSION` (versie + bron-URL); importeer alleen de onderdelen uit design D1; controleer dat de build slaagt en de CSS geen Bootstrap-JS-afhankelijke onderdelen (modal, dropdown, collapse) bevat
- [x] 1.3 Neem Fraunces en Inter (variabel, WOFF2, Latijn) op in `Doctrina.UI/wwwroot/fonts/` met `OFL.txt`, en de Lucide-subset (`.svg`, ingesloten resource) in `Doctrina.UI/Icons/` met `LICENSE`; controleer dat de bestanden als `_content/Doctrina.UI/...` worden geserveerd (integratietest op status 200)
- [x] 1.4 Maak testproject `tests/Doctrina.UI.Tests` (bUnit, xUnit v3; verwijst naar `Doctrina.UI` en `Doctrina.Core`) en voeg het toe aan `Doctrina.slnx`; voeg een architectuurtest toe dat `Doctrina.UI` niet naar `Doctrina.Infrastructure`, `Doctrina.Host` of modules verwijst; controleer `dotnet test`
- [x] 1.5 Werk `docs/Doctrina-voorstel.md` §8 bij ("Specs in het Engels" → Nederlands, zie design D12 van add-modular-foundation); controleer met een zoekopdracht dat de oude regel weg is

## 2. Tokens en thema-CSS

- [x] 2.1 Schrijf `Styles/_tokens.scss` (palet, semantische kleuren met `-contrast`, typografie, spacing, radius, schaduw, motion; licht, `[data-dc-theme=dark]` en `[data-dc-theme=system]` met `prefers-color-scheme`), één token per regel; controleer met tests `KleurcontrastVoldoetAanWcag22AaInBeideThemas.TekstcontrastInHetLichteThema`, `…TekstcontrastInHetDonkereThema` en `…FocusIndicatorValtOp` (WCAG-contrastformule over een vaste lijst tokenparen)
- [x] 2.2 Schrijf `_bootstrap-bridge.scss` (Bootstrap-runtimevariabelen → `var(--dc-…)`, afgeleide toestanden met `color-mix()`), typografie met de zelf gehoste lettertypen en de reduced-motion-regels; controleer met tests `DesignTokensZijnDeEnigeBronVanDeVormgeving.GewijzigdAccenttokenWerktDoorInKnoppen` (gecompileerde knopstijl gebruikt `var(--dc-accent)`) en `BewegingRespecteertDeVoorkeurVanDeGebruiker.MinderBewegingGevraagd`
- [x] 2.3 Controleer met test `DesignTokensZijnDeEnigeBronVanDeVormgeving.ComponentstijlenBevattenGeenLetterlijkeKleuren` dat `.razor.css`- en `.scss`-bestanden van `Doctrina.UI` buiten `_tokens.scss` en de vendor-map geen letterlijke kleuren bevatten

## 3. Themakeuze en white-label in de host

- [x] 3.1 Laat `App.razor` `doctrina.css`, de lettertypen en `data-dc-theme`/`data-bs-theme` op `<html>` zetten op basis van de cookie `doctrina.theme`; voeg `POST /thema` toe (antiforgery, `theme` ∈ light/dark/system, lokale `returnUrl`) dat de cookie zet en terugstuurt; controleer met tests `LichtEnDonkerThemaNaarKeuzeVanDeGebruiker.ZonderKeuzeVolgtHetThemaHetSysteem`, `…GekozenDonkerThemaWordtOnthouden`, `…TerugNaarDeSysteeminstelling` en `…ThemakeuzeWerktZonderScripts`
- [x] 3.2 Voeg `Theme:StylesheetPath`, `Theme:LogoPath` en `Theme:LogoAlt` toe met opstartvalidatie (lokaal pad, alt-tekst verplicht bij logo); controleer met tests `EenInstellingKanHetThemaAanpassenZonderCode.EigenThemaStylesheetWordtNaDeStandaardstijlenGeladen`, `…EigenLogoVervangtHetWoordmerk`, `…ZonderEigenThemaDeStandaardvormgeving` en een test voor een ongeldig pad
- [x] 3.3 Controleer met test `LettertypenEnIconenWordenZelfGehost.GeenExterneBronnenVoorDeVormgeving` dat de startpagina en de geladen stylesheets geen `http(s)://` naar andere domeinen bevatten
- [x] 3.4 Beschrijf white-label (instellingen, voorbeeld-stylesheet met alleen `--dc-*`, waar het bestand moet staan) in `docs/ontwikkelen/vormgeving.md`; controleer dat het voorbeeld uit het document in test 3.2 wordt gebruikt

## 4. Basiscomponenten

- [x] 4.1 Maak `DcIcon` (Lucide inline SVG, `aria-hidden`, fout bij onbekende naam), `DcButton` (varianten, `Href`, `Icon`, `Label`, `Busy`) en `DcLink`; controleer met bUnit-tests `KnoppenEnLinksHebbenAltijdEenToegankelijkeNaam.KnopMetAlleenEenIcoonHeeftEenLabel`, `…KnopZonderNaamWordtGeweigerd` en `…BezigeKnop`
- [x] 4.2 Maak `DcCard`, `DcPageHeader` (h1, beschrijving, acties, paginatitel "<titel> · Doctrina"), `DcAlert` (`role` per ernst) en `DcBadge`, met `.razor.css` die alleen tokens gebruikt; controleer met bUnit-tests per component (opbouw, rollen, titel)
- [x] 4.3 Voeg `IErrorTitles` toe aan `Doctrina.Core/Results` en laat `ErrorTitles` in Infrastructure die implementeren en registreren; controleer dat de bestaande fouttitel-tests slagen en de architectuurtests voor Core groen blijven

## 5. Tabellen, formulieren, Result en dialoog

- [x] 5.1 Maak `DcTable<TItem>` (bijschrift, kolomkoppen met `scope="col"`, lege toestand); controleer met bUnit-tests `TabellenZijnSemantischEnHebbenEenLegeToestand.KolomkoppenEnBijschrift` en `…LegeTabel`
- [x] 5.2 Maak `DcForm`, `DcTextField`, `DcSelect`, `DcCheckbox` en `DcFieldError` (label gekoppeld, verplicht gemarkeerd, `aria-invalid` + `aria-describedby` bij fouten); controleer met bUnit-tests `FormulierveldenZijnGekoppeldAanLabelEnFoutmelding.LabelHoortBijHetInvoerveld`, `…VerplichtVeld` en `…VeldMetEenValidatiefout`
- [x] 5.3 Maak `DcResult` (vertaalde titel via `IErrorTitles` in een melding) en de koppeling van `Error.Details` aan de velden van `DcForm` (hoofdletterongevoelig); controleer met bUnit-tests `FoutenUitEenResultWordenEenduidigGetoond.FoutWordtAlsMeldingGetoond` en `…ValidatiefoutenVerschijnenBijDeVelden`
- [x] 5.4 Maak `DcModal` (native `<dialog>`, JS-module voor `showModal`/`close` en focusherstel, `aria-labelledby` naar de titel, sluitknop); controleer met bUnit-test `DialogenHoudenDeFocusVast.DialoogOpenenEnMetEscapeSluiten` (titel als naam, JS-aanroepen voor openen, sluiten via Escape-event en focusherstel)

## 6. App-shell en hostpagina's

- [x] 6.1 Maak `DcAppShell`, `DcNavMenu` (Popover API, "Menu"-knop, `aria-current="page"`), `DcAccountMenu` en `DcThemeSwitcher` (formulier naar `POST /thema`, actieve keuze met `aria-pressed`/tekst); controleer met bUnit-tests `HetHoofdmenuWerktOpElkSchermformaat.MenuknopOpSmalleSchermen` en `DeThemakeuzeStaatInDeHeader.ActieveThemakeuze`
- [x] 6.2 Zet `MainLayout`, `NavMenu` en de pagina's start, profiel, geen toegang, fout en `NietToegestaan` om naar de componenten; verwijder `wwwroot/app.css` van de host; houd bestaande `data-testid`-attributen gelijk en werk tests bij die op kale HTML controleren; controleer met integratietests `ElkePaginaHeeftDezelfdeToegankelijkeOpbouw.OpbouwVanEenPagina`, `…Paginatitel`, `HetAccountgedeelteToontWieErIsIngelogd.AnoniemeBezoeker`, `…IngelogdeGebruiker` en `HetHoofdmenuWerktOpElkSchermformaat.HuidigePaginaInHetMenu`, en dat de hele suite groen is
- [x] 6.3 Breid de architectuurregel uit met letterlijke kleuren (`#rgb`/`#rrggbb`, `rgb(`, `hsl(`) in `.razor`, `.razor.css` en `.css`, voor modules en voor de host; voeg fixtures toe; controleer met tests `ModulesGevenAlleenVormViaDeComponentenbibliotheek.LetterlijkeKleurInEenModuleLaatDeTestsFalen`, `…TokenGebruikenIsToegestaan` en `HostpaginasGebruikenDeComponentenbibliotheek.HostpaginasVolgenDeRegelsVoorModules`

## 7. Stijlgids en documentatie

- [x] 7.1 Maak `/stijlgids` in de host: elk component in zijn varianten, licht en donker naast elkaar, alleen in Development of met `Ui:StyleGuide:Enabled` (anders `NavigationManager.NotFound()`); controleer met tests `DeStijlgidsToontElkComponent.ElkComponentStaatInDeStijlgids` (reflectie over de publieke `Dc*`-componenten tegen de secties `dc-<component>`) en `…StijlgidsStandaardVerborgenInProductie`
- [x] 7.2 Schrijf `docs/ontwikkelen/vormgeving.md` af (tokens, thema's, componenten met voorbeelden, regels voor modules, stijlgids, nieuw component toevoegen) en werk `docs/ontwikkelen/modules.md` en `starten.md` bij (stijlgids, themakeuze); controleer dat alle verwijzingen naar bestanden en componenten in de documenten bestaan

## 8. Integratiecontrole

- [x] 8.1 Draai `dotnet test` voor de hele solution en maak de traceerbaarheidstabel (elk scenario van `theming`, `ui-components` en `app-shell` heeft een slagende test) in `verificatie.md` van deze change
- [x] 8.2 Handmatige browsercontrole in de stijlgids (Edge/Chrome en Firefox): dialoog (focus, Escape, focusherstel), menu en accountmenu als popover op een smal venster en met het toetsenbord, themawissel licht/donker/systeem zonder flits, `prefers-reduced-motion`; leg de uitkomst vast in `verificatie.md`

## Vervolg in de workflow

- Beoordeel de change (proposal, specs, ontwerpbesluiten D1–D13) vóór `/opsx:apply`.
- Na afronding: commit op een branch, push naar GitHub, PR; na de merge `/opsx:archive`.
