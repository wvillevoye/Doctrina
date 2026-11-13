# Spec-delta

## Purpose
Systemen laten lezen welke groepen er in een uitvoering zijn en wie erin zit, zonder dat ze groepen kunnen wijzigen.

## ADDED Requirements

### Requirement: Groepen zijn via de API te lezen
`GET /api/v1/groups?offeringId={offeringId}` SHALL de actieve groepen van de actieve GroupSets van die uitvoering geven,
en `GET /api/v1/groups/{id}` één groep. Een groep SHALL worden weergegeven met `id`, `groupId` (de ExternalId), `name`,
de GroupSet (`groupSetId`, `groupSetName`), `offeringId` (de ExternalId van de uitvoering) en `members` als lijst van
`personId`'s (ExternalId). Beide SHALL de permissie `Groups.Read` vereisen. Er SHALL geen endpoints zijn om groepen te
wijzigen.

#### Scenario: Groepen van een uitvoering
- **GIVEN** uitvoering "BIO101-2026-P1" met een GroupSet "Project teams" en een groep "Team A" met de studenten "I6000001" en "I6000002"
- **WHEN** een systeem met `Groups.Read` `GET /api/v1/groups?offeringId={id}` aanroept
- **THEN** bevat het antwoord "Team A" met `groupSetName` "Project teams", `offeringId` "BIO101-2026-P1" en `members` ["I6000001", "I6000002"]

#### Scenario: Verwijderde groep
- **GIVEN** een groep met status `toBeDeleted`
- **WHEN** een systeem `GET /api/v1/groups/{id}` van die groep aanroept
- **THEN** is de status 404 met `code` `Groups.Group.NotFound`

#### Scenario: Zonder Groups.Read
- **WHEN** een systeem zonder `Groups.Read` `GET /api/v1/groups?offeringId={id}` aanroept
- **THEN** is de status 403
