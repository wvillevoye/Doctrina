# Voorstel

## Why

Een uitvoering heeft sinds `add-groups` een eigen pagina, maar er staat nog geen lesmateriaal op: docenten kunnen
niets delen en studenten vinden er alleen hun groepen. Content is de kern van een LMS en de basis voor wat volgt:
LTI-items staan in dezelfde structuur (`add-lti-platform`) en cursussjablonen kopiëren haar (`add-course-templates`).
Deze change is rij 7 van de roadmap (`docs/Doctrina-voorstel.md` §8, `add-content`; inhoud in §6.4). Content is
LMS-data: Doctrina is er de bron van, niet het SIS (§4.4).

## What Changes

- **Nieuwe module `Content`** (schema `content`, `DependsOn` Academic) met **secties** per uitvoering en daarin
  **items** van drie soorten: **pagina** (tekst in Markdown), **bestand** (upload) en **link** (webadres).
- **Content beheren** op `/courses/{offering}/content`: secties en items aanmaken, hernoemen en verwijderen, publiceren
  en verbergen, en de volgorde wijzigen met "Move up" en "Move down". Toegang voor de docenten van de uitvoering
  (actieve inschrijving als `lecturer` of `coordinator`) en voor gebruikers met de permissie `Content.Manage`.
- **Pagina's** worden in Markdown geschreven, met een voorbeeldweergave tijdens het bewerken. HTML in de tekst wordt
  niet uitgevoerd.
- **Bestanden** staan in een map op schijf (`Content:Files:Path`), met de gegevens erover in de database; standaard
  hoogstens 100 MB per bestand. Dit beantwoordt de open vraag over bestandsopslag uit §10 van het voorstel.
- **Sectie "Content" op de uitvoeringspagina** (`/courses/{offering}`): deelnemers zien de gepubliceerde secties en
  items, openen pagina's op `/courses/{offering}/content/{item}`, downloaden bestanden en volgen links. Docenten zien
  daar ook de concepten, met het label "Draft", en een knop "Manage content".
- **Publiceren**: nieuwe secties en items zijn een concept. Een item is zichtbaar voor deelnemers als het item én zijn
  sectie gepubliceerd zijn.
- **Permissie `Content.Read`**: gepubliceerde content van elke uitvoering lezen zonder inschrijving.

## Buiten de scope

- LTI-items in de contentstructuur (`add-lti-platform`) en content kopiëren vanuit een sjabloon
  (`add-course-templates`).
- REST-API en import voor content; contracten of events voor andere modules (komen met de change die ze nodig heeft).
- Een WYSIWYG-editor, afbeeldingen uploaden in een pagina en bestanden in de browser bekijken (alleen downloaden).
- Items naar een andere sectie verplaatsen, slepen, geneste secties en inplannen op datum.
- Zichtbaarheid per groep of per rol, voortgang bijhouden en "gelezen"-markeringen.
- Object storage (S3), virusscan en versiebeheer van bestanden; een prullenbak om verwijderde content terug te halen.

## Capabilities

### New Capabilities
- `course-content`: secties met pagina's, bestanden en links in een uitvoering; wie ze mag beheren, publiceren en
  verbergen, de volgorde, en wat deelnemers op de pagina van de uitvoering zien.

### Modified Capabilities

Geen. De sectie komt op de uitvoeringspagina via het bestaande mechanisme van `course-page` (`add-groups`); die
change moet dus eerst gearchiveerd zijn.

## Impact

- Nieuw project `src/Modules/Content/Doctrina.Modules.Content`, testproject
  `tests/Modules/Doctrina.Modules.Content.Tests`; integratietests in `Doctrina.Host.IntegrationTests`.
- Nieuw package **Markdig** (BSD-2-Clause) in `Directory.Packages.props`, voor Markdown naar HTML.
- `Doctrina.UI`: de downloadhulp (`Downloads` en `download.js`) verhuist van de module Academic hierheen, zodat
  Content haar ook gebruikt; de importpagina's van Academic gaan de gedeelde versie gebruiken.
- `Doctrina.TestKit`: de in-memory `IAcademicDirectory` uit de tests van Groups verhuist hierheen.
- `src/Doctrina.Host`: module registreren in `KnownModules`, aanzetten in `appsettings.Development.json`, instellingen
  `Content:Files` (map en maximale grootte); de map met bestanden in `.gitignore`.
- Documentatie: `docs/ontwikkelen/starten.md` (content proberen), `src/Modules/README.md`.
- Database: nieuw schema `content`; geen wijziging in andere schema's.
- Beheer: de map met bestanden moet mee in de back-up, naast de database.
