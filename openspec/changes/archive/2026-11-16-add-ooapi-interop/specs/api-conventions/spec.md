## MODIFIED Requirements

### Requirement: Module-endpoints staan geversioneerd onder /api/v1
De eigen API-endpoints die modules leveren SHALL onder het padprefix `/api/v1` worden aangeboden. Een module die een
externe standaard-API implementeert (zoals OOAPI) MAY die onder het prefix aanbieden dat die standaard voorschrijft
(bv. `/ooapi/v5`), mits de endpoints dezelfde bearer-only beveiliging hebben als `/api/v1`: geen sessiecookie en geen
redirects.

#### Scenario: Module-endpoint onder het versieprefix
- **GIVEN** een ingeschakelde module levert endpoint `GET samples`
- **WHEN** een client `GET /api/v1/samples` aanroept met een geldig token
- **THEN** het endpoint van de module handelt het verzoek af

#### Scenario: Standaard-API onder eigen prefix
- **GIVEN** een ingeschakelde module die een standaard-API onder `/ooapi/v5` aanbiedt
- **WHEN** een client een endpoint daarvan aanroept met een geldig token
- **THEN** handelt de module het verzoek af, en geeft dezelfde aanroep met alleen een sessiecookie status 401
