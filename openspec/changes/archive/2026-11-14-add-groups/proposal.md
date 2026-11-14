# Voorstel

## Why

Docenten werken in bijna elke cursus met groepen: werkgroepen, projectteams, practicumgroepen. Doctrina kent sinds
`add-academic-core` uitvoeringen en inschrijvingen, maar nog geen groepen. Groepen zijn ook de basis voor latere
onderdelen: content per groep, LTI-diensten die deelnemers per groep doorgeven (NRPS) en het indelen bij opdrachten.
Deze change is rij 4 van de roadmap (`docs/Doctrina-voorstel.md` §8, `add-groups`; inhoud in §6.2). Groepen zijn
LMS-data: Doctrina is er de bron van, niet het SIS (§4.4).

Studenten hebben nog geen pagina per uitvoering; "My courses" is alleen een lijst. Om hun groepen te kunnen zien, komt
die pagina er nu, als plek waar ook latere modules (content, LTI) hun deel kunnen tonen.

## What Changes

- **Nieuwe module `Groups`** (schema `groups`, `DependsOn` Academic) met **GroupSets** per uitvoering (naam, zichtbaar
  voor studenten ja/nee, optionele maximale groepsgrootte) en **groepen** daarin. Een student zit per GroupSet in
  hoogstens één groep; alleen studenten met een actieve inschrijving in de uitvoering kunnen lid worden.
- **Groepen beheren** op `/courses/{offering}/groups`: GroupSets en groepen aanmaken, hernoemen en verwijderen, en
  studenten **handmatig** indelen of **willekeurig verdelen** (over N groepen, of in groepen van X) over wie nog niet
  is ingedeeld. Toegang voor de docenten van de uitvoering (actieve inschrijving als `lecturer` of `coordinator`) en
  voor gebruikers met de permissie `Groups.Manage`.
- **Event `GroupMembershipChanged`** in `Doctrina.Modules.Groups.Contracts` bij elke toevoeging of verwijdering van een
  lid (§6.2), voor latere modules zoals LTI-NRPS.
- **Pagina per uitvoering** `/courses/{offering}` in de module Academic, voor iedereen met een actieve inschrijving in
  die uitvoering (en voor `Academic.Read`): cursus, uitvoering, periode en de eigen rol. Andere modules kunnen er een
  sectie aan toevoegen via `Doctrina.Modules.Academic.Contracts`; Groups toont daar "My groups" (de eigen groepen met
  leden, alleen uit GroupSets die zichtbaar zijn voor studenten) en voor docenten een link naar het groepsbeheer.
  "My courses" linkt naar deze pagina.
- **Eerste publieke contracten van Academic** (`.Contracts`): een uitvoering opzoeken, de persoon van de ingelogde
  gebruiker, de actieve leden van een uitvoering met hun rol, en de registratie van secties op de uitvoeringspagina.

## Buiten de scope

- Zelf inschrijven in een groep door studenten (met open/dicht-moment): een vervolgchange.
- Groepen importeren of via REST beheren (`add-api-and-import`), en groepen uit het SIS.
- Groepen op cursus- of organisatieniveau; groepen buiten een uitvoering.
- Docenten of teaching assistants als groepslid of als begeleider van een groep.
- Een deelnemerslijst van de hele uitvoering voor studenten.
- Rechten per uitvoering voor andere onderdelen dan groepen (bv. beheerschermen van Academic blijven op `Academic.Read`).

## Capabilities

### New Capabilities
- `groups`: GroupSets en groepen in een uitvoering, handmatig en willekeurig indelen, wie ze mag beheren, zichtbaarheid
  voor studenten en het event `GroupMembershipChanged`.
- `course-page`: de pagina per uitvoering voor deelnemers, met secties die andere modules aanleveren.

### Modified Capabilities
- `my-courses`: nieuwe requirement: elke uitvoering in "My courses" linkt naar haar pagina.

`my-courses` komt uit `add-academic-core`; die change moet dus eerst gearchiveerd zijn.

## Impact

- Nieuwe projecten `src/Modules/Groups/Doctrina.Modules.Groups` en `.Contracts`, testproject
  `tests/Modules/Doctrina.Modules.Groups.Tests`; integratietests in `Doctrina.Host.IntegrationTests`.
- `Doctrina.Modules.Academic.Contracts`: eerste interfaces en DTO's; `Doctrina.Modules.Academic`: implementatie, pagina
  `/courses/{offering}`, link vanuit "My courses".
- `src/Doctrina.Host`: module registreren in `KnownModules`, aanzetten in `appsettings.Development.json`.
- Documentatie: `docs/ontwikkelen/starten.md` (groepen proberen), `src/Modules/README.md`.
- Database: nieuw schema `groups`; geen wijziging in het schema `academic`.
