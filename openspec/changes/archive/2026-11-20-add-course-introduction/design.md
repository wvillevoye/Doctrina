# Design

## Context

Zie `proposal.md`. Observaties in de huidige code:

- De cursuspagina (`CoursePage.razor`, module Academic) toont secties die andere modules registreren met
  `AddOfferingPageSection<T>(order, title, icon)`; Content registreert `ContentSectionList` met `order: 10`. Titel en
  icoon komen ook in het cursusmenu in de zijbalk.
- Content heeft `ContentService`, `ContentAccess.CanManageAsync(user, offeringId)`, `Markdown.ToSafeHtml` (Markdig, HTML
  uit, alleen `http(s)`/`mailto:`-adressen), `MarkdownView` en `EditContentPage` (tekstveld + knop "Preview").
- Entiteiten in Content erven `ExchangeableEntity` (Id, ExternalId, Status, DateLastModified, Source); `OfferingId`
  heeft geen foreign key naar Academic.
- `ContentModule.Navigation` is leeg; modules leveren menu-items met `RequiredPermission` en `Icon`.

## Goals / Non-Goals

**Goals:**
- Eén introductie per uitvoering, bovenaan, in een door beheerders vastgelegde opmaak.
- Hergebruik van bestaande Markdown-weergave en toegangsregels.

**Non-Goals:**
- Geen HTML-editor, geen vrije CSS, geen afbeeldingen uploaden, geen API, geen kopie bij uitvoeringen kopiëren
  (zie proposal).

## Decisions

### D1: Markdown in plaats van een HTML-editor

De gebruiker vroeg om een HTML-editor; na overleg is gekozen voor een Markdown-editor. Redenen: `Markdown.ToSafeHtml`
saneert al, er is geen nieuw (JS-)pakket of licentiekeuze nodig, en docenten kennen het patroon van de contentpagina's.
Alternatief: een WYSIWYG-editor (Quill, TipTap): afgewezen voor nu; vereist een JS-bundel, HTML-sanering op de server en
een extra afweging per pakket (huisregel 8). Een editor kan later bovenop dezelfde opslag (tekst) komen.

### D2: Twee entiteiten in het schema `content`

- `IntroductionTemplate`: `Name` (max 100), `Layout` (opgeslagen als tekst: `plain`, `banner`, `objectives`), `StartBody`
  (Markdown), `Position`, `IsDefault`.
- `CourseIntroduction`: `OfferingId` (uniek, geen foreign key naar Academic), `TemplateId` (optioneel, geen cascade),
  `Body`, `Published`.

Beide erven `ExchangeableEntity` (huisregel 6), Id's door EF (huisregel 5), eigen migratie `AddCourseIntroduction`.
Het sjabloon "Default" (`plain`, lege startinhoud) maakt `IntroductionService` bij eerste gebruik aan, zodat er altijd
één bestaat; een migratie kan de door EF gegenereerde sequentiële Id niet leveren (huisregel 5). Verwijderen is zacht (`Status` = inactief); een introductie die naar een inactief sjabloon wijst, valt terug op
"Default" (scenario "Verwijderd sjabloon").

### D3: Opmaak is een vaste set, geen vrije CSS

`Layout` kiest een CSS-klasse (`intro--plain`, `intro--banner`, `intro--objectives`) in een scoped stylesheet van
`IntroductionSection`, uitsluitend met `--dc-*`-tokens (huisregel 4, architectuurtest op letterlijke kleuren geldt).
Beheerders leveren dus naam, keuze uit de set en startinhoud; geen eigen stijlen. Dit houdt de opmaak thema-vast
(licht/donker, white-label) en voorkomt CSS-injectie. Een nieuwe opmaak vraagt een codewijziging; dat is bewust.
Alternatief: HTML-sjablonen met placeholders: afgewezen (vraagt HTML-sanering van sjablonen en geeft geen themagarantie).

### D4: Toegang volgt de bestaande regels

- Bewerken: `ContentAccess.CanManageAsync(user, offeringId)`; anders `/access-denied`.
- Sjablonen: permissie `Content.Manage` (bestaat al) via `RequirePermission`, geen nieuwe permissie.
- Weergave: `IntroductionSection` toont alleen gepubliceerde introducties; wie kan beheren ziet ook het concept met een
  `DcBadge` "Draft" (zelfde patroon als `ContentSectionList`). Zonder zichtbare introductie rendert de sectie niets, en
  het cursusmenu toont het item dan niet.

### D5: Sectie en menu

`OfferingPageSection` kent nu alleen `Component`, `Order`, `Title` en `Icon`; het cursusmenu toont daardoor ook items
voor secties die niets renderen. Dat mag bij de introductie niet (scenario "Geen introductie voor deelnemers"), en of er
een zichtbare introductie is, hangt af van de uitvoering en de gebruiker, dus het kan niet bij de registratie bij het
opstarten worden bepaald. Besluit: `OfferingPageSection` krijgt een optionele
`Func<IServiceProvider, ClaimsPrincipal, Guid, Task<bool>>? IsVisible`; `CoursePage` roept die aan en laat sectie en
menu-item weg bij `false`. Dit lost meteen het lege menu-item bij "My groups" en "Scores" op als die de functie
meegeven (buiten de scope; ze mogen dat later). Alternatief: een leeg menu-item accepteren: afgewezen, het scenario
eist het tegendeel.

### D6: Bewerken en kiezen van een sjabloon

`EditIntroductionPage` (`/courses/{offering}/introduction`): keuzelijst "Template", tekstveld "Text", selectievakje
"Published", knop "Preview" en "Save". Kiezen van een sjabloon met een lege tekst vult `StartBody` in; bij een niet-lege
tekst wordt alleen `TemplateId` gewijzigd. Validatie in `ContentService` als `Result` met fouten `Content.Introduction.Invalid`,
`Content.Template.Invalid`, `Content.Template.NotFound`, `Content.Template.DefaultProtected` (titels in
`ContentErrors.resx`).

### D7: Geen API

Net als de rest van Content is dit alleen UI (huisregel 12 speelt niet: geen REST/CSV-tegenhanger). Uitwisseling via
OOAPI of een API volgt als er een behoefte is.

## Risks / Trade-offs

- Markdown is minder vertrouwd dan een WYSIWYG-editor voor docenten → het voorbeeld en de startinhoud van sjablonen
  vangen dat op; een editor kan later komen (D1).
- Uitbreiding van `OfferingPageSection` raakt de Contracts van Academic → de nieuwe parameter is optioneel, bestaande
  registraties blijven werken.
- Zachte verwijdering laat inactieve sjablonen achter → acceptabel; geen opruimtaak in deze change.
