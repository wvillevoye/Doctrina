## Purpose

Externe systemen die OOAPI v5 spreken kunnen het onderwijskernmodel van Doctrina (organisaties, perioden, cursussen,
uitvoeringen, personen en inschrijvingen) alleen-lezen opvragen, zonder Doctrina-specifieke koppeling.

## ADDED Requirements

### Requirement: Doctrina biedt OOAPI v5-resources aan onder /ooapi/v5
De module Ooapi SHALL, als ze is ingeschakeld, voor elk van `organizations`, `academic-sessions`, `courses`,
`offerings`, `persons` en `associations` een lijst (`GET /ooapi/v5/{resource}`) en één record op id
(`GET /ooapi/v5/{resource}/{id}`) aanbieden, met veldnamen volgens OOAPI v5. Een onbekend of verwijderd id SHALL 404
geven. Er SHALL geen schrijvend endpoint bestaan.

#### Scenario: Cursus opvragen op id
- **GIVEN** een actieve cursus "BIO101" met naam "Cell biology"
- **WHEN** een systeem met `Ooapi.Read` `GET /ooapi/v5/courses/{id}` aanroept
- **THEN** is de status 200 met `courseId` gelijk aan het id, `primaryCode` met code "BIO101" en `name` "Cell biology"

#### Scenario: Onbekend id
- **WHEN** een systeem `GET /ooapi/v5/persons/{id}` aanroept met een id dat niet bestaat
- **THEN** is de status 404

#### Scenario: Schrijven wordt niet ondersteund
- **WHEN** een systeem `POST /ooapi/v5/courses` aanroept
- **THEN** is de status 404 of 405 en is er niets aangemaakt

### Requirement: Een uitvoering is een OOAPI-offering van het type course
`GET /ooapi/v5/offerings` SHALL de uitvoeringen (`CourseOffering`) van Doctrina tonen als offerings met
`offeringType` "course", met verwijzingen naar de cursus (`course`) en de periode (`academicSession`) en de begin- en
einddatum.

#### Scenario: Uitvoering met cursus en periode
- **GIVEN** een uitvoering van "BIO101" in periode "2026-P1"
- **WHEN** een systeem `GET /ooapi/v5/offerings/{id}` aanroept
- **THEN** heeft het antwoord `offeringType` "course" en verwijzen `course` en `academicSession` naar de id's van "BIO101" en "2026-P1"

### Requirement: Een inschrijving is een OOAPI-association met een rol
`GET /ooapi/v5/associations` SHALL de inschrijvingen tonen met `role`, een verwijzing naar de persoon (`person`) en de
uitvoering (`offering`). Rollen SHALL worden afgebeeld volgens design D5; een rol zonder OOAPI-equivalent SHALL
als "other" worden getoond.

#### Scenario: Docent in een uitvoering
- **GIVEN** een persoon die als `Lecturer` in een uitvoering is ingeschreven
- **WHEN** een systeem de association opvraagt
- **THEN** is `role` "teacher" en verwijzen `person` en `offering` naar de persoon en de uitvoering

#### Scenario: Rol zonder equivalent
- **GIVEN** een inschrijving met rol `Coordinator`
- **WHEN** een systeem de association opvraagt
- **THEN** is `role` "other"

### Requirement: Lijsten zijn gepagineerd volgens de standaard
Elke lijst SHALL `pageNumber` (vanaf 1) en `pageSize` ondersteunen, met een standaardgrootte en een maximum volgens
design D6, en SHALL `pageSize`, `pageNumber`, `hasPreviousPage`, `hasNextPage`, `totalPages` en `items` teruggeven. De
volgorde SHALL stabiel zijn. Een ongeldige waarde SHALL status 400 geven.

#### Scenario: Tweede pagina van personen
- **GIVEN** 45 actieve personen en een standaard `pageSize` van 20
- **WHEN** een systeem `GET /ooapi/v5/persons?pageNumber=2` aanroept
- **THEN** bevat `items` 20 personen, is `totalPages` 3 en zijn `hasPreviousPage` en `hasNextPage` beide waar

#### Scenario: Te grote pagina
- **WHEN** een systeem `pageSize` boven het maximum opgeeft
- **THEN** is de status 400

### Requirement: Alleen actieve records worden getoond
De OOAPI-resources SHALL records met status "te verwijderen" NIET tonen, ook niet op id.

#### Scenario: Verwijderd record is onzichtbaar
- **GIVEN** een persoon met status "te verwijderen"
- **WHEN** een systeem de persoon opvraagt op id of via de lijst
- **THEN** is het id 404 en staat de persoon niet in `items`

### Requirement: De sleutel van het bronsysteem staat als code in het antwoord
Elk record SHALL zijn `ExternalId` tonen als `primaryCode` (met `codeType` "identifier"), en het Doctrina-id als het
OOAPI-id. Een lijst SHALL kunnen filteren op `primaryCode`.

#### Scenario: Filteren op code
- **GIVEN** cursussen "BIO101" en "WIS201"
- **WHEN** een systeem `GET /ooapi/v5/courses?primaryCode=WIS201` aanroept
- **THEN** bevat `items` alleen "WIS201"

### Requirement: Het lezen van OOAPI vraagt de permissie Ooapi.Read
Alle endpoints SHALL alleen bearer-tokens accepteren (geen sessiecookie, geen redirects) en `Ooapi.Read` vereisen.
`Authorization:RolePermissions` SHALL scopes aan die permissie koppelen; in Development SHALL `doctrina.persons.read`
en `doctrina.courses.read` dat doen. Zonder geldig token SHALL de status 401 zijn, zonder permissie 403.

#### Scenario: Zonder token
- **WHEN** een client `GET /ooapi/v5/courses` aanroept zonder token
- **THEN** is de status 401

#### Scenario: Token zonder permissie
- **GIVEN** een geldig token zonder scope die op `Ooapi.Read` uitkomt
- **WHEN** de client `GET /ooapi/v5/courses` aanroept
- **THEN** is de status 403

#### Scenario: Sessiecookie volstaat niet
- **GIVEN** een ingelogde gebruiker met alleen een sessiecookie
- **WHEN** die `GET /ooapi/v5/courses` aanroept
- **THEN** is de status 401

### Requirement: De module staat uit als ze niet is ingeschakeld
Zonder `Ooapi` in `Modules:Enabled` SHALL `/ooapi/v5` niets aanbieden (404) en SHALL Doctrina normaal starten.

#### Scenario: Module uitgeschakeld
- **GIVEN** `Modules:Enabled` zonder `Ooapi`
- **WHEN** een client `GET /ooapi/v5/courses` aanroept
- **THEN** is de status 404
