# Design

## Context

Zie `proposal.md` voor aanleiding en scope. Waar deze change op voortbouwt:

- **Academic** (`add-academic-core`, `add-rest-api`) bewaart `Organization`, `AcademicSession`, `Course`,
  `CourseOffering`, `Person` en `Association` met OOAPI-namen en de vaste velden (`Id`, `ExternalId`, `Source`,
  `Status`, `DateLastModified`). `AcademicEndpoints` bedient `/api/v1/{type}` met paginering, `modifiedSince` en
  rate limits; de queries daarvoor (`AcademicQueries`) zijn intern.
- **Modulesysteem**: `IModule.MapEndpoints` krijgt alleen de `/api/v1`-groep (`ApiCaller`-policy: bearer-only). Een
  module kan dus geen ander prefix bedienen zonder dat de host dat toelaat. `Doctrina.Modules.Academic.Contracts` bevat
  nu alleen `IAcademicDirectory` (offerings, leden, huidige persoon), geen lijsten.
- **Autorisatie**: scopes in de `roles`-claim worden via `Authorization:RolePermissions` aan permissies gekoppeld
  (Development: `doctrina.persons.read` → `Academic.Read`, …).
- Rol-enums van Academic: `Student`, `Lecturer`, `TeachingAssistant`, `Coordinator`; organisatietypen en periodetypen
  volgen al OOAPI.

Keuze van de gebruiker vóór dit ontwerp (2026-11-16): alleen **aanbieden**; consumeren later. Pad: apart
`/ooapi/v5`.

## Goals / Non-Goals

**Goals:**
- Een OOAPI v5-consument kan organisaties, perioden, cursussen, uitvoeringen, personen en inschrijvingen lezen met de
  paginering en antwoordvorm van de standaard.
- Geen tweede waarheid: de module toont wat Academic bewaart, zonder eigen opslag of eigen validatie.

**Non-Goals:**
- Consumeren van OOAPI, schrijven via OOAPI, resources die Doctrina niet heeft, een eigen OpenAPI-document, delta's
  (`modifiedSince` is geen onderdeel van de standaard; wie delta's wil gebruikt `/api/v1`).

## Decisions

### D1: Eigen module `Ooapi` zonder eigen schema

Een module `Ooapi` in `src/Modules/Interop/Doctrina.Modules.Interop.OOAPI` (naam uit voorstel §3, modulenaam `Ooapi`,
permissie `Ooapi.Read`), met `DependsOn = ["Academic"]`. Ze bewaart niets en heeft dus **geen schema, DbContext of
migraties**. Dat wijkt af van huisregel 3 ("elke module heeft een eigen schema"): die regel gaat over opslag, en een
lege DbContext zou alleen schijnstructuur zijn. Controleer in de architectuurtests of ze dit toelaten; zo niet, dan
past de test aan zodat een module zonder entiteiten is toegestaan (taak 1.2).

*Alternatief:* de endpoints in Academic zelf. Verworpen: het voorstel scheidt interoperabiliteit in `Interop.*`, en
Groups kan later ook OOAPI-resources (`groups`) aanbieden zonder dat Academic daarvan weet.

### D2: Alleen lezen; schrijven blijft op `/api/v1`

Het voorstel (§6.7) noemt ook schrijven van associations. Dat gebeurt nu al via `/api/v1/associations` met dezelfde
validator en upsert als de CSV-import (huisregel 12). Een tweede schrijfroute zou de bronregels (`Api:Sources`,
`Academic.Record.OtherSource`) moeten dupliceren of hergebruiken zonder dat een consument erom vraagt. Daarom nu
read-only; schrijven via OOAPI volgt als een koppelpartner het nodig heeft (afwijking van §6.7, vast te leggen in het
voorstel bij het afronden).

### D3: Tweede routegroep via een optionele `IModule`-methode

De host maakt naast `/api/v1` een groep `/ooapi/v5` met dezelfde `ApiCaller`-policy (bearer-only, geen redirects) en
roept daarop `IModule.MapOoapiEndpoints(IEndpointRouteBuilder)` aan. Die methode is een **default interface-lid zonder
werking**, zodat bestaande modules niet wijzigen. Een schoner algemeen mechanisme (modules melden zelf een prefix) is
overbodig zolang er één standaard-API is.

*Alternatief:* de module mapt zelf op de root van de app. Verworpen: de module zou dan de beveiliging van de host
omzeilen, precies wat het huidige ontwerp van `MapEndpoints` voorkomt.

### D4: Leescontract in `Academic.Contracts`

Nieuw: `IAcademicExchangeReader` met per resource een gepagineerde lijst en een opvraag op id, die platte, door Ooapi te
mappen records teruggeeft (alleen actieve, stabiele volgorde, filter op `ExternalId`). De lijsten die de standaard
kent: organisaties, perioden, cursussen, personen, uitvoeringen (van een cursus, een periode of een organisatie) en
inschrijvingen (van een uitvoering, met rolfilter, of van een persoon). Een persoon komt met zijn rollen mee, zodat
`affiliations` en `activeEnrollment` zonder extra query te bepalen zijn. Academic implementeert het met zijn bestaande
queries. Zo blijft Ooapi onafhankelijk van `AcademicDbContext` en de entiteiten (huisregel 2). De mapping naar
OOAPI-JSON (namen, `primaryCode`, rollen) staat in Ooapi, niet in Academic.

### D5: Veldmapping

Uitgangspunt is de officiële OpenAPI van OOAPI v5 (release 5.0, `v5/ooapi.yaml`), gelezen bij taak 1.1.

| Doctrina | OOAPI v5 |
|---|---|
| `Id` (Guid, met streepjes) | resource-id (`courseId`, `personId`, `offeringId`, `associationId`, …) |
| `ExternalId` | `primaryCode` `{ codeType: "identifier", code }` |
| `Name` | `name`: `[{ language: Ooapi:Language, value }]` |
| `CourseOffering` | `offering` met `offeringType` "course", `course`, `academicSession`, `startDate`, `endDate` |
| `Association` | `association` met `associationType` "courseOfferingAssociation", `role`, `state` "associated", `person`, `offering` |
| `AssociationRole` `Student` / `Lecturer` / `TeachingAssistant` / `Coordinator` | `student` / `lecturer` / `teaching assistant` / `coordinator` |
| `OrganizationType` `Root` / `Institution` / `Faculty` / `Department` / `Branch` / `Academy` / `School` | `root` / `institute` / `faculty` / `department` / `branch` / `academy` / `school` |
| `AcademicSessionType` | `academic year` / `semester` / `trimester` / `quarter` / `testing period` / `period` |
| `Person` | `givenName`, `surnamePrefix`, `surname`, `displayName`, `mail` |
| `Source`, `Status`, `DateLastModified` | niet getoond (intern); alleen actieve records |

**Verplichte velden zonder bron.** OOAPI verplicht een aantal velden die Doctrina niet bewaart. Besluit (2026-11-16):
afleiden waar het kan, anders een instelling met een vaste standaardwaarde, en niets verzinnen waar geen van beide kan.

| Veld | Waarde |
|---|---|
| association `state` | altijd "associated" (alleen actieve records worden getoond) |
| person `affiliations` | uit de rollen: `student` voor een studentinschrijving, `employee` voor docent, teaching assistant of coördinator; `guest` zonder inschrijving |
| person `activeEnrollment` | waar bij een actieve inschrijving als student |
| person `mail` | alleen als Doctrina een adres heeft (anders weggelaten) |
| course `abbreviation` / `description` | `Abbreviation` of anders de code; `Description` of anders de naam |
| course en offering `teachingLanguage` | instelling `Ooapi:TeachingLanguage` (standaard `nld`) |
| course `level` | instelling `Ooapi:Level` (standaard `undefined`, een geldige waarde) |
| offering `description` | de naam van de uitvoering |
| offering `resultExpected` | instelling `Ooapi:ResultExpected` (standaard onwaar) |
| offering `startDate` / `endDate` | die van de uitvoering, anders van de periode |
| organization `shortName` | `ShortName` of anders de naam |

De waarden uit de instellingen gelden voor de hele installatie; ze zijn een keuze van de beheerder en geen bewaarde data.

Namen zijn in OOAPI taalgetypeerd (`[{ language, value }]`); Doctrina kent geen taal per veld. De module gebruikt één
instelling `Ooapi:Language` (standaard `nl`) voor alle teksten.

### D6: Paginering en filters

`pageNumber` (vanaf 1) en `pageSize`, met de waarden van de standaard: 10, 20, 50, 100 of 250, standaard 10. Antwoord met
`pageSize`, `pageNumber`, `hasPreviousPage`, `hasNextPage`, `totalPages`, `items`. Volgorde: stabiel op code/naam en
daarna id. Filters: `primaryCode` op de lijsten van organisaties, perioden, cursussen en personen, en `role` op de
inschrijvingen van een uitvoering; `search`, `sort`, `expand` en andere parameters worden genegeerd. Fouten
(400/401/403/404) zijn `application/problem+json` met `status` en `title`, zoals de standaard voorschrijft.

**Paden.** De standaard kent geen top-level `/offerings` of `/associations`. We bieden precies de paden van de standaard:
`/`, `/organizations`, `/organizations/{id}`, `/organizations/{id}/offerings`, `/academic-sessions`,
`/academic-sessions/{id}`, `/academic-sessions/{id}/offerings`, `/courses`, `/courses/{id}`, `/courses/{id}/offerings`,
`/persons`, `/persons/{id}`, `/persons/{id}/associations`, `/offerings/{id}`, `/offerings/{id}/associations` en
`/associations/{id}`. Niet gebouwd: de paden voor resources die Doctrina niet heeft en `/persons/me`,
`/associations/external/me` (schrijven/eigen gegevens).

### D7: Autorisatie

Eén permissie `Ooapi.Read`, gekoppeld via `Authorization:RolePermissions`; in Development door
`doctrina.persons.read` en `doctrina.courses.read`. Persoonsgegevens (naam, mail) staan in `persons` en
`associations`; wie die niet mag zien krijgt de scope niet. Geen aparte permissie per resource (YAGNI).

### D8: Logs zonder persoonsgegevens

De module logt niets uit antwoorden of queryparameters (ook niet `primaryCode`, dat een persoonsnummer kan zijn),
alleen resource, status en duur. Dit wordt getest met `CapturingLoggerProvider` op `Trace`.

### D9: Servicegegevens op `GET /`

OOAPI verplicht een servicebeschrijving op de basis: `contactEmail`, `specification` en `documentation`. Ze komen uit de
instellingen `Ooapi:ContactEmail`, `Ooapi:Specification` (standaard de OOAPI-specificatie) en `Ooapi:Documentation`; het
contactadres is in productie een keuze van de beheerder (de standaardwaarde in `appsettings.json` is een voorbeeld).

## Risks / Trade-offs

- **Details van de standaard**: paden, veldnamen en enum-waarden zijn gelezen uit de officiële OpenAPI van release 5.0 (taak 1.1);
  een latere release van de standaard kan afwijken.
- **Afgeleide en ingestelde velden**: een deel van de verplichte velden (D5) is geen bewaarde data. Een consument die
  `level` of `teachingLanguage` per cursus wil, krijgt de waarde van de installatie.
- **Taal**: één vaste taal per installatie kan voor meertalige cursusnamen te weinig zijn. Acceptabel nu; Doctrina bewaart
  geen vertalingen.
- **Geen delta's** in OOAPI: consumenten die incrementeel willen synchroniseren moeten `/api/v1?modifiedSince=` gebruiken.
- **Twee routes naar dezelfde data**: Ooapi en `/api/v1` kunnen uiteenlopen als Academic een veld toevoegt. Beide lezen
  via de bestaande queries, dus filters en zichtbaarheid (alleen actief) blijven gelijk.

## Open vragen

- Welke OOAPI-profielen gebruiken de koppelpartners (SURF/Edu-V) echt? Dit bepaalt of we meer resources of velden nodig
  hebben (§10 van het voorstel); nu bouwen we de zes resources die Academic heeft.
