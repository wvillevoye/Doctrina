# Spec-delta

## Purpose
Systemen (SIS, koppelplatform) onderwijsdata in Doctrina laten lezen en aanleveren via REST, met dezelfde validatie en
upsert als de CSV-import.

## ADDED Requirements

### Requirement: Elk Academic-type heeft REST-endpoints onder /api/v1
De typen `organizations`, `academic-sessions`, `courses`, `course-offerings`, `persons` en `associations` SHALL elk
endpoints hebben onder `/api/v1/{type}`: een lijst, één record op Doctrina-id, aanmaken, upsert op ExternalId,
gedeeltelijk bijwerken, zacht verwijderen en een batch. Een onbekend id SHALL 404 geven met `code`
`Academic.<Type>.NotFound`.

#### Scenario: Onbekend id
- **WHEN** een systeem `GET /api/v1/persons/{id}` aanroept met een id dat niet bestaat
- **THEN** is de status 404 met een ProblemDetails-body met `code` `Academic.Person.NotFound`

### Requirement: Records gebruiken de veldnamen van de import
Een record SHALL worden weergegeven met dezelfde OOAPI-veldnamen als de CSV-kolommen, met `id` (Doctrina-id), de sleutel
van het bronsysteem onder het sleutelveld van het type (bv. `personId`), verwijzingen als ExternalId, en `source`,
`status` en `dateLastModified`.

#### Scenario: Persoon opvragen op id
- **GIVEN** een persoon "I6000001" uit "SIS"
- **WHEN** een systeem met leesrechten `GET /api/v1/persons/{id}` van die persoon aanroept
- **THEN** is de status 200 met `id`, `personId` "I6000001", `givenName`, `surname`, `source` "SIS", `status` "active" en `dateLastModified`

### Requirement: Lijsten zijn gepagineerd en ondersteunen delta's
`GET /api/v1/{type}` SHALL gepagineerd zijn met `pageNumber` (vanaf 1) en `pageSize` (standaard 100, hoogstens 500),
en SHALL `items`, `pageNumber`, `pageSize`, `totalPages`, `hasPreviousPage` en `hasNextPage` teruggeven. De lijst SHALL
kunnen filteren op `source` en `status`, en met `modifiedSince` (ISO 8601) alleen records geven die op of na dat moment
zijn gewijzigd. De volgorde SHALL stabiel zijn: op `dateLastModified`, daarna op `id`.

#### Scenario: Delta sinds een moment
- **GIVEN** cursus "BIO101" gewijzigd op 2026-11-12T10:00:00Z en cursus "WIS201" gewijzigd op 2026-11-13T09:00:00Z
- **WHEN** een systeem `GET /api/v1/courses?modifiedSince=2026-11-13T00:00:00Z` aanroept
- **THEN** bevat `items` alleen "WIS201"

#### Scenario: Paginering
- **GIVEN** 120 personen
- **WHEN** een systeem `GET /api/v1/persons?pageSize=50&pageNumber=3` aanroept
- **THEN** bevat `items` 20 personen, is `totalPages` 3, `hasPreviousPage` waar en `hasNextPage` onwaar

### Requirement: Systemen schrijven met hun eigen bron
Elk schrijvend endpoint SHALL de `Source` gebruiken die `Api:Sources:{client}` aan de client van het token koppelt
(claim `azp`), en anders `SIS`. Een systeem SHALL alleen records van zijn eigen bron kunnen wijzigen of verwijderen; een
poging op een record van een andere bron SHALL 403 geven met `code` `Academic.Record.OtherSource`.

#### Scenario: Bron uit de configuratie
- **GIVEN** `Api:Sources:roosterpakket` is "Rooster"
- **WHEN** de client "roosterpakket" een cursus aanmaakt
- **THEN** heeft de cursus `source` "Rooster"

#### Scenario: Record van een andere bron
- **GIVEN** een persoon uit "SIS" en een client die als bron "Rooster" schrijft
- **WHEN** die client de persoon met `PATCH` of `DELETE` wijzigt
- **THEN** is de status 403 met `code` `Academic.Record.OtherSource` en is de persoon ongewijzigd

### Requirement: Aanmaken geeft een conflict bij een bestaande sleutel
`POST /api/v1/{type}` SHALL een nieuw record aanmaken en 201 geven met de representatie en een `Location`. Bestaan
`ExternalId` en `Source` al, dan SHALL het 409 geven met `code` `Academic.<Type>.DuplicateExternalId`. De validatie,
verwijzingsregels en foutcodes SHALL die van de CSV-import zijn.

#### Scenario: Aanmaken van een nieuwe persoon
- **WHEN** een systeem `POST /api/v1/persons` doet met `personId` "I6000001", `givenName` "Sanne" en `surname` "Student"
- **THEN** is de status 201 met een `Location` naar `/api/v1/persons/{id}`

#### Scenario: Aanmaken van een bestaande sleutel
- **GIVEN** persoon "I6000001" uit de bron van het systeem
- **WHEN** het systeem `POST /api/v1/persons` doet met `personId` "I6000001"
- **THEN** is de status 409 met `code` `Academic.Person.DuplicateExternalId`

### Requirement: Upsert op de sleutel van het bronsysteem
`PUT /api/v1/{type}/by-external-id/{externalId}` SHALL een upsert zijn met de validatie van de CSV-import: 201 als het
record nieuw is, anders 200. Staat het sleutelveld in de body, dan SHALL het gelijk zijn aan `{externalId}`, anders 400.

#### Scenario: Upsert van een nieuwe persoon
- **WHEN** een systeem `PUT /api/v1/persons/by-external-id/I6000001` aanroept met `givenName` "Sanne" en `surname` "Student"
- **THEN** is de status 201 en bestaat persoon "I6000001" met de bron van dat systeem

#### Scenario: Upsert van een bestaande persoon
- **GIVEN** persoon "I6000001" met `givenName` "Sanne"
- **WHEN** het systeem dezelfde PUT doet met `givenName` "Sanna"
- **THEN** is de status 200 en heet de persoon "Sanna"

#### Scenario: Ongeldige invoer
- **WHEN** een systeem `PUT /api/v1/persons/by-external-id/I6000001` aanroept zonder `surname`
- **THEN** is de status 400 met `code` `Academic.Person.Invalid` en een fout bij het veld `surname`

### Requirement: Gedeeltelijk bijwerken behoudt de overige velden
`PATCH /api/v1/{type}/{id}` SHALL alleen de meegegeven velden wijzigen en de rest behouden, met de validatie van de
CSV-import op het resultaat. Het sleutelveld SHALL NOT te wijzigen zijn.

#### Scenario: Gedeeltelijk bijwerken
- **GIVEN** persoon "I6000001" met `givenName` "Sanne", `surname` "Student" en `mail` "sanne@example.test"
- **WHEN** het systeem `PATCH` doet met alleen `mail` "s.student@example.test"
- **THEN** is de status 200, is `mail` gewijzigd en zijn `givenName` en `surname` gelijk gebleven

### Requirement: Verwijderen is zacht
`DELETE /api/v1/{type}/{id}` SHALL de status van het record op `toBeDeleted` zetten en 204 geven; het record SHALL blijven
bestaan en met `GET` op te vragen zijn.

#### Scenario: Inschrijving verwijderen
- **GIVEN** een actieve inschrijving
- **WHEN** het systeem `DELETE /api/v1/associations/{id}` aanroept
- **THEN** is de status 204 en geeft `GET /api/v1/associations/{id}` de inschrijving met `status` "toBeDeleted"

### Requirement: Een batch verwerkt tot 500 records met een resultaat per record
`POST /api/v1/{type}/batch` SHALL een lijst van hoogstens 500 records als upsert verwerken en per record (in dezelfde
volgorde) de index, de ExternalId en `created`, `updated`, `unchanged` of `failed` met foutcode en meldingen
teruggeven, met status 200. Een fout record SHALL de andere niet tegenhouden. Meer dan 500 records SHALL 400 geven
zonder iets op te slaan.

#### Scenario: Batch met één fout record
- **WHEN** een systeem een batch van drie personen stuurt waarvan de tweede geen `surname` heeft
- **THEN** is de status 200, zijn de eerste en derde "created" en is de tweede "failed" met `code` `Academic.Person.Invalid`

#### Scenario: Te grote batch
- **WHEN** een systeem een batch van 501 records stuurt
- **THEN** is de status 400 en is er niets opgeslagen

### Requirement: Batches zijn per client begrensd
Per client SHALL het aantal batch-aanroepen per minuut begrensd zijn (`Api:BatchesPerMinute`, standaard 60); daarboven
SHALL status 429 volgen.

#### Scenario: Te veel batches
- **GIVEN** `Api:BatchesPerMinute` is 2
- **WHEN** een client binnen een minuut drie batches stuurt
- **THEN** krijgt de derde status 429

### Requirement: Lezen en schrijven vragen aparte permissies
Lees-endpoints SHALL `Academic.Read` vereisen en schrijvende endpoints `Academic.Write`. De configuratie van Development
SHALL `doctrina.persons.read` en `doctrina.courses.read` aan `Academic.Read` en `Groups.Read` koppelen, en
`doctrina.persons.write` en `doctrina.courses.write` aan `Academic.Write`.

#### Scenario: Alleen leesrechten
- **GIVEN** een systeem met alleen `Academic.Read`
- **WHEN** het `PUT /api/v1/persons/by-external-id/I6000001` aanroept
- **THEN** is de status 403 met `code` `Auth.Forbidden`
