## Purpose

Een LTI-tool kan via Assignment and Grade Services (AGS) line items beheren en scores van studenten naar Doctrina
sturen. Doctrina bewaart ze en toont ze aan de student en de docent bij de tool link.

## ADDED Requirements

### Requirement: Line items van een tool beheren
Met `…/scope/lineitem` SHALL een tool onder `/lti/ags/{context}/lineitems` (`{context}` = id van de uitvoering) line
items kunnen aanmaken, opvragen, wijzigen en verwijderen; met `…/lineitem.readonly` alleen lezen. Een line item heeft
`label` en `scoreMaximum` (groter dan 0). De lijst SHALL filteren (`resource_link_id`, `resource_id`, `tag`) en
pagineren (`limit`, `Link`). Een tool SHALL alleen haar eigen line items zien, in uitvoeringen met een tool link van haar.

#### Scenario: Line item aanmaken
- **GIVEN** een tool met cijferservices en een tool link in de uitvoering "Cell biology"
- **WHEN** de tool met een geldig token een line item `{ "label": "Quiz 1", "scoreMaximum": 10 }` post
- **THEN** is de status 201 met een `id`-URL, `label` "Quiz 1" en `scoreMaximum` 10, en is het line item bewaard

#### Scenario: Line item met een resource link van een andere tool
- **GIVEN** een `resourceLinkId` dat bij een tool link van een andere tool hoort
- **WHEN** de tool het line item aanmaakt
- **THEN** is de status 422 en is niets bewaard

#### Scenario: Lijst filteren op resource link
- **GIVEN** twee line items van de tool, één bij resource link "A"
- **WHEN** de tool `GET …/lineitems?resource_link_id=A` aanroept
- **THEN** bevat het antwoord alleen dat line item

#### Scenario: Uitvoering zonder tool link van deze tool
- **GIVEN** een uitvoering waarin de tool geen tool link heeft
- **WHEN** de tool daarvan de line items opvraagt of er één aanmaakt
- **THEN** is de status 404

#### Scenario: Line item van een andere tool
- **GIVEN** een line item dat een andere tool heeft aangemaakt
- **WHEN** de tool dat line item opvraagt, wijzigt of verwijdert
- **THEN** is de status 404

#### Scenario: Alleen lezen
- **GIVEN** een token met alleen `…/scope/lineitem.readonly`
- **WHEN** de tool een line item aanmaakt
- **THEN** is de status 403

### Requirement: Scores van studenten posten
Met `…/scope/score` SHALL een tool op `{lineitem}/scores` een score (`POST`, mediatype
`application/vnd.ims.lis.v1.score+json`) kunnen plaatsen met `userId` (het `sub` van een lid van de uitvoering),
`scoreGiven`, `scoreMaximum`, `comment`, `activityProgress`, `gradingProgress` en `timestamp`. `scoreGiven` en
`scoreMaximum` komen samen voor (maximum > 0, score niet negatief). Een score met een oudere `timestamp` dan de
bewaarde SHALL 409 geven; anders vervangt hij die.

#### Scenario: Score posten
- **GIVEN** een line item met `scoreMaximum` 10 en een student die lid is van de uitvoering
- **WHEN** de tool een score `{ "userId": "<sub>", "scoreGiven": 8, "scoreMaximum": 10, "activityProgress": "Completed", "gradingProgress": "FullyGraded", "timestamp": "2026-11-16T10:00:00Z" }` post
- **THEN** is de status 204 en is 8 van 10 bewaard voor die student

#### Scenario: Oudere score wordt geweigerd
- **GIVEN** een bewaarde score met `timestamp` 2026-11-16T10:00:00Z
- **WHEN** de tool een score voor dezelfde student post met `timestamp` 2026-11-16T09:00:00Z
- **THEN** is de status 409 en is de bewaarde score ongewijzigd

#### Scenario: Nieuwere score vervangt de oude
- **GIVEN** een bewaarde score van 5 van 10
- **WHEN** de tool een score van 9 van 10 post met een latere `timestamp`
- **THEN** is voor die student 9 van 10 bewaard

#### Scenario: Gebruiker is geen lid van de uitvoering
- **GIVEN** een `userId` van een persoon die niet is ingeschreven
- **WHEN** de tool een score post
- **THEN** is de status 422 en is niets bewaard

#### Scenario: Ongeldige score
- **WHEN** de tool een score post met `scoreGiven` zonder `scoreMaximum`, een negatieve `scoreGiven` of een onbekende `activityProgress`
- **THEN** is de status 422 en is niets bewaard

### Requirement: Results lezen
Met `…/scope/result.readonly` SHALL een tool op `{lineitem}/results` de bewaarde scores als results kunnen ophalen
(mediatype `application/vnd.ims.lis.v2.resultcontainer+json`), met `userId`, `resultScore`, `resultMaximum`,
`comment` en het `id` van het result, gepagineerd en te filteren op `user_id`.

#### Scenario: Results van een line item
- **GIVEN** scores voor twee studenten op een line item
- **WHEN** de tool `GET {lineitem}/results` aanroept
- **THEN** bevat het antwoord twee results met `userId`, `resultScore` en `resultMaximum`

#### Scenario: Results voor één gebruiker
- **GIVEN** scores voor twee studenten
- **WHEN** de tool `?user_id=<sub>` meegeeft
- **THEN** bevat het antwoord alleen het result van die student

### Requirement: Een student ziet de eigen scores
Bij een offering SHALL een ingelogde student de eigen scores van tool links in die offering zien: tool link, label van
het line item, score en maximum. Een student SHALL nooit scores van anderen zien.

#### Scenario: Student ziet de eigen score
- **GIVEN** een score 8 van 10 voor "Quiz 1" van student A
- **WHEN** student A de pagina van de offering bekijkt
- **THEN** staat "Quiz 1: 8 / 10" bij de eigen scores

#### Scenario: Student ziet geen scores van anderen
- **GIVEN** scores voor student A en student B
- **WHEN** student B de scores bekijkt
- **THEN** bevat het overzicht niets van student A

### Requirement: Een docent ziet de scores per tool link
Een gebruiker die de content van de uitvoering mag beheren SHALL per tool link een overzicht zien van de line items met
per ingeschreven student de score, met naam. Zonder beheerrecht SHALL de pagina 403 geven.

#### Scenario: Docent ziet het overzicht
- **GIVEN** scores van twee studenten op een line item van een tool link
- **WHEN** de docent het overzicht van die tool link opent
- **THEN** staan beide studenten met naam en score in de tabel

#### Scenario: Student mag het overzicht niet openen
- **WHEN** een student het overzicht van een tool link opent
- **THEN** is de status 403

### Requirement: Scores verdwijnen met hun line item
Bij het verwijderen van een line item SHALL Doctrina de bijbehorende scores meeverwijderen. Een tool link die uit de
content verdwijnt SHALL zijn koppeling met het line item verliezen (`resourceLinkId` leeg), niet de scores.

#### Scenario: Line item verwijderen
- **GIVEN** een line item met twee scores
- **WHEN** de tool het line item verwijdert
- **THEN** is de status 204 en zijn het line item en de scores weg

### Requirement: Geen persoonsgegevens in logs bij AGS
Doctrina SHALL bij AGS-aanroepen geen `userId`, scores, opmerkingen of namen loggen.

#### Scenario: Logs bij een score
- **WHEN** een tool een score post en een docent het overzicht opent
- **THEN** bevat geen logregel (ook niet op `Trace`) een `userId`, score, opmerking of naam
