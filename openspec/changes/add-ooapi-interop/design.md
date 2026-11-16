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

Nieuw: `IAcademicExchangeReader` met per type een gepagineerde lijst en een opvraag op id, die platte, door Ooapi
te mappen records teruggeeft (`Result<T>`/pagina's, status "actief" alleen). Academic implementeert het met zijn
bestaande queries. Zo blijft Ooapi onafhankelijk van `AcademicDbContext` en de entiteiten (huisregel 2). De mapping naar
OOAPI-JSON (namen, `primaryCode`, rollen) staat in Ooapi, niet in Academic.

### D5: Veldmapping

| Doctrina | OOAPI v5 |
|---|---|
| `Id` (Guid, met streepjes) | resource-id (`courseId`, `personId`, `offeringId`, …) |
| `ExternalId` | `primaryCode` `{ codeType: "identifier", code }` |
| `CourseOffering` | `offering` met `offeringType` "course", `course`, `academicSession`, `startDate`, `endDate` |
| `Association` | `association` met `role`, `person`, `offering` |
| `AssociationRole` `Student` / `Lecturer` / `TeachingAssistant` / `Coordinator` | `student` / `teacher` / `assistant` / `other` |
| `Person` | `givenName`, `surnamePrefix` (als `surnamePrefix`), `surname`, `displayName`, `mail` |
| `Source`, `Status`, `DateLastModified` | niet getoond (intern); alleen actieve records |

Namen zijn in OOAPI taalgetypeerd (`[{ language, value }]`); Doctrina kent geen taal per veld. De module gebruikt één
instelling `Ooapi:Language` (standaard `nl`) voor alle teksten. De exacte veldnamen en enum-waarden (o.a. de
rolwaarden) worden bij taak 1.1 vastgesteld aan de officiële OpenAPI van v5; deze tabel is het uitgangspunt.

### D6: Paginering en filters

`pageNumber` (vanaf 1) en `pageSize`; standaard 20, hoogstens 100 (aanname, bij taak 1.1 te controleren aan de
standaard en eventueel aan te passen). Antwoord met `pageSize`, `pageNumber`, `hasPreviousPage`, `hasNextPage`,
`totalPages`, `items`. Volgorde: stabiel op naam/code en daarna id. Ondersteunde filters: `primaryCode` op alle
lijsten, daarnaast alleen wat de standaard voor die resource voorschrijft en Academic al kan filteren
(bv. `offerings?course=…`); onbekende parameters worden genegeerd. Fouten (400/401/403/404) als ProblemDetails
(`application/problem+json`) zoals `/api/v1`; als de standaard een ander foutformaat voorschrijft, volgt taak 1.1 dat.

### D7: Autorisatie

Eén permissie `Ooapi.Read`, gekoppeld via `Authorization:RolePermissions`; in Development door
`doctrina.persons.read` en `doctrina.courses.read`. Persoonsgegevens (naam, mail) staan in `persons` en
`associations`; wie die niet mag zien krijgt de scope niet. Geen aparte permissie per resource (YAGNI).

### D8: Logs zonder persoonsgegevens

De module logt niets uit antwoorden of queryparameters (ook niet `primaryCode`, dat een persoonsnummer kan zijn),
alleen resource, status en duur. Dit wordt getest met `CapturingLoggerProvider` op `Trace`.

## Risks / Trade-offs

- **Onbekende details van de standaard**: paden, veldnamen en enum-waarden zijn hier uit de kennis van v5 afgeleid en
  niet aan het officiële OpenAPI-bestand getoetst. Taak 1.1 doet dat vóór de bouw; afwijkingen verwerken we in de
  specs (`openspec-update-change`) voordat er code is.
- **Taal**: één vaste taal per installatie kan voor meertalige cursusnamen te weinig zijn. Acceptabel nu; Doctrina bewaart
  geen vertalingen.
- **Geen delta's** in OOAPI: consumenten die incrementeel willen synchroniseren moeten `/api/v1?modifiedSince=` gebruiken.
- **Twee routes naar dezelfde data**: Ooapi en `/api/v1` kunnen uiteenlopen als Academic een veld toevoegt. Beide lezen
  via de bestaande queries, dus filters en zichtbaarheid (alleen actief) blijven gelijk.

## Open vragen

- Welke OOAPI-profielen gebruiken de koppelpartners (SURF/Edu-V) echt? Dit bepaalt of we meer resources of velden nodig
  hebben (§10 van het voorstel); nu bouwen we de zes resources die Academic heeft.
