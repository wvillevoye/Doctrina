# Spec-delta

## Purpose
Beheerders laten zien welke onderwijsdata er in Doctrina staat, zodat ze een import kunnen controleren, zonder dat ze
gegevens van het SIS in Doctrina kunnen wijzigen.

## ADDED Requirements

### Requirement: Beheerders zien per type een lijst met zoeken
Een gebruiker met de permissie `Academic.Read` SHALL per type (organizations, academic sessions, courses, course
offerings, persons, associations) een lijst kunnen openen. De lijst SHALL gepagineerd zijn (standaard 25 per pagina),
SHALL kunnen zoeken op naam en `ExternalId`, en SHALL per record de `Status` en `Source` tonen. Het hoofdmenu SHALL
het item "Academic" alleen tonen aan gebruikers met `Academic.Read`.

#### Scenario: Zoeken op ExternalId
- **GIVEN** een gebruiker met `Academic.Read` en de personen "I6000001" en "P7000002"
- **WHEN** de gebruiker in de personenlijst zoekt op "I6000001"
- **THEN** toont de lijst alleen de persoon "I6000001"

#### Scenario: Paginering
- **GIVEN** 60 cursussen
- **WHEN** een gebruiker met `Academic.Read` de cursuslijst opent
- **THEN** toont de lijst 25 cursussen en kan de gebruiker naar pagina 2 en 3

#### Scenario: Geen permissie
- **WHEN** een ingelogde gebruiker zonder `Academic.Read` de cursuslijst opent
- **THEN** krijgt de gebruiker de pagina "Access denied" met status 403 en staat "Academic" niet in het hoofdmenu

### Requirement: Een detailpagina toont een record met zijn verbanden
De detailpagina van een record SHALL alle velden, `ExternalId`, `Source`, `Status` en `DateLastModified` tonen, en de
directe verbanden als links: bij een uitvoering de cursus, de periode en de inschrijvingen; bij een persoon de
inschrijvingen; bij een cursus de uitvoeringen.

#### Scenario: Uitvoering met inschrijvingen
- **GIVEN** een uitvoering met twee studenten en één docent
- **WHEN** een gebruiker met `Academic.Read` de detailpagina van de uitvoering opent
- **THEN** toont de pagina de cursus, de periode en drie inschrijvingen met naam en rol

### Requirement: De beheerschermen zijn alleen-lezen
De beheerschermen SHALL geen formulieren of knoppen hebben om records aan te maken, te wijzigen of te verwijderen.
Wijzigingen SHALL via de bron lopen (de CSV-import, later REST).

#### Scenario: Geen bewerkknoppen
- **WHEN** een gebruiker met alle permissies de detailpagina van een persoon opent
- **THEN** staat er geen knop of formulier om de persoon te wijzigen of te verwijderen
