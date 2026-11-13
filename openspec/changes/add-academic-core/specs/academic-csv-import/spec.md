# Spec-delta

## Purpose
Onderwijsdata uit het SIS in Doctrina krijgen door per type een CSV-bestand te uploaden, met een rapport per regel en
dezelfde validatie en upsert als elke andere invoerroute.

## ADDED Requirements

### Requirement: Beheerders importeren één CSV-bestand per type
Een gebruiker met de permissie `Academic.Import` SHALL op de pagina "Import" een CSV-bestand kunnen uploaden voor één
type (`organizations`, `academic-sessions`, `courses`, `course-offerings`, `persons` of `associations`), met een bron
(`Source`, standaard "SIS"). Elke regel SHALL via de validator en de upsert-service van dat type gaan. Gebruikers zonder
`Academic.Import` SHALL de pagina niet kunnen gebruiken.

#### Scenario: Personen importeren
- **GIVEN** een gebruiker met `Academic.Import` en een CSV met drie geldige personen
- **WHEN** de gebruiker het bestand als type `persons` uploadt
- **THEN** bestaan de drie personen met `Source` "SIS" en toont het rapport drie keer "created"

#### Scenario: Import zonder permissie
- **WHEN** een ingelogde gebruiker zonder `Academic.Import` de importpagina opent
- **THEN** krijgt de gebruiker de pagina "Access denied" met status 403

### Requirement: Het CSV-formaat is vergevingsgezind en voorspelbaar
Het bestand SHALL UTF-8 zijn, met of zonder BOM, met `,` of `;` als scheidingsteken (automatisch herkend) en een
verplichte kopregel. Kolomnamen SHALL de OOAPI-veldnamen volgen. Datums SHALL ISO 8601 zijn. Ontbreekt een verplichte
kolom, dan SHALL het hele bestand worden geweigerd zonder iets op te slaan. Onbekende kolommen SHALL een waarschuwing
geven en verder genegeerd worden.

#### Scenario: Puntkomma als scheidingsteken
- **WHEN** een CSV met `;` als scheidingsteken en een BOM wordt geüpload
- **THEN** worden de regels net zo verwerkt als bij een bestand met `,` zonder BOM

#### Scenario: Verplichte kolom ontbreekt
- **WHEN** een `persons`-CSV zonder kolom `surname` wordt geüpload
- **THEN** wordt het bestand geweigerd met een melding die de ontbrekende kolom noemt, en is er niets opgeslagen

#### Scenario: Onbekende kolom
- **WHEN** een `courses`-CSV met een extra kolom `colour` wordt geüpload
- **THEN** worden de regels verwerkt en meldt het rapport een waarschuwing over `colour`

### Requirement: Het rapport toont het resultaat per regel
Na de import SHALL de pagina een samenvatting (aantallen created, updated, unchanged, failed) en een tabel per regel
tonen met regelnummer, `ExternalId`, resultaat en bij een fout de foutcode en melding. Een foute regel SHALL de andere
regels niet tegenhouden.

#### Scenario: Eén foute regel tussen goede
- **GIVEN** een `associations`-CSV met vier regels, waarvan regel 3 een onbekende persoon noemt
- **WHEN** het bestand wordt geïmporteerd
- **THEN** zijn regels 2, 4 en 5 opgeslagen en toont het rapport bij regel 3 de code `Academic.Association.ReferenceNotFound`

### Requirement: Importeren is idempotent
Hetzelfde bestand twee keer importeren SHALL geen dubbele records opleveren.

#### Scenario: Zelfde bestand opnieuw
- **GIVEN** een `courses`-CSV die al eens is geïmporteerd
- **WHEN** hetzelfde bestand opnieuw wordt geïmporteerd
- **THEN** is het aantal cursussen gelijk gebleven en toont het rapport elke regel als "unchanged"

### Requirement: De import heeft een grens
Een bestand groter dan de ingestelde grens (`Academic:Import:MaxRows`, standaard 10.000 regels, en
`Academic:Import:MaxFileSize`, standaard 5 MB) SHALL worden geweigerd met een melding, zonder iets op te slaan.

#### Scenario: Te groot bestand
- **GIVEN** `Academic:Import:MaxRows` is 100
- **WHEN** een CSV met 101 regels wordt geüpload
- **THEN** wordt het bestand geweigerd met een melding over het maximum en is er niets opgeslagen
