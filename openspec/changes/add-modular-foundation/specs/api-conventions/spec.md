# Spec-delta

## Purpose

Geeft de API van elke module dezelfde geversioneerde adresruimte, machineleesbare documentatie, foutopmaak en
operationele endpoints, zodat externe systemen op één consistente manier met alle modules kunnen koppelen.

## ADDED Requirements

### Requirement: Module-endpoints staan geversioneerd onder /api/v1
Alle API-endpoints die modules leveren SHALL onder het padprefix `/api/v1` worden aangeboden.

#### Scenario: Module-endpoint onder het versieprefix
- **GIVEN** een ingeschakelde module levert endpoint `GET samples`
- **WHEN** een client `GET /api/v1/samples` aanroept met een geldig token
- **THEN** het endpoint van de module handelt het verzoek af

### Requirement: De API is gedocumenteerd met OpenAPI
De host SHALL een OpenAPI-document publiceren dat alle endpoints van ingeschakelde modules beschrijft, inclusief hun
beveiligingseisen, en SHALL een interactieve Scalar-referentiepagina aanbieden. Beide SHALL beschikbaar zijn in
Development en SHALL buiten Development alleen aan staan als `Api:Documentation:Enabled` true is.

#### Scenario: OpenAPI-document toont module-endpoints
- **GIVEN** de omgeving Development en ingeschakelde module `Sample`
- **WHEN** een client het OpenAPI-document opvraagt
- **THEN** het beschrijft de endpoints van `Sample` onder `/api/v1` met het bearer-beveiligingsschema

#### Scenario: Documentatie standaard verborgen in productie
- **GIVEN** de omgeving Production zonder `Api:Documentation:Enabled`
- **WHEN** een client het OpenAPI-document of de Scalar-pagina opvraagt
- **THEN** de responsestatus is 404

### Requirement: Verwachte fouten worden als ProblemDetails teruggegeven
Een verwachte fout SHALL worden teruggegeven als RFC 9457 ProblemDetails-response met mediatype
`application/problem+json`, de status die bij het fouttype hoort en de stabiele foutcode in het extensieveld `code`.

#### Scenario: Fouttype bepaalt de status
- **WHEN** een endpoint faalt met fouttype Validation, Unauthorized, Forbidden, NotFound, Conflict of Failure
- **THEN** de responsestatus is respectievelijk 400, 401, 403, 404, 409 of 500 en `code` bevat de foutcode

#### Scenario: Validatiefouten per veld
- **GIVEN** een validatiefout met details voor de velden `email` en `givenName`
- **WHEN** een endpoint die fout teruggeeft
- **THEN** de ProblemDetails-body heeft status 400 en een veld `errors` met de meldingen per veld

### Requirement: Fouttitels zijn vertaald
De ProblemDetails-`title` SHALL de Nederlandse of Engelse vertaling van de foutcode zijn, gekozen op basis van de
`Accept-Language` van het verzoek met Nederlands als standaard. Als er geen vertaling bestaat, SHALL de eigen melding
van de fout gebruikt worden.

#### Scenario: Engelse titel gevraagd
- **GIVEN** foutcode `Sample.Item.NotFound` heeft een Nederlandse en een Engelse vertaling
- **WHEN** een client aanroept met `Accept-Language: en` en die fout krijgt
- **THEN** de `title` is de Engelse vertaling

#### Scenario: Ontbrekende vertaling valt terug op de melding
- **GIVEN** een foutcode zonder vertalingen en met melding "Item not found"
- **WHEN** een client die fout krijgt
- **THEN** de `title` is "Item not found"

### Requirement: Onverwachte fouten lekken geen details
Een onafgehandelde exception tijdens een API-verzoek SHALL status 500 opleveren met een generieke ProblemDetails-body
met een trace-id en code `Server.Unexpected`. Buiten Development SHALL NOT de body exceptionmeldingen of stacktraces
bevatten. De exception SHALL gelogd worden met dezelfde trace-id en zonder persoonsgegevens.

#### Scenario: Onafgehandelde exception in productie
- **GIVEN** de omgeving Production en een endpoint dat een exception gooit
- **WHEN** een client het aanroept
- **THEN** de response is 500 met `code` "Server.Unexpected" en een `traceId`, zonder exceptionmelding of stacktrace

### Requirement: Liveness- en readiness-endpoints
De host SHALL `/health/live` aanbieden, dat gezond meldt zolang het proces draait, en `/health/ready`, dat alleen
gezond meldt als de database en de checks van alle ingeschakelde modules gezond zijn. Beide SHALL zonder authenticatie
aanroepbaar zijn en SHALL NOT persoonsgegevens of connection strings tonen.

#### Scenario: Liveness terwijl de database onbereikbaar is
- **GIVEN** de database is onbereikbaar
- **WHEN** een client `/health/live` aanroept
- **THEN** de responsestatus is 200

#### Scenario: Readiness terwijl de database onbereikbaar is
- **GIVEN** de database is onbereikbaar
- **WHEN** een client `/health/ready` aanroept
- **THEN** de responsestatus is 503
