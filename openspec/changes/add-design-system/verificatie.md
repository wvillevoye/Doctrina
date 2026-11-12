# Verificatie — add-design-system

## Geautomatiseerde tests (taak 8.1)

`dotnet test` voor de hele solution op 2026-11-12: **252 tests, 0 mislukt** (Doctrina.Core.Tests,
Doctrina.Architecture.Tests, Doctrina.UI.Tests, Doctrina.Host.IntegrationTests). De tests op de gecompileerde CSS slagen
zowel tegen de uitgebreide (Debug) als de gecomprimeerde (Release) `doctrina.css`.

## Traceerbaarheid: scenario → test

Projecten: **UI** = `tests/Doctrina.UI.Tests`, **Host** = `tests/Doctrina.Host.IntegrationTests`,
**Arch** = `tests/Doctrina.Architecture.Tests`. Klasse = requirement, methode = scenario.

### theming

| Requirement | Scenario | Test |
|---|---|---|
| Design tokens zijn de enige bron van de vormgeving | Gewijzigd accenttoken werkt door in knoppen | UI `DesignTokensZijnDeEnigeBronVanDeVormgeving.GewijzigdAccenttokenWerktDoorInKnoppen` (+ `BootstrapVolgtDeTokensOokInGenesteThemas`) |
| | Componentstijlen bevatten geen letterlijke kleuren | UI `DesignTokensZijnDeEnigeBronVanDeVormgeving.ComponentstijlenBevattenGeenLetterlijkeKleuren` |
| Licht en donker thema naar keuze van de gebruiker | Zonder keuze volgt het thema het systeem | Host `LichtEnDonkerThemaNaarKeuzeVanDeGebruiker.ZonderKeuzeVolgtHetThemaHetSysteem` |
| | Gekozen donker thema wordt onthouden | Host `LichtEnDonkerThemaNaarKeuzeVanDeGebruiker.GekozenDonkerThemaWordtOnthouden` |
| | Terug naar de systeeminstelling | Host `LichtEnDonkerThemaNaarKeuzeVanDeGebruiker.TerugNaarDeSysteeminstelling` |
| | Themakeuze werkt zonder scripts | Host `LichtEnDonkerThemaNaarKeuzeVanDeGebruiker.ThemakeuzeWerktZonderScripts` (+ `OnbekendThemaOfExternTerugkeeradresWordtGeweigerd`, `ZonderAntiforgeryTokenGeenKeuze`) |
| Kleurcontrast voldoet aan WCAG 2.2 AA in beide thema's | Tekstcontrast in het lichte thema | UI `KleurcontrastVoldoetAanWcag22AaInBeideThemas.TekstcontrastInHetLichteThema` (23 tokenparen) |
| | Tekstcontrast in het donkere thema | UI `KleurcontrastVoldoetAanWcag22AaInBeideThemas.TekstcontrastInHetDonkereThema` (23 tokenparen) |
| | Focus-indicator valt op | UI `KleurcontrastVoldoetAanWcag22AaInBeideThemas.FocusIndicatorValtOp` (focus en randen, beide thema's) |
| Een instelling kan het thema aanpassen zonder code | Eigen thema-stylesheet wordt na de standaardstijlen geladen | Host `EenInstellingKanHetThemaAanpassenZonderCode.EigenThemaStylesheetWordtNaDeStandaardstijlenGeladen` (+ `OngeldigeThemaInstellingLaatHetOpstartenMislukken`, `VoorbeeldUitDeDocumentatieBevatAlleenBestaandeTokens`) |
| | Eigen logo vervangt het woordmerk | Host `EenInstellingKanHetThemaAanpassenZonderCode.EigenLogoVervangtHetWoordmerk` |
| | Zonder eigen thema de standaardvormgeving | Host `EenInstellingKanHetThemaAanpassenZonderCode.ZonderEigenThemaDeStandaardvormgeving` |
| Lettertypen en iconen worden zelf gehost | Geen externe bronnen voor de vormgeving | Host `LettertypenEnIconenWordenZelfGehost.GeenExterneBronnenVoorDeVormgeving` (+ `BronnenVanDeVormgevingWordenDoorDoctrinaGeserveerd`; UI `IcoonIsInlineSvgZonderExterneVerwijzing`) |
| Beweging respecteert de voorkeur van de gebruiker | Minder beweging gevraagd | UI `BewegingRespecteertDeVoorkeurVanDeGebruiker.MinderBewegingGevraagd` (+ `AnimatiesZijnKort`) |

### ui-components

| Requirement | Scenario | Test |
|---|---|---|
| Knoppen en links hebben altijd een toegankelijke naam | Knop met alleen een icoon heeft een label | UI `KnoppenEnLinksHebbenAltijdEenToegankelijkeNaam.KnopMetAlleenEenIcoonHeeftEenLabel` |
| | Knop zonder naam wordt geweigerd | UI `KnoppenEnLinksHebbenAltijdEenToegankelijkeNaam.KnopZonderNaamWordtGeweigerd` (+ `LinkZonderTekstWordtGeweigerd`) |
| | Bezige knop | UI `KnoppenEnLinksHebbenAltijdEenToegankelijkeNaam.BezigeKnop` |
| Formuliervelden zijn gekoppeld aan label en foutmelding | Label hoort bij het invoerveld | UI `FormulierveldenZijnGekoppeldAanLabelEnFoutmelding.LabelHoortBijHetInvoerveld` |
| | Verplicht veld | UI `FormulierveldenZijnGekoppeldAanLabelEnFoutmelding.VerplichtVeld` |
| | Veld met een validatiefout | UI `FormulierveldenZijnGekoppeldAanLabelEnFoutmelding.VeldMetEenValidatiefout` |
| Fouten uit een Result worden eenduidig getoond | Fout wordt als melding getoond | UI `FoutenUitEenResultWordenEenduidigGetoond.FoutWordtAlsMeldingGetoond` (+ Host `FouttitelsZijnVertaald.ComponentenKrijgenDezelfdeTitelsViaIErrorTitles`) |
| | Validatiefouten verschijnen bij de velden | UI `FoutenUitEenResultWordenEenduidigGetoond.ValidatiefoutenVerschijnenBijDeVelden` (+ `GewijzigdVeldVerliestDeMeldingVanDeServer`) |
| Dialogen houden de focus vast | Dialoog openen en met Escape sluiten | UI `DialogenHoudenDeFocusVast.DialoogOpenenEnMetEscapeSluiten` en `ScriptZetDeFocusTerugNaSluiten`; het browsergedrag zelf: handmatige controle hieronder |
| Tabellen zijn semantisch en hebben een lege toestand | Kolomkoppen en bijschrift | UI `TabellenZijnSemantischEnHebbenEenLegeToestand.KolomkoppenEnBijschrift` |
| | Lege tabel | UI `TabellenZijnSemantischEnHebbenEenLegeToestand.LegeTabel` |
| De stijlgids toont elk component | Elk component staat in de stijlgids | Host `DeStijlgidsToontElkComponent.ElkComponentStaatInDeStijlgids` |
| | Stijlgids standaard verborgen in productie | Host `DeStijlgidsToontElkComponent.StijlgidsStandaardVerborgenInProductie` (+ `StijlgidsAanTeZettenBuitenDevelopment`) |
| Modules geven alleen vorm via de componentenbibliotheek | Letterlijke kleur in een module laat de tests falen | Arch `ModulesGevenAlleenVormViaDeComponentenbibliotheek.LetterlijkeKleurInEenModuleLaatDeTestsFalen` |
| | Token gebruiken is toegestaan | Arch `ModulesGevenAlleenVormViaDeComponentenbibliotheek.TokenGebruikenIsToegestaan` |

### app-shell

| Requirement | Scenario | Test |
|---|---|---|
| Elke pagina heeft dezelfde toegankelijke opbouw | Opbouw van een pagina | Host `ElkePaginaHeeftDezelfdeToegankelijkeOpbouw.OpbouwVanEenPagina` (+ UI `ShellHeeftSkiplinkLandmarksEnMerk`) |
| | Paginatitel | Host `ElkePaginaHeeftDezelfdeToegankelijkeOpbouw.Paginatitel` (+ UI `DcPageHeaderTests.ZetDeTitelVanHetBrowservenster`) |
| Het accountgedeelte toont wie er is ingelogd | Anonieme bezoeker | Host `HetAccountgedeelteToontWieErIsIngelogd.AnoniemeBezoeker` (+ UI idem) |
| | Ingelogde gebruiker | Host `HetAccountgedeelteToontWieErIsIngelogd.IngelogdeGebruiker` (+ UI idem) |
| Het hoofdmenu werkt op elk schermformaat | Menuknop op smalle schermen | UI `HetHoofdmenuWerktOpElkSchermformaat.MenuknopOpSmalleSchermen` (+ `OpBredeSchermenIsHetMenuAltijdZichtbaar`) |
| | Huidige pagina in het menu | Host `HetHoofdmenuWerktOpElkSchermformaat.HuidigePaginaInHetMenu` (+ UI idem) |
| De themakeuze staat in de header | Actieve themakeuze | Host `DeThemakeuzeStaatInDeHeader.ActieveThemakeuze` (+ UI idem en `ThemakeuzeIsEenGewoonFormulier`) |
| Hostpagina's gebruiken de componentenbibliotheek | Hostpagina's volgen de regels voor modules | Arch `HostpaginasGebruikenDeComponentenbibliotheek.HostpaginasVolgenDeRegelsVoorModules` |

Overige controles uit de taken: architectuurregel `DoctrinaUiHangtAlleenAfVanCore.UiVerwijstNietNaarInfrastructuurHostOfModules`
(taak 1.4), `GeenJavaScriptAfhankelijkeBootstrapOnderdelen` (taak 1.2), en de bestaande suites van
`add-modular-foundation` (bijgewerkt waar ze op kale HTML controleerden).

## Handmatige browsercontrole (taak 8.2)

Nog uit te voeren in de stijlgids (https://localhost:5001/stijlgids), in Edge of Chrome én in Firefox:

| Controle | Edge/Chrome | Firefox |
|---|---|---|
| Dialoog: focus naar de dialoog bij openen, Tab blijft erin, Escape sluit, focus terug naar de knop | – | – |
| Hoofdmenu op een smal venster: knop "Menu" opent/sluit (ook met Enter/Spatie en Escape) | – | – |
| Accountmenu als popover, met het toetsenbord te bedienen | – | – |
| Themawissel licht/donker/systeem, zonder flits bij het laden | – | – |
| `prefers-reduced-motion`: geen animatie van dialoog en spinner | – | – |
