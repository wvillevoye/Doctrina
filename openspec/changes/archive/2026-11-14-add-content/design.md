# Design

## Context

Zie `proposal.md` voor aanleiding en scope. Waar deze change op voortbouwt:

- De uitvoeringspagina `/courses/{offering}` (Academic) toont secties die modules registreren met
  `AddOfferingPageSection<T>(order)`; Groups staat op 20. De pagina is statisch gerenderd.
- `IAcademicDirectory` (Academic.Contracts) geeft de uitvoering, de persoon van de ingelogde gebruiker en de actieve
  leden met hun rol. `GroupsAccess.CanManageAsync` combineert die tot "docent van deze uitvoering of `Groups.Manage`".
  Het design van `add-groups` stelde het generaliseren daarvan uit tot een tweede module het nodig heeft: dat is nu.
- `IModule.MapEndpoints` geeft alleen de `/api/v1`-groep (bearer-tokens). Een module kan dus geen gewone
  browser-download met de inlogcookie aanbieden; de importpagina's downloaden daarom via JS (`Downloads` +
  `download.js` in de module Academic).
- Beheerpagina's zijn interactief en voeren elke actie in een eigen scope uit (`ManageGroups`), met
  `Result`-foutcodes en titels uit een `.resx`.
- Uploads van de bulkimport staan in de database (`varbinary(max)`, hoogstens 50 MB, 30 dagen).

Keuzes van de gebruiker vóór dit ontwerp (2026-11-14): bestanden in een map op schijf, pagina's in Markdown, en in
deze change alleen de UI (geen REST-API voor content).

## Goals / Non-Goals

**Goals:**
- Een contentstructuur die `add-lti-platform` (een vierde itemsoort) en `add-course-templates` (kopiëren) kunnen
  uitbreiden zonder het model om te gooien.
- Bestandsopslag zonder extra dienst, die later te vervangen is door object storage.

**Non-Goals:**
- Een opslagabstractie met meerdere implementaties: er is er één, dus één concrete klasse (D5).
- Een algemeen rechtenmodel per uitvoering: alleen de gedeelde rolopvraag uit D4.
- Een endpoint voor bestanden buiten `/api/v1` (D6).

## Decisions

### D1: Module `Content`, zonder `.Contracts`-project

`src/Modules/Content/Doctrina.Modules.Content` met schema `content`, `DependsOn` Academic, de permissies
`Content.Manage` en `Content.Read`, health check en fouttitels (`ContentErrors.resx`/`.en.resx`), zoals Groups. Geen
menu-item: content bereik je via de uitvoeringspagina.

Er komt geen `Doctrina.Modules.Content.Contracts`: niemand gebruikt het nog. De change die content van buiten nodig
heeft (sjablonen, LTI) voegt het toe.

### D2: Datamodel in schema `content`: twee tabellen, één tabel voor alle itemsoorten

| Tabel | Velden |
|---|---|
| `Sections` (`ExchangeableEntity`) | `OfferingId`, `Title` (200), `Position`, `Published` |
| `Items` (`ExchangeableEntity`) | `SectionId` (FK), `Kind` (`Page`, `File`, `Link`; opgeslagen als tekst), `Title` (200), `Position`, `Published`, `Body` (`nvarchar(max)`, pagina), `Url` (2048, link), `FileName` (255), `ContentType` (255), `Size` (bestand) |

- `OfferingId` verwijst naar Academic zonder foreign key, zoals in Groups; de service controleert de uitvoering via
  `IAcademicDirectory`.
- `ExternalId` is het eigen `Id` en `Source` is "Doctrina", zoals bij groepen (huisregel 6).
- Verwijderen zet `Status` op `ToBeDeleted`, voor de sectie en al haar items, zoals in Groups. Alle queries filteren
  op actieve records. Het bestand van een verwijderd item gaat direct van schijf (D5).
- `Position` is een oplopend geheel getal binnen de uitvoering (secties) of de sectie (items). Nieuw komt achteraan
  (hoogste + 1); verplaatsen wisselt `Position` met de actieve buur (D3).

Alternatief: een tabel per itemsoort of overerving (TPH/TPT). Afgewezen: drie soorten met samen vijf eigen velden
rechtvaardigen dat niet, en lijsten en volgorde gaan altijd over alle soorten tegelijk. Een LTI-item wordt later een
vierde `Kind` met een verwijzing.

### D3: Eén service voor alle wijzigingen

`ContentService` (intern, scoped) doet alles wat opslaat: secties en items aanmaken, hernoemen, verwijderen,
publiceren/verbergen, verplaatsen, de tekst van een pagina opslaan en een bestand toevoegen. Elke methode slaat één
keer op en geeft een `Result` met codes `Content.Section.*`, `Content.Item.*` en `Content.File.*`.

Lezen gaat via één methode `SectionsAsync(offeringId, includeDrafts)`: secties met hun items op volgorde. Zonder
concepten laat ze verborgen secties weg en verborgen items in zichtbare secties. De sectie op de uitvoeringspagina,
de beheerpagina en de itempagina gebruiken allemaal deze methode, zodat de zichtbaarheidsregel op één plek staat.

Er zijn geen domeinevents: niemand luistert ernaar.

### D4: Rollen in een uitvoering: één gedeelde opvraag in Academic.Contracts

`Doctrina.Modules.Academic.Contracts` krijgt een extensiemethode op `IAcademicDirectory`:
`CurrentRolesAsync(user, offeringId)` → de rollen van de ingelogde gebruiker in die uitvoering (leeg zonder persoon of
inschrijving). Ze combineert de bestaande `CurrentPersonIdAsync` en `MembersAsync`; de interface verandert niet.
`GroupsAccess` gaat haar gebruiken in plaats van de eigen combinatie.

`ContentAccess` in Content bouwt daarop:
- `CanManageAsync`: `Content.Manage`, of rol `lecturer` of `coordinator` in de uitvoering.
- `CanReadAsync`: mag beheren, of `Content.Read`, of een actieve inschrijving in de uitvoering (elke rol).

Teaching assistants beheren geen content, net als bij groepen. Alternatief: een algemeen rechtenmodel per uitvoering
(permissies per rol per uitvoering). Afgewezen: twee modules met dezelfde regel "docent of permissie" hebben genoeg
aan een gedeelde rolopvraag.

### D5: Bestanden in een map op schijf, gegevens in de database

`ContentFileStore` (één concrete klasse, geen interface) bewaart een bestand als
`{Content:Files:Path}/{offeringId}/{itemId}`, zonder extensie. De naam op schijf komt dus nooit uit gebruikersinvoer.
De oorspronkelijke bestandsnaam (alleen het laatste deel van het pad, ingekort tot 255 tekens), het type en de grootte
staan in `Items`.

- Instellingen: `Content:Files:Path` (standaard `App_Data/content` onder de content root; in `Test` een tijdelijke map
  per testhost) en `Content:Files:MaxSizeMb` (standaard 100). `App_Data/` komt in `.gitignore`.
- Uploaden: de stream van `InputFile` (met de grens als `maxAllowedSize`) gaat naar `{itemId}.tmp` en wordt daarna
  hernoemd; pas dan wordt het item opgeslagen. Mislukt het opslaan, dan wordt het bestand weer verwijderd. Een te groot
  bestand geeft `Content.File.TooLarge` en laat niets achter.
- Verwijderen: eerst de status in de database, dan het bestand. Lukt het verwijderen van het bestand niet, dan wordt
  dat gelogd (zonder bestandsnaam) en blijft er een onbereikbaar bestand achter.

Alternatieven: in SQL Server zoals de bulkimport (afgewezen: lesmateriaal is groter en blijft jaren staan, de database
en haar back-ups groeien mee) en S3-compatibele object storage (afgewezen door de gebruiker: een extra dienst nu al).
Wie later naar object storage wil, vervangt alleen deze klasse.

### D6: Downloaden via de gedeelde JS-download, geen eigen endpoint

`Downloads` en `download.js` verhuizen van de module Academic naar `Doctrina.UI`, met een variant die een `Stream`
aanneemt. Content geeft daar de `FileStream` aan door, zodat een bestand van 100 MB niet in het geheugen komt. De
knop "Download" is een klein interactief component dat bij de klik opnieuw `CanReadAsync` en de zichtbaarheid
controleert.

Alternatief: een GET-endpoint met de inlogcookie, zodat een gewone link volstaat. Afgewezen voor nu: `IModule` biedt
alleen de `/api/v1`-groep met bearer-tokens, dus dat vraagt een nieuwe haak in het modulecontract. Die komt zodra
bestanden in de browser bekeken moeten worden of afbeeldingen in een pagina moeten kunnen.

### D7: Markdown met Markdig, HTML uit, links gefilterd

Nieuw package **Markdig** (BSD-2-Clause, laatste stabiele versie) in `Directory.Packages.props`. Eén statische
functie `Markdown.ToSafeHtml(text)` in Content:

- pipeline: CommonMark met tabellen en automatische links, en `DisableHtml()` zodat HTML in de tekst als tekst
  verschijnt;
- na het parsen worden links en afbeeldingen nagelopen: een adres dat niet met `http://`, `https://` of `mailto:`
  begint (afbeeldingen: alleen `http(s)://`) wordt leeggemaakt.

De uitvoer gaat als `MarkupString` in een component `MarkdownView` met de typografie van `Doctrina.UI`. De test van
het scenario "HTML en scripts worden niet uitgevoerd" bewaakt dit; hoe Markdig precies met `javascript:`-links omgaat
hoeft daarvoor niet vast te staan.

Alternatief: een WYSIWYG-editor die HTML opslaat. Afgewezen door de gebruiker; het vraagt een JS-editor en het
opschonen van HTML.

### D8: Pagina's

- Sectie **"Content"** op de uitvoeringspagina (statisch, volgorde 10, dus boven "My groups"): per sectie een kop en
  een lijst met items. Een pagina en een bestand linken naar de itempagina, een link direct naar het adres
  (`target="_blank"`, `rel="noopener noreferrer"`). Voor wie mag beheren: ook concepten met `DcBadge` "Draft" en de
  knop "Manage content". Zonder zichtbare content: "No content yet." Wie niet mag lezen, ziet de sectie niet.
- **`/courses/{offering}/content`** (interactief, zoals `ManageGroups`): een `DcCard` per sectie met haar items; per
  sectie en item de acties hernoemen, "Publish"/"Hide", "Move up"/"Move down" en verwijderen (met `DcModal`);
  formulieren voor een nieuwe sectie, pagina (alleen de titel, daarna door naar bewerken), link en bestand.
- **`/courses/{offering}/content/{item}`** (statisch): de titel, voor een pagina de opgemaakte tekst, voor een bestand
  de naam, de grootte en de knop "Download" (D6). Een link-item verwijst door naar zijn adres. 403 zonder leesrecht,
  404 voor een onbekend, verwijderd of niet-zichtbaar item. Voor wie mag beheren: "Draft" en een knop "Edit".
- **`/courses/{offering}/content/{item}/edit`** (interactief, alleen pagina's): titel, tekstvak, "Preview" (toont
  `MarkdownView` van de huidige tekst zonder op te slaan) en "Save".

Alle teksten zijn Engels; alleen `Dc*`-componenten en `--dc-*`-tokens.

### D9: Afwijkingen van het voorstel

- §6.4 noemt LTI-items als contentsoort. Die komen in `add-lti-platform`, dat van deze change afhangt.
- §10 laat de bestandsopslag open (SQL, bestandsshare of object storage). Besluit: een map op schijf (D5).
- §3 gaat uit van een `.Contracts`-project per module. Content krijgt er pas een als een andere module het nodig heeft
  (D1).

## Risks / Trade-offs

- [Database en map lopen uiteen: een bestand zonder item of een item zonder bestand] → De volgorde uit D5 laat hoogstens
  een onbereikbaar bestand achter, nooit een item zonder bestand. Ontbreekt een bestand toch (bv. na een terugzetactie),
  dan geeft downloaden `Content.File.Missing` in plaats van een fout.
- [De map met bestanden zit niet in de back-up van de database] → Vastleggen in de documentatie; bij meer dan één
  webserver moet `Content:Files:Path` een gedeelde share zijn.
- [Uploaden via de Blazor-verbinding is traag bij grote bestanden] → De grens is instelbaar; een upload-endpoint komt
  met de haak uit D6 als het nodig blijkt.
- [Een bestand is alleen te downloaden, niet te bekijken] → Bewust (D6); het voorkomt ook dat geüploade HTML in de
  browser wordt uitgevoerd onder het domein van Doctrina.
- [Verwijderen is definitief voor bestanden, terwijl de rij blijft staan] → De bevestiging zegt dat; een prullenbak
  valt buiten de scope.
- [`Position` kan gaten krijgen na verwijderen] → Geen probleem: de volgorde telt, niet de waarde.

## Migration Plan

Nieuw schema `content` met één migratie; geen wijziging in bestaande schema's. De module staat alleen aan waar
`Modules:Enabled` haar noemt. Terugdraaien: de module uitzetten; schema en map blijven staan.
