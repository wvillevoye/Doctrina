# Spec-delta

## MODIFIED Requirements

### Requirement: Beheerders importeren één CSV-bestand per type
Een gebruiker met de permissie `Academic.Import` SHALL op de pagina "Import" een CSV-bestand voor één type
(`organizations`, `academic-sessions`, `courses`, `course-offerings`, `persons` of `associations`) of een zip met meerdere
CSV-bestanden kunnen uploaden, met een bron (`Source`, standaard "SIS") en optioneel "dry run". De upload SHALL een
importjob maken en de gebruiker naar de pagina van die job sturen. Elke regel SHALL via de validator en de
upsert-service van zijn type gaan. Gebruikers zonder `Academic.Import` SHALL de pagina niet kunnen gebruiken.

#### Scenario: Personen importeren
- **GIVEN** een gebruiker met `Academic.Import` en een CSV met drie geldige personen
- **WHEN** de gebruiker het bestand als type `persons` uploadt en de job klaar is
- **THEN** bestaan de drie personen met `Source` "SIS" en toont de jobpagina drie keer "created"

#### Scenario: Import zonder permissie
- **WHEN** een ingelogde gebruiker zonder `Academic.Import` de importpagina opent
- **THEN** krijgt de gebruiker de pagina "Access denied" met status 403

### Requirement: Het rapport toont het resultaat per regel
Een importjob SHALL per regel het bestand, het regelnummer, de sleutel, het resultaat (`created`, `updated`, `unchanged`
of `failed`) en bij een fout de foutcode en melding bewaren, en een samenvatting per resultaat. Het rapport SHALL op de
jobpagina te zien zijn en als CSV te downloaden. Een foute regel SHALL de andere regels niet tegenhouden.

#### Scenario: Eén foute regel tussen goede
- **GIVEN** een `associations`-CSV met vier regels, waarvan regel 3 een onbekende persoon noemt
- **WHEN** het bestand wordt geïmporteerd
- **THEN** zijn regels 2, 4 en 5 opgeslagen en heeft het rapport bij regel 3 de code `Academic.Association.ReferenceNotFound`

#### Scenario: Rapport als CSV
- **GIVEN** een afgeronde importjob
- **WHEN** de gebruiker het rapport downloadt
- **THEN** krijgt de gebruiker een CSV met de kolommen `file`, `line`, `key`, `result`, `code` en `message`, één regel per gegevensregel

### Requirement: De import heeft een grens
Een upload groter dan de ingestelde grens (`Academic:Import:MaxRows`, standaard 100.000 regels over alle bestanden, en
`Academic:Import:MaxFileSize`, standaard 50 MB) SHALL worden geweigerd met een melding, zonder iets op te slaan.

#### Scenario: Te groot bestand
- **GIVEN** `Academic:Import:MaxRows` is 100
- **WHEN** een CSV met 101 regels wordt geüpload
- **THEN** eindigt de job als `failed` met een melding over het maximum en is er niets opgeslagen

## ADDED Requirements

### Requirement: Een import draait als achtergrondjob
Een upload SHALL direct een importjob met status `queued` opleveren en daarna op de achtergrond worden verwerkt
(`running`, dan `completed` of `failed`). De job SHALL de bron, wie hem startte, het tijdstip, de bestanden en de
aantallen per resultaat bewaren. Een fout in het bestand als geheel (lege upload, ontbrekende verplichte kolom, onbekend
bestand in een zip, te groot) SHALL de job `failed` maken met een melding, zonder iets op te slaan.

#### Scenario: Job doorloopt de statussen
- **WHEN** een gebruiker een geldige CSV uploadt
- **THEN** is de job eerst `queued` en daarna `completed`, met de aantallen per resultaat

#### Scenario: Ontbrekende verplichte kolom
- **WHEN** een `persons`-CSV zonder kolom `surname` wordt geüpload
- **THEN** eindigt de job als `failed` met een melding die de kolom noemt, en is er niets opgeslagen

### Requirement: Een zip bevat meerdere typen in een vaste volgorde
Een zip SHALL meerdere CSV-bestanden kunnen bevatten. Het type van een bestand SHALL uit `manifest.csv` (kolommen `file`
en `type`) komen, of zonder manifest uit de bestandsnaam (`<type>.csv`). De bestanden SHALL in de volgorde
organizations, academic-sessions, courses, course-offerings, persons, associations worden verwerkt, ongeacht hun volgorde
in de zip. Een bestand waarvan het type niet te bepalen is, SHALL de job `failed` maken zonder iets op te slaan.

#### Scenario: Zip met verwijzingen tussen bestanden
- **GIVEN** een zip met `associations.csv`, `persons.csv`, `course-offerings.csv`, `courses.csv` en `academic-sessions.csv`, in die volgorde
- **WHEN** de zip wordt geïmporteerd
- **THEN** zijn alle regels "created", ook de inschrijvingen die naar personen en uitvoeringen uit dezelfde zip verwijzen

#### Scenario: Manifest met eigen bestandsnamen
- **GIVEN** een zip met `manifest.csv` die `studenten.csv` als `persons` aanwijst
- **WHEN** de zip wordt geïmporteerd
- **THEN** worden de regels van `studenten.csv` als personen verwerkt

#### Scenario: Onbekend bestand
- **WHEN** een zip zonder manifest met een bestand `cijfers.csv` wordt geüpload
- **THEN** eindigt de job als `failed` met een melding die `cijfers.csv` noemt, en is er niets opgeslagen

### Requirement: Een dry-run laat zien wat er zou gebeuren zonder op te slaan
Een import met "dry run" SHALL precies dezelfde verwerking en hetzelfde rapport geven als een echte import, inclusief
verwijzingen naar records uit eerdere bestanden in dezelfde upload, maar SHALL niets opslaan behalve de job en het
rapport. Na een afgeronde dry-run SHALL de gebruiker dezelfde upload als echte import kunnen starten.

#### Scenario: Dry-run van een zip
- **GIVEN** een lege database en een zip met cursussen, perioden, uitvoeringen, personen en inschrijvingen
- **WHEN** de zip als dry run wordt geïmporteerd
- **THEN** toont het rapport elke regel als "created" en bestaan er daarna geen cursussen, personen of inschrijvingen

#### Scenario: Na een dry-run echt importeren
- **GIVEN** een afgeronde dry-run
- **WHEN** de gebruiker op de jobpagina "Import for real" kiest
- **THEN** start een nieuwe job met dezelfde bestanden zonder dry run, en zijn de records daarna opgeslagen

### Requirement: Systemen importeren via de API
`POST /api/v1/imports` SHALL een multipart-upload (veld `file`, en `type` bij een losse CSV) accepteren, met
`?dryRun=true` als optie, en SHALL 202 geven met de job en een `Location`. `GET /api/v1/imports/{id}` SHALL de status en
samenvatting geven, en `GET /api/v1/imports/{id}/report` het rapport als JSON, of als CSV met `?format=csv`.

#### Scenario: Import via de API
- **WHEN** een systeem met `Academic.Import` een `courses`-CSV naar `POST /api/v1/imports` stuurt
- **THEN** is de status 202 met een `Location` naar `/api/v1/imports/{id}`, en geeft die na afloop `completed` met de aantallen

### Requirement: De import-API vraagt Academic.Import en gebruikt de bron van de client
Alle import-endpoints SHALL `Academic.Import` vereisen. De bron van een import via de API SHALL die van de client zijn
(`Api:Sources:{azp}`, anders `SIS`). De upload SHALL per client begrensd zijn zoals de batch-endpoints.

#### Scenario: Bron van de client
- **GIVEN** `Api:Sources:roosterpakket` is "Rooster"
- **WHEN** de client "roosterpakket" een `courses`-CSV importeert
- **THEN** hebben de cursussen `source` "Rooster"

#### Scenario: Zonder Academic.Import
- **WHEN** een systeem zonder `Academic.Import` `POST /api/v1/imports` aanroept
- **THEN** is de status 403

### Requirement: Er is een leeg sjabloon per type
`GET /api/v1/imports/templates/{type}.csv` en de importpagina SHALL per type een CSV met alleen de kopregel geven, met
de kolommen van dat type (sleutel, gegevensvelden en `status`).

#### Scenario: Sjabloon voor personen
- **WHEN** een gebruiker het sjabloon voor `persons` downloadt
- **THEN** is de inhoud `personId,givenName,surnamePrefix,surname,displayName,mail,status`

### Requirement: Geüploade bestanden worden na de bewaartermijn verwijderd
Een dagelijkse achtergrondtaak SHALL de geüploade bestanden van jobs die ouder zijn dan `Academic:Import:RetentionDays`
(standaard 30) verwijderen. De job en het rapport SHALL blijven bestaan. Een job zonder bestand SHALL NOT meer als echte
import te herhalen zijn.

#### Scenario: Oude upload opruimen
- **GIVEN** een job van 31 dagen geleden met zijn bestand
- **WHEN** de opruimtaak draait
- **THEN** is het bestand verwijderd en zijn de job en het rapport er nog
