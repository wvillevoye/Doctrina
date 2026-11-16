## ADDED Requirements

### Requirement: De id_token vermeldt de ledenservices
Voor een tool met ledenservices SHALL de `id_token` van een `LtiResourceLinkRequest` de claim
`https://purl.imsglobal.org/spec/lti-nrps/claim/namesroleservice` bevatten met `context_memberships_url` (de URL van de
deelnemerslijst van de uitvoering) en `service_versions` `["2.0"]`. Een tool zonder ledenservices SHALL die claim niet krijgen,
en een `LtiDeepLinkingRequest` bevat haar nooit.

#### Scenario: Launch van een tool met ledenservices
- **GIVEN** een tool met ledenservices en een tool link in "Cell biology"
- **WHEN** de `id_token` van de launch wordt gelezen
- **THEN** bevat de NRPS-claim een `context_memberships_url` met het id van de uitvoering en `service_versions` `["2.0"]`

#### Scenario: Tool zonder ledenservices krijgt geen NRPS-claim
- **GIVEN** een tool met "Membership services" uit
- **WHEN** de `id_token` van de launch wordt gelezen
- **THEN** bevat het token geen NRPS-claim
