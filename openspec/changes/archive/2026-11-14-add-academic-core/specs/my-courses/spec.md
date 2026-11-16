# Spec-delta

## Purpose
Elke ingelogde gebruiker een overzicht geven van de cursusuitvoeringen waarin die persoon zit, met de eigen rol.

## ADDED Requirements

### Requirement: "My courses" toont de eigen actieve uitvoeringen
De pagina `/my-courses` SHALL voor de ingelogde, gekoppelde persoon elke actieve uitvoering tonen waarin die een actieve
inschrijving heeft, met de naam van de cursus, de naam van de uitvoering, de periode en de eigen rol. De pagina en het
menu-item "My courses" SHALL de permissie `Academic.MyCourses` vereisen; de configuratie van Development SHALL die permissie
aan de rollen `admin`, `employee` en `student` geven.

#### Scenario: Student met twee uitvoeringen
- **GIVEN** een gekoppelde persoon met een actieve inschrijving als `student` in "BIO101-2026-P1" en als `teaching-assistant` in "WIS201-2026-P1"
- **WHEN** de gebruiker "My courses" opent
- **THEN** toont de pagina beide uitvoeringen met rol "Student" en "Teaching assistant"

#### Scenario: Inschrijvingen en uitvoeringen die niet meetellen
- **GIVEN** een gekoppelde persoon met een inschrijving met status `ToBeDeleted` en een actieve inschrijving in een uitvoering met status `ToBeDeleted`
- **WHEN** de gebruiker "My courses" opent
- **THEN** toont de pagina geen van beide uitvoeringen

#### Scenario: Geen inschrijvingen
- **GIVEN** een gekoppelde persoon zonder actieve inschrijvingen
- **WHEN** de gebruiker "My courses" opent
- **THEN** toont de pagina de melding "You are not enrolled in any courses"

#### Scenario: Menu-item voor studenten
- **WHEN** een gebruiker met rol `student` inlogt
- **THEN** staat "My courses" in het hoofdmenu

#### Scenario: Anonieme bezoeker
- **WHEN** een anonieme bezoeker `/my-courses` opent
- **THEN** wordt de bezoeker naar inloggen gestuurd met `/my-courses` als terugkeeradres
