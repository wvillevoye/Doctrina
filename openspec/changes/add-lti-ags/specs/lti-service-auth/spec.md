## Purpose

Een LTI-tool kan zich bij Doctrina authenticeren met een door de tool ondertekende `client_assertion` en krijgt een
kortlevend access token voor de LTI-services waarvoor ze is ingesteld. Dit is de gedeelde basis van AGS en latere services.

## ADDED Requirements

### Requirement: Een tool haalt een access token op met een client_assertion
`POST /lti/token` SHALL een `application/x-www-form-urlencoded`-verzoek met `grant_type=client_credentials`,
`client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer`, een `client_assertion` en een `scope`
(spatiegescheiden) beantwoorden met `access_token`, `token_type` "Bearer", `expires_in` (hoogstens 3600) en `scope`. De
`client_assertion` SHALL een RS256-JWT zijn van een actieve tool (`iss` = `sub` = `client_id`, `aud` = het
tokenendpoint, `exp` hoogstens 5 minuten na `iat`, uniek `jti`), gecontroleerd met de JWKS van de tool.

#### Scenario: Geldige assertion geeft een token
- **GIVEN** een tool met cijferservices en een `Public keys URL`
- **WHEN** de tool een geldige `client_assertion` stuurt met scope `https://purl.imsglobal.org/spec/lti-ags/scope/score`
- **THEN** is de status 200 met `token_type` "Bearer", een `access_token`, `expires_in` van hoogstens 3600 en `scope` met die scope

#### Scenario: Onjuiste handtekening wordt geweigerd
- **GIVEN** een `client_assertion` ondertekend met een andere sleutel dan die uit de JWKS van de tool
- **WHEN** de tool het token opvraagt
- **THEN** is de status 400 met `error` "invalid_client" en wordt er geen token uitgegeven

#### Scenario: Verkeerde audience wordt geweigerd
- **GIVEN** een `client_assertion` met een `aud` die niet het tokenendpoint van Doctrina is
- **WHEN** de tool het token opvraagt
- **THEN** is de status 400 met `error` "invalid_client"

#### Scenario: Verlopen assertion wordt geweigerd
- **GIVEN** een `client_assertion` waarvan `exp` meer dan een minuut geleden is verstreken
- **WHEN** de tool het token opvraagt
- **THEN** is de status 400 met `error` "invalid_client"

#### Scenario: Hergebruikte assertion wordt geweigerd
- **GIVEN** een `client_assertion` waarvan het `jti` al is gebruikt
- **WHEN** de tool hem nogmaals instuurt
- **THEN** is de status 400 met `error` "invalid_client" en wordt er geen tweede token uitgegeven

#### Scenario: Onbekende of uitgeschakelde tool
- **GIVEN** een `client_assertion` met een `iss` die niet bij een actieve tool hoort
- **WHEN** de tool het token opvraagt
- **THEN** is de status 400 met `error` "invalid_client"

### Requirement: Scopes worden beperkt tot wat de tool mag
Het token SHALL alleen de gevraagde scopes bevatten waarvoor de tool is ingesteld. Een vraag zonder enige toegestane
scope SHALL status 400 met `error` "invalid_scope" geven; bij een deels toegestane vraag SHALL het token alleen de
toegestane scopes bevatten. Een tool zonder cijferservices SHALL geen AGS-scope krijgen.

#### Scenario: Scope die niet is toegestaan
- **GIVEN** een tool zonder cijferservices
- **WHEN** de tool de scope `https://purl.imsglobal.org/spec/lti-ags/scope/score` vraagt
- **THEN** is de status 400 met `error` "invalid_scope"

#### Scenario: Deels toegestane scopes
- **GIVEN** een tool met cijferservices
- **WHEN** de tool de scopes `…/scope/score` en een onbekende scope vraagt
- **THEN** bevat het token alleen `…/scope/score`

### Requirement: Een access token geldt kort en voor één tool
Het access token SHALL een met de platformsleutel ondertekende JWT zijn die de tool (`sub` = `client_id`), de scopes
en een korte geldigheid bevat. Een LTI-service-endpoint SHALL een ontbrekend, verlopen, vervalst of voor een andere
scope uitgegeven token weigeren met 401 (ontbrekend, verlopen of vervalst) of 403 (onvoldoende scope).

#### Scenario: Zonder token
- **WHEN** een client een AGS-endpoint aanroept zonder `Authorization`-header
- **THEN** is de status 401

#### Scenario: Verlopen token
- **GIVEN** een access token waarvan `exp` is verstreken
- **WHEN** een tool er een AGS-endpoint mee aanroept
- **THEN** is de status 401

#### Scenario: Onvoldoende scope
- **GIVEN** een access token met alleen `…/scope/result.readonly`
- **WHEN** de tool een score post
- **THEN** is de status 403

### Requirement: Geen geheimen of persoonsgegevens in logs bij service-auth
Doctrina SHALL bij het tokenendpoint en de service-endpoints geen `client_assertion`, access token, `jti` of
persoonsgegevens loggen.

#### Scenario: Logs bij het ophalen van een token
- **WHEN** een tool een token ophaalt en er een service-endpoint mee wordt aangeroepen
- **THEN** bevat geen logregel (ook niet op `Trace`) de assertion, het token, het `jti` of een naam of e-mailadres
