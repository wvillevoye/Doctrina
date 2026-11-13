# Spec-delta

## Purpose
Een pagina per cursusuitvoering voor de deelnemers, waarop modules elk hun deel tonen (groepen nu, later content en
LTI-tools).

## ADDED Requirements

### Requirement: Elke uitvoering heeft een pagina voor haar deelnemers
De pagina `/courses/{offering}` SHALL de naam van de cursus, de naam van de uitvoering, de periode en de eigen rol van de
gebruiker tonen. Toegang SHALL hebben: wie een actieve inschrijving in die uitvoering heeft, en wie de permissie
`Academic.Read` heeft. Ieder ander ingelogd SHALL de pagina "Access denied" met status 403 krijgen; een onbekende of te
verwijderen uitvoering SHALL status 404 geven.

#### Scenario: Student opent de pagina van zijn uitvoering
- **GIVEN** een student met een actieve inschrijving als `student` in "BIO101-2026-P1"
- **WHEN** de student `/courses/{offering}` van "BIO101-2026-P1" opent
- **THEN** toont de pagina de cursus, de uitvoering, de periode en de rol "Student"

#### Scenario: Niet ingeschreven
- **WHEN** een student zonder inschrijving in "BIO101-2026-P1" de pagina van die uitvoering opent
- **THEN** krijgt de student de pagina "Access denied" met status 403

#### Scenario: Uitvoering op te verwijderen
- **GIVEN** een uitvoering met status `ToBeDeleted`
- **WHEN** een ingeschreven student haar pagina opent
- **THEN** is de status 404

### Requirement: Modules voegen secties toe aan de pagina van een uitvoering
Een module SHALL via `Doctrina.Modules.Academic.Contracts` een component als sectie van de pagina van een uitvoering
kunnen registreren, met een volgorde. De pagina SHALL de secties van alle ingeschakelde modules in die volgorde tonen,
met de uitvoering als parameter. Secties van uitgeschakelde modules SHALL NOT getoond worden.

#### Scenario: Sectie van een ingeschakelde module
- **GIVEN** een ingeschakelde module die een sectie "Test section" registreert
- **WHEN** een ingeschreven student de pagina van een uitvoering opent
- **THEN** toont de pagina "Test section" met het id van die uitvoering
