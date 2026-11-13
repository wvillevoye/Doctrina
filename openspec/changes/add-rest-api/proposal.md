# Voorstel

## Why

Sinds `add-academic-core` komt onderwijsdata alleen via het uploaden van een CSV-bestand op een beheerscherm in
Doctrina. Een SIS of koppelplatform wil records automatisch aanleveren en bijwerken, en wil kunnen lezen wat er in
Doctrina staat (bijvoorbeeld welke groepen er zijn). Dat vraagt een REST-API met dezelfde validatie en upsert als de
CSV-import (huisregel 12). De systeemclient `doctrina-sis-sync` en de scopes `doctrina.persons.*`,
`doctrina.courses.*` bestaan al in het claimcontract, maar er is nog niets om ze tegen te gebruiken.

Deze change is de eerste helft van rij 5 van de roadmap (`docs/Doctrina-voorstel.md` §8, `add-api-and-import`; inhoud
in §6.6 A en D). Op keuze van de gebruiker is die rij gesplitst: deze change levert de REST-endpoints; de generieke
asynchrone import (§6.6 B, C en E: zip, manifest, dry-run, Hangfire, opgeslagen rapporten, `fullSync`, bulk via
staging en `MERGE`) volgt als `add-bulk-import`.

## What Changes

- **REST-endpoints per Academic-type** onder `/api/v1` met OOAPI-naamgeving: `organizations`, `academic-sessions`,
  `courses`, `course-offerings`, `persons`, `associations`. Per type:
  - `GET /{type}` met paginering (`pageNumber`, `pageSize`), filters (`source`, `status`) en `modifiedSince` (delta);
  - `GET /{type}/{id}` op het Doctrina-id;
  - `POST /{type}`: nieuw record, 409 als `ExternalId` + `Source` al bestaat;
  - `PUT /{type}/by-external-id/{externalId}`: upsert met de sleutel van het bronsysteem;
  - `PATCH /{type}/{id}`: alleen de meegegeven velden wijzigen;
  - `DELETE /{type}/{id}`: zachte verwijdering (status `toBeDeleted`);
  - `POST /{type}/batch`: tot 500 records in één aanroep, met een resultaat per record.
- **Eén representatie per type**: dezelfde OOAPI-veldnamen als de CSV-kolommen, met verwijzingen als `ExternalId`.
  Alle schrijfroutes gaan door dezelfde validatie en upsert als de CSV-import.
- **Bron per systeem**: wat een systeem schrijft, krijgt de `Source` die de configuratie aan zijn client koppelt
  (standaard `SIS`). Zo kan een systeem nooit records van een andere bron overschrijven.
- **Groepen alleen-lezen**: `GET /groups?offeringId=…` en `GET /groups/{id}` met de leden (persoon-`ExternalId`).
  Groepen zijn LMS-data (§4.4), dus systemen schrijven ze niet.
- **Permissies en scopes**: nieuwe permissies `Academic.Write` en `Groups.Read`. Lezen vraagt `Academic.Read` of
  `Groups.Read`, schrijven `Academic.Write`. De configuratie van Development koppelt `doctrina.persons.read` en
  `doctrina.courses.read` aan de leespermissies, en `doctrina.persons.write` en `doctrina.courses.write` aan
  `Academic.Write`.
- **Rate limiting** op de batch-endpoints per client.

## Buiten de scope

- De asynchrone import: zip met manifest, dry-run, Hangfire-jobs, opgeslagen rapporten, `fullSync`, bulk via
  `SqlBulkCopy`/`MERGE`, `Idempotency-Key` en het importscherm uit §6.6 E. Dat wordt `add-bulk-import`.
- Groepen schrijven via de API.
- OOAPI v5-conforme endpoints (`/ooapi/…`, OOAPI-envelope en -filters): `add-ooapi-interop`. Deze API volgt de
  naamgeving van OOAPI, maar is de eigen API van Doctrina.
- Onderscheid tussen persons- en courses-scopes in de permissies. Beide schrijfscopes geven `Academic.Write`; fijner
  kan later zonder de endpoints te wijzigen.
- ETags en optimistische concurrency.

## Capabilities

### New Capabilities
- `academic-api`: REST-endpoints voor de zes Academic-typen (lezen, aanmaken, upsert, gedeeltelijk bijwerken,
  verwijderen, batch), met bron per client.
- `groups-api`: groepen en hun leden lezen via de API.

### Modified Capabilities
<!-- geen: de endpoints volgen de bestaande api-conventions (ProblemDetails, OpenAPI, standaard beveiligd) en
access-control (permissies via de rolkoppeling) zonder ze te wijzigen. -->

## Impact

- `Doctrina.Modules.Academic`: endpoints in `MapEndpoints`, een omzetting van entiteit naar representatie per type
  (voor GET en PATCH), permissie `Academic.Write`. De CSV-import blijft ongewijzigd en gebruikt dezelfde upsert.
- `Doctrina.Modules.Groups`: lees-endpoints en permissie `Groups.Read`.
- `src/Doctrina.Host`: rate limiting voor de batch-endpoints; rolkoppeling van de systeemscopes en de bronkoppeling in
  `appsettings.Development.json`.
- Documentatie: `docs/ontwikkelen/starten.md` (de API proberen met `doctrina-sis-sync`), `docs/doctrina-integratie.md`
  (scopes en bron).
- Geen databasewijzigingen.
