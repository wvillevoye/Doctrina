## Purpose

Externe systemen die OOAPI v5 spreken kunnen het onderwijskernmodel van Doctrina (organisaties, perioden, cursussen,
uitvoeringen, personen en inschrijvingen) alleen-lezen opvragen, zonder Doctrina-specifieke koppeling.

## ADDED Requirements

### Requirement: Doctrina biedt OOAPI v5-resources aan onder /ooapi/v5
De module Ooapi SHALL, als ze is ingeschakeld, voor `organizations`, `academic-sessions`, `courses` en `persons` een lijst
(`GET /ooapi/v5/{resource}`) en één record op id (`GET /ooapi/v5/{resource}/{id}`) aanbieden, en voor `offerings` en
`associations` een record op id, met veldnamen en taaltypering volgens OOAPI v5 (namen als `[{ language, value }]`, de
taal uit `Ooapi:Language`). Een onbekend of verwijderd id SHALL 404 geven. Er SHALL geen schrijvend endpoint bestaan.

#### Scenario: Cursus opvragen op id
- **GIVEN** een actieve cursus "BIO101" met naam "Cell biology"
- **WHEN** een systeem met `Ooapi.Read` `GET /ooapi/v5/courses/{id}` aanroept
- **THEN** is de status 200 met `courseId` gelijk aan het id, `primaryCode` met code "BIO101" en `name` met de waarde "Cell biology" in de ingestelde taal

#### Scenario: Onbekend id
- **WHEN** een systeem `GET /ooapi/v5/persons/{id}` aanroept met een id dat niet bestaat
- **THEN** is de status 404

#### Scenario: Schrijven wordt niet ondersteund
- **WHEN** een systeem `POST /ooapi/v5/courses` aanroept
- **THEN** is de status 404 of 405 en is er niets aangemaakt

#### Scenario: Organisatie met type
- **GIVEN** een actieve organisatie van het type `Institution` met een bovenliggende organisatie
- **WHEN** een systeem de organisatie opvraagt
- **THEN** is `organizationType` "institute" en verwijst `parent` naar het id van de bovenliggende organisatie

### Requirement: Een uitvoering is een OOAPI-offering van het type course
`GET /ooapi/v5/offerings/{id}` SHALL een uitvoering (`CourseOffering`) tonen als offering met `offeringType` "course",
verwijzingen naar de cursus (`course`) en de periode (`academicSession`) en `startDate` en `endDate` (zonder eigen datum
die van de periode). De lijsten `GET /courses/{id}/offerings`, `/academic-sessions/{id}/offerings` en
`/organizations/{id}/offerings` SHALL de uitvoeringen daarvan geven. Een top-level `/offerings` bestaat niet.

#### Scenario: Uitvoering met cursus en periode
- **GIVEN** een uitvoering van "BIO101" in periode "2026-P1"
- **WHEN** een systeem `GET /ooapi/v5/offerings/{id}` aanroept
- **THEN** heeft het antwoord `offeringType` "course" en verwijzen `course` en `academicSession` naar de id's van "BIO101" en "2026-P1"

#### Scenario: Uitvoeringen van een cursus
- **GIVEN** een cursus met twee uitvoeringen en een andere cursus met één
- **WHEN** een systeem `GET /ooapi/v5/courses/{id}/offerings` aanroept
- **THEN** bevat `items` alleen de twee uitvoeringen van die cursus

#### Scenario: Uitvoering zonder eigen datums
- **GIVEN** een uitvoering zonder begin- en einddatum in een periode die loopt van 1 september tot 31 oktober
- **WHEN** een systeem de offering opvraagt
- **THEN** zijn `startDate` en `endDate` de datums van de periode

### Requirement: Een inschrijving is een OOAPI-association met een rol
`GET /ooapi/v5/associations/{id}` SHALL een inschrijving tonen met `associationType` "courseOfferingAssociation",
`role`, `state` "associated" en verwijzingen naar de persoon (`person`) en de uitvoering (`offering`). De lijsten
`GET /offerings/{id}/associations` (filter `role`) en `GET /persons/{id}/associations` SHALL de inschrijvingen daarvan
geven. Rollen SHALL worden afgebeeld volgens design D5 (`student`, `lecturer`, `teaching assistant`, `coordinator`).

#### Scenario: Docent in een uitvoering
- **GIVEN** een persoon die als `Lecturer` in een uitvoering is ingeschreven
- **WHEN** een systeem de association opvraagt
- **THEN** is `role` "lecturer" en verwijzen `person` en `offering` naar de persoon en de uitvoering

#### Scenario: Teaching assistant en coördinator
- **GIVEN** een `TeachingAssistant` en een `Coordinator` in een uitvoering
- **WHEN** een systeem de associations van de uitvoering opvraagt
- **THEN** zijn de rollen "teaching assistant" en "coordinator"

#### Scenario: Filteren op rol
- **GIVEN** een uitvoering met drie studenten en een docent
- **WHEN** een systeem `GET /ooapi/v5/offerings/{id}/associations?role=student` aanroept
- **THEN** bevat `items` alleen de drie studenten

### Requirement: Lijsten zijn gepagineerd volgens de standaard
Elke lijst SHALL `pageNumber` (vanaf 1) en `pageSize` ondersteunen, met als toegestane groottes 10, 20, 50, 100 en 250
en standaard 10, en SHALL `pageSize`, `pageNumber`, `hasPreviousPage`, `hasNextPage`, `totalPages` en `items`
teruggeven. De volgorde SHALL stabiel zijn. Een ongeldige waarde SHALL status 400 geven.

#### Scenario: Tweede pagina van personen
- **GIVEN** 45 actieve personen
- **WHEN** een systeem `GET /ooapi/v5/persons?pageSize=20&pageNumber=2` aanroept
- **THEN** bevat `items` 20 personen, is `totalPages` 3 en zijn `hasPreviousPage` en `hasNextPage` beide waar

#### Scenario: Standaard paginagrootte
- **GIVEN** 45 actieve personen
- **WHEN** een systeem `GET /ooapi/v5/persons` aanroept zonder parameters
- **THEN** bevat `items` 10 personen en is `pageNumber` 1

#### Scenario: Te grote of ongeldige pagina
- **WHEN** een systeem `pageSize` opgeeft die niet 10, 20, 50, 100 of 250 is, of `pageNumber` kleiner dan 1
- **THEN** is de status 400

### Requirement: Alleen actieve records worden getoond
De OOAPI-resources SHALL records met status "te verwijderen" NIET tonen, ook niet op id.

#### Scenario: Verwijderd record is onzichtbaar
- **GIVEN** een persoon met status "te verwijderen"
- **WHEN** een systeem de persoon opvraagt op id of via de lijst
- **THEN** is het id 404 en staat de persoon niet in `items`

### Requirement: De sleutel van het bronsysteem staat als code in het antwoord
Elk record SHALL zijn `ExternalId` tonen als `primaryCode` (met `codeType` "identifier"), en het Doctrina-id als het
OOAPI-id. Een lijst van organisaties, perioden, cursussen of personen SHALL kunnen filteren op `primaryCode`.

#### Scenario: Filteren op code
- **GIVEN** cursussen "BIO101" en "WIS201"
- **WHEN** een systeem `GET /ooapi/v5/courses?primaryCode=WIS201` aanroept
- **THEN** bevat `items` alleen "WIS201"

### Requirement: Verplichte OOAPI-velden zonder bron worden afgeleid of ingesteld
Een veld dat OOAPI verplicht stelt maar Doctrina niet bewaart SHALL worden afgeleid of uit de instellingen komen,
volgens design D5: bij een persoon `affiliations` (uit de rollen) en `activeEnrollment`, bij een cursus
`teachingLanguage` en `level`, bij een offering `description`, `teachingLanguage` en `resultExpected`. Een veld dat
Doctrina niet kan afleiden en niet mag verzinnen (het e-mailadres van een persoon zonder adres) SHALL ontbreken.

#### Scenario: Persoon met een inschrijving als student
- **GIVEN** een persoon met een actieve inschrijving als `Student`
- **WHEN** een systeem de persoon opvraagt
- **THEN** is `affiliations` `["student"]` en `activeEnrollment` waar

#### Scenario: Persoon zonder inschrijving
- **GIVEN** een persoon zonder inschrijvingen en zonder e-mailadres
- **WHEN** een systeem de persoon opvraagt
- **THEN** is `affiliations` `["guest"]`, is `activeEnrollment` onwaar en ontbreekt `mail`

#### Scenario: Cursus met ingestelde taal en niveau
- **GIVEN** `Ooapi:TeachingLanguage` "eng" en `Ooapi:Level` "master"
- **WHEN** een systeem een cursus opvraagt
- **THEN** zijn `teachingLanguage` "eng" en `level` "master"

### Requirement: GET / geeft de servicegegevens
`GET /ooapi/v5` SHALL de servicegegevens geven: `contactEmail`, `specification` en `documentation`, uit de instellingen
`Ooapi:ContactEmail`, `Ooapi:Specification` en `Ooapi:Documentation`.

#### Scenario: Servicegegevens
- **GIVEN** een ingesteld contactadres
- **WHEN** een systeem met `Ooapi.Read` `GET /ooapi/v5` aanroept
- **THEN** is de status 200 en bevat het antwoord `contactEmail`, `specification` en `documentation`

### Requirement: Het lezen van OOAPI vraagt de permissie Ooapi.Read
Alle endpoints SHALL alleen bearer-tokens accepteren (geen sessiecookie, geen redirects) en `Ooapi.Read` vereisen.
`Authorization:RolePermissions` SHALL scopes aan die permissie koppelen; in Development SHALL `doctrina.persons.read`
en `doctrina.courses.read` dat doen. Zonder geldig token SHALL de status 401 zijn, zonder permissie 403. Fouten zijn
`application/problem+json` met `status` en `title`.

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

### Requirement: Geen persoonsgegevens in logs bij OOAPI
Doctrina SHALL bij OOAPI-aanroepen geen namen, e-mailadressen, persoonsnummers (`primaryCode`) of query-waarden loggen.

#### Scenario: Logs bij het opvragen van personen
- **WHEN** een systeem personen opvraagt, ook gefilterd op `primaryCode`
- **THEN** bevat geen logregel (ook niet op `Trace`) een naam, e-mailadres of persoonsnummer
