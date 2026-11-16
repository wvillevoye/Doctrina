## Purpose

Een LTI-tool kan via Names and Role Provisioning Services (NRPS) de deelnemers van een uitvoering met hun rollen
ophalen, zonder dat Doctrina meer persoonsgegevens deelt dan de beheerder voor die tool heeft toegestaan.

## ADDED Requirements

### Requirement: De deelnemers van een uitvoering ophalen
Met de scope `https://purl.imsglobal.org/spec/lti-nrps/scope/contextmembership.readonly` SHALL een tool met
ledenservices `GET /lti/nrps/{context}/memberships` kunnen aanroepen. Het antwoord (mediatype
`application/vnd.ims.lti-nrps.v2.membershipcontainer+json`) bevat `id`, `context` en `members` met per persoon `user_id`,
`status` "Active" en `roles`, in stabiele volgorde. Een uitvoering zonder tool link van de tool SHALL 404 geven.

#### Scenario: Deelnemers van een uitvoering
- **GIVEN** een uitvoering met student "S1" en docent "D1", en een tool met ledenservices met een tool link daarin
- **WHEN** de tool met een geldig token de deelnemers opvraagt
- **THEN** is de status 200 en bevat `members` twee deelnemers met `status` "Active", `user_id` en `roles`, en bevat `context` de id, het label en de titel van de uitvoering

#### Scenario: user_id is hetzelfde als sub in de launch
- **GIVEN** een student die een tool link van de tool heeft gestart
- **WHEN** de tool de deelnemers opvraagt
- **THEN** staat de `sub` uit de `id_token` van die student als `user_id` in `members`

#### Scenario: Persoon met meer rollen komt eenmaal voor
- **GIVEN** een persoon die in de uitvoering zowel docent als teaching assistant is
- **WHEN** de tool de deelnemers opvraagt
- **THEN** staat de persoon één keer in `members` met beide rollen

#### Scenario: Uitvoering zonder tool link van deze tool
- **GIVEN** een uitvoering waarin de tool geen tool link heeft
- **WHEN** de tool de deelnemers opvraagt
- **THEN** is de status 404

#### Scenario: Verwijderde inschrijving of persoon
- **GIVEN** een inschrijving of persoon met status "te verwijderen"
- **WHEN** de tool de deelnemers opvraagt
- **THEN** komt die deelnemer niet in `members` voor

### Requirement: Rollen volgen de LIS-rollen van de launch
`roles` SHALL dezelfde LIS-rol-URI's gebruiken als de `id_token` van de launch: `…/membership#Learner` voor een student,
`…/membership/Instructor#TeachingAssistant` voor een teaching assistant en `…/membership#Instructor` voor een docent of
coördinator. De lijst SHALL te filteren zijn met `role` (de volledige URI of de korte naam "Learner" of "Instructor").

#### Scenario: Rollen van een student en een docent
- **GIVEN** een student en een docent in de uitvoering
- **WHEN** de tool de deelnemers opvraagt
- **THEN** heeft de student de rol `http://purl.imsglobal.org/vocab/lis/v2/membership#Learner` en de docent `http://purl.imsglobal.org/vocab/lis/v2/membership#Instructor`

#### Scenario: Filteren op rol
- **GIVEN** drie studenten en een docent
- **WHEN** de tool `?role=Learner` meegeeft
- **THEN** bevat `members` alleen de drie studenten

### Requirement: Naam en e-mail alleen als de tool daarvoor is ingesteld
Een deelnemer SHALL `name`, `given_name` en `family_name` alleen bevatten als de tool "Send name" aan heeft en `email`
alleen bij "Send email" aan, net als in de `id_token`. Anders SHALL er alleen `user_id`, `status` en `roles` staan.

#### Scenario: Zonder instelling geen namen
- **GIVEN** een tool met "Send name" en "Send email" uit
- **WHEN** de tool de deelnemers opvraagt
- **THEN** bevat geen deelnemer `name`, `given_name`, `family_name` of `email`

#### Scenario: Met instelling naam en e-mail
- **GIVEN** een tool met "Send name" en "Send email" aan
- **WHEN** de tool de deelnemers opvraagt
- **THEN** bevat elke deelnemer `name`, `given_name`, `family_name` en `email` van de persoon

### Requirement: De lijst is gepagineerd en te beperken tot een resource link
`limit` SHALL het aantal deelnemers per pagina begrenzen (standaard 50, hoogstens 500); de volgende pagina SHALL via een
`Link`-header met `rel="next"` bereikbaar zijn. Met `rlid` SHALL de lijst alleen gelden als dat een tool link van deze tool
in de uitvoering is (anders 404); de lijst is dan gelijk aan die van de uitvoering.

#### Scenario: Paginering
- **GIVEN** 120 deelnemers en `limit=50`
- **WHEN** de tool de lijst en daarna de `rel="next"`-pagina's opvraagt
- **THEN** bevatten de pagina's 50, 50 en 20 deelnemers zonder dubbelen en heeft de laatste pagina geen `rel="next"`

#### Scenario: Te grote limit
- **WHEN** de tool `limit` boven 500 opgeeft
- **THEN** is de status 400

#### Scenario: Onbekende resource link
- **WHEN** de tool `rlid` opgeeft dat geen tool link van haar in de uitvoering is
- **THEN** is de status 404

### Requirement: Alleen met de scope en met ledenservices
Het endpoint SHALL een token zonder de NRPS-scope weigeren met 403 en zonder token met 401. Het tokenendpoint SHALL de
NRPS-scope alleen toekennen aan een tool met ledenservices.

#### Scenario: Zonder token
- **WHEN** een client de deelnemers opvraagt zonder token
- **THEN** is de status 401

#### Scenario: Token zonder NRPS-scope
- **GIVEN** een token met alleen een AGS-scope
- **WHEN** de tool de deelnemers opvraagt
- **THEN** is de status 403

#### Scenario: Tool zonder ledenservices
- **GIVEN** een tool met "Membership services" uit
- **WHEN** de tool de NRPS-scope vraagt
- **THEN** is de status 400 met `error` "invalid_scope"

### Requirement: Geen persoonsgegevens in logs bij NRPS
Doctrina SHALL bij NRPS-aanroepen geen `user_id`, namen of e-mailadressen loggen.

#### Scenario: Logs bij het ophalen van deelnemers
- **WHEN** een tool de deelnemers met naam en e-mail opvraagt
- **THEN** bevat geen logregel (ook niet op `Trace`) een `user_id`, naam of e-mailadres
