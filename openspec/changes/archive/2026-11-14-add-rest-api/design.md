# Design

## Context

Zie `proposal.md` voor de aanleiding. Waar deze change op voortbouwt:

- `AcademicType` per type (sinds `add-academic-core`): de lijst velden (OOAPI-namen, gelijk aan de CSV-kolommen), de
  validatie en een upsert op `ExternalId` + `Source`, met uitkomst `Created`/`Updated`/`Unchanged`. De invoer is een
  woordenboek veldnaam → tekst (design D3 van `add-academic-core`), juist zodat REST erop kan aansluiten.
- `/api/v1` is standaard beveiligd met de policy `ApiCaller` (bearer-token). `RequirePermission` zet een permissie op
  een endpoint; `Result.ToHttpResult()` maakt ProblemDetails met `code`. De fouttitels komen uit `.resx` per module.
- Systeemtokens dragen de client in `azp` en hun scopes in `roles`; `Authorization:RolePermissions` koppelt scopes aan
  permissies.
- Groups heeft `GroupService.SetsAsync` en `IAcademicDirectory` (uitvoering en leden met `ExternalId`).

## Goals / Non-Goals

**Goals:**
- Eén set generieke endpoints die voor alle zes typen werkt, op basis van `AcademicType`; geen code per type behalve
  de omzetting van entiteit naar velden.
- REST en CSV laten exact dezelfde validatie en upsert gebruiken (huisregel 12).

**Non-Goals:**
- Een OpenAPI-schema per type (zie Risks): de endpoints zijn gedocumenteerd, de body als generiek object.
- Optimistische concurrency (ETag/If-Match).

## Decisions

### D1: Rij 5 van de roadmap wordt gesplitst

Op keuze van de gebruiker levert deze change §6.6 A (REST) en D (scopes, rate limiting op batch); §6.6 B, C en E (zip,
manifest, dry-run, Hangfire, opgeslagen rapporten, `fullSync`, bulk) worden `add-bulk-import`. Afwijking van §8:
`add-api-and-import` bestaat niet als één change. Reden: REST is los bruikbaar voor koppelpartners, terwijl de bulkimport
een nieuwe dependency (Hangfire) en een eigen datamodel voor jobs en rapporten vraagt.

### D2: Generieke endpoints per `AcademicType`

`AcademicModule.MapEndpoints` registreert voor elk `AcademicType` in `AcademicTypes.All` dezelfde zeven endpoints onder
`/api/v1/{type.Name}`. Elk type krijgt er twee dingen bij:

- `Query(db)`: de `IQueryable` van het type met de navigaties die nodig zijn voor de verwijzingen (bv. bij een
  uitvoering de cursus en de periode);
- `ToFields(entity)`: de omgekeerde richting van `Apply`, dus entiteit → woordenboek met dezelfde veldnamen, met
  verwijzingen als `ExternalId`, datums als `yyyy-MM-dd`, getallen invariant en enums als hun OOAPI-waarde.

De representatie is `{ id, <sleutelveld>, …velden, source, status, dateLastModified }`, met `status` als `active` of
`toBeDeleted` en `dateLastModified` in ISO 8601 UTC.

Alternatief: per type een DTO, mapping en eigen endpoints. Afgewezen: zes keer dezelfde code, en twee plekken met
veldnamen die uit de pas kunnen lopen. Nu bepaalt `AcademicType.Fields` zowel de CSV-kolommen als de JSON-velden.

### D3: JSON → velden

De body is een JSON-object. Strings blijven strings; getallen worden invariant tekst; `true`/`false` worden `"true"`/
`"false"`; `null` betekent "leeg". Een array of object als waarde geeft een validatiefout bij dat veld
(`Academic.<Type>.Invalid`). Onbekende velden worden genegeerd, net als onbekende CSV-kolommen. Zo komt de body via
precies dezelfde `FieldReader` binnen als een CSV-regel.

### D4: POST, PUT en PATCH op dezelfde upsert

- **PUT** `by-external-id/{externalId}`: het sleutelveld wordt `{externalId}`. Staat er een ander sleutelveld in de body,
  dan volgt 400 met `Academic.Api.KeyMismatch`. Daarna de upsert met de bron van de client: `Created` geeft 201,
  `Updated` en `Unchanged` geven 200.
- **POST**: bestaat het record al voor (`ExternalId`, bron), dan volgt 409 met `Academic.<Type>.DuplicateExternalId`;
  anders de upsert (201). Een gelijktijdige aanmaak loopt via de unieke index op `Academic.<Type>.Conflict` (409).
- **PATCH** `{id}`: haalt het record op en controleert de bron (D5). De huidige velden komen uit `ToFields`; de velden uit
  de body vervangen die (een veld met `null` wordt leeggemaakt, een ontbrekend veld blijft). Een ander sleutelveld geeft
  400 met `Academic.Api.KeyMismatch`. Daarna de upsert op het samengevoegde woordenboek; de status blijft wat het was,
  tenzij `status` in de body staat.

Na elke schrijfactie geeft het endpoint de representatie terug, opnieuw gelezen via `Query`.

### D5: De bron komt van de client, niet uit de body

`ApiSource.For(user)` geeft `Api:Sources:{azp}` uit de configuratie, en anders `SIS`. Een `source` in de body wordt
genegeerd. PATCH en DELETE op een record van een andere bron geven 403 met `Academic.Record.OtherSource`. POST en PUT
schrijven per definitie in de eigen bron, dus die kunnen geen record van een ander raken.

Alternatief: de bron als veld in de body of als header. Afgewezen: dan kan elk systeem met schrijfrechten de records van
een ander systeem overschrijven.

### D6: DELETE en lijsten

- **DELETE** `{id}` zet `Status` op `ToBeDeleted` en slaat op; de interceptor zet `DateLastModified`, zodat de wijziging
  in een delta verschijnt. Een tweede DELETE geeft opnieuw 204.
- **Lijst**: `Query(db)` met de filters `source`, `status` (`active`/`toBeDeleted`) en `modifiedSince` (op of na). De
  volgorde is `DateLastModified`, daarna `Id`. `pageSize` is standaard 100 en hoogstens 500; `pageNumber` begint bij 1.
  Het antwoord heeft de vorm van OOAPI-pagina's (`items`, `pageNumber`, `pageSize`, `totalPages`, `hasPreviousPage`,
  `hasNextPage`). Een ongeldige waarde (bv. `pageSize=0`) geeft 400 met `Academic.Api.InvalidQuery`.

### D7: Batch en rate limiting

`POST /{type}/batch` neemt een JSON-array. Bij meer dan 500 elementen volgt 400 (`Academic.Api.BatchTooLarge`)
zonder iets op te slaan. Anders wordt elk element als PUT-upsert verwerkt; het sleutelveld komt uit het element zelf.
Het antwoord is per element `{ index, <sleutel>, result, code?, errors? }`. Na een fout element wordt de change tracker
geleegd, zodat het volgende element schoon begint.

Rate limiting gebruikt de ingebouwde `RateLimiter` van ASP.NET Core: een fixed window van één minuut per `azp`, met
limiet `Api:BatchesPerMinute` (standaard 60). Infrastructure registreert de policy `api-batch`; de module zet
`.RequireRateLimiting("api-batch")` op de batch-endpoints. Bij de limiet volgt 429 als ProblemDetails.

### D8: Permissies

Academic krijgt `Academic.Write`, Groups krijgt `Groups.Read`. Lezen vraagt `Academic.Read` (dezelfde permissie als de
beheerschermen), schrijven `Academic.Write`. De rolkoppeling in `appsettings.Development.json` koppelt de bestaande
Keycloak-scopes; `admin` heeft al `*`. Persons- en courses-scopes geven hier dezelfde permissies (zie proposal).

### D9: Groups-API

`GET /api/v1/groups?offeringId={Guid}` gebruikt `GroupService.SetsAsync` en `IAcademicDirectory`. De `offeringId` in
het antwoord is de `ExternalId` uit `FindOfferingAsync`, en de leden worden van `PersonId` naar `ExternalId` vertaald via
`MembersAsync`. Leden die niet meer actief zijn ingeschreven, vallen daarbij weg, net als in de UI (design D8 van
`add-groups`). `GET /api/v1/groups/{id}` geeft 404 (`Groups.Group.NotFound`) voor een onbekende of te verwijderen groep
of set. Zonder `offeringId` volgt 400 (`Groups.Api.OfferingRequired`); een onbekende uitvoering geeft 404.

## Risks / Trade-offs

- [OpenAPI toont de body als generiek object, niet per type] → de veldnamen staan in de beschrijving van elk endpoint,
  gegenereerd uit `AcademicType.Fields` (met verplichte velden gemarkeerd). Een schema per type kan later met een
  OpenAPI-transformer uit dezelfde lijst. ponytail: in de code gemarkeerd.
- [Een batch van 500 met één `SaveChanges` per record is traag] → in de orde van seconden op LocalDB; echte bulk komt
  met `add-bulk-import`. De limiet van 500 en de rate limiting houden het binnen de perken.
- [`ToFields` en `Apply` raken uit de pas] → een test per type doet ToFields → upsert en verwacht `Unchanged`. Zo is
  gegarandeerd dat lezen en terugschrijven niets verandert.
- [Groepsleden die niet meer zijn ingeschreven, ontbreken in de API] → zelfde gedrag als de UI; opruimen hoort bij een
  latere change.

## Migration Plan

Geen databasewijzigingen. Nieuwe permissies en rolkoppeling in `appsettings.Development.json`; een productie-installatie
koppelt de scopes van haar eigen systemen. Archiveren pas na `add-academic-core` en `add-groups`.
