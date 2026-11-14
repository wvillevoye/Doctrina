# Spec-delta

## Purpose
Doctrina een gedeelde manier geven om werk op de achtergrond te doen (imports nu, later bijvoorbeeld e-mail of
synchronisatie), dat een herstart overleeft en voor beheer zichtbaar is.

## ADDED Requirements

### Requirement: Achtergrondtaken draaien met Hangfire
Doctrina SHALL achtergrondtaken uitvoeren met Hangfire, met de opslag in het schema `hangfire` van de Doctrina-database.
Een taak die is ingepland, SHALL ook na een herstart van de host worden uitgevoerd. Modules SHALL taken inplannen via
de standaard Hangfire-API (`IBackgroundJobClient`, `IRecurringJobManager`).

#### Scenario: Ingeplande taak wordt uitgevoerd
- **WHEN** een module een taak inplant
- **THEN** voert de host die taak op de achtergrond uit

#### Scenario: Schema van Hangfire
- **WHEN** de host voor het eerst start op een lege database
- **THEN** staan de tabellen van Hangfire in het schema `hangfire`

### Requirement: Het dashboard is alleen voor beheerders
Het Hangfire-dashboard SHALL op `/hangfire` staan en SHALL alleen toegankelijk zijn voor ingelogde gebruikers met de rol
`admin`. Anderen SHALL status 401 (anoniem) of 403 krijgen.

#### Scenario: Beheerder opent het dashboard
- **WHEN** een gebruiker met rol `admin` `/hangfire` opent
- **THEN** wordt het dashboard getoond

#### Scenario: Student opent het dashboard
- **WHEN** een ingelogde gebruiker zonder rol `admin` `/hangfire` opent
- **THEN** is de status 403
