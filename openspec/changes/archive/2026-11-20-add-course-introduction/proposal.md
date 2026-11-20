# Proposal: add-course-introduction

## Why

Een cursuspagina begint nu direct met de kaart "Content". Deelnemers missen een plek waar de docent de cursus kort
voorstelt (welkom, leerdoelen, planning), en docenten hebben daarvoor alleen een gewone contentpagina zonder vaste plek of
opmaak. Een introductie bovenaan, in een herkenbare opmaak per cursus, geeft elke cursus dezelfde duidelijke start (zoals
de startpagina van een cursus in Canvas).

## What Changes

- Nieuwe sectie "Introduction" op de cursuspagina, **boven** "Content" (Content-module, `OfferingPageSection` met een
  lagere `Order`). Zichtbaar in het cursusmenu in de zijbalk.
- Wie de content van een uitvoering mag beheren (`ContentAccess.CanManageAsync`) bewerkt de introductie op
  `/courses/{offering}/introduction` met een Markdown-editor met voorbeeld, naar het patroon van de bestaande
  contentpagina's. Er komt bewust **geen** HTML-editor (geen nieuwe pakketten); de tekst wordt gesaneerd weergegeven met
  de bestaande `Markdown.ToSafeHtml`.
- Introductiesjablonen: een beheerder (`Content.Manage`) beheert een lijst sjablonen op `/content/templates`, elk met een
  naam, een vaste opmaak (uit een kleine, in code vastgelegde set) en een startinhoud in Markdown. De opmaak gebruikt
  alleen `--dc-*`-tokens, zodat elke cursus er in het actieve thema passend uitziet.
- Per uitvoering kiest de docent een sjabloon; bij een lege introductie wordt de startinhoud ingevuld. Wisselen van
  sjabloon verandert de opmaak, niet de geschreven tekst.
- Nieuwe tabellen in het schema `content` met een eigen migratie; menu-item "Intro templates" voor beheerders.

Buiten de scope:
- Een HTML/WYSIWYG-editor, vrije CSS of upload van afbeeldingen in de introductie.
- Kopiëren van de introductie bij het kopiëren van een uitvoering (`add-course-templates`); dat volgt in een eigen change.
- Een REST-API of OOAPI-uitwisseling voor introducties en sjablonen.
- Vertalingen of versiegeschiedenis van de tekst.

## Capabilities

### New Capabilities
- `course-introduction`: de introductie van een uitvoering (tekst, concept/gepubliceerd, plaats boven de content), het
  bewerken ervan door docenten, en de beheerde sjablonen die opmaak en startinhoud leveren.

### Modified Capabilities

(geen: de plaats op de cursuspagina volgt uit het bestaande mechanisme voor secties van `course-page`)

## Impact

- `src/Modules/Content`: nieuwe entiteiten `IntroductionTemplate` en `CourseIntroduction`, migratie, service,
  componenten `IntroductionSection`, `EditIntroductionPage`, `ManageTemplatesPage`; menu-item en registratie in
  `ContentModule`.
- `src/Doctrina.UI/Icons`: eventueel één nieuw Lucide-icoon voor de sectie.
- Tests in `tests/Modules/...Content...` en `Doctrina.Host.IntegrationTests`; `docs/ontwikkelen/modules.md` krijgt een
  korte alinea (Engels).
- Geen nieuwe packages; geen wijziging van `.Contracts`-projecten.
