## ADDED Requirements

### Requirement: De id_token vermeldt de cijferservices
Voor een tool met cijferservices SHALL de `id_token` van een `LtiResourceLinkRequest` de claim
`https://purl.imsglobal.org/spec/lti-ags/claim/endpoint` bevatten met de toegestane `scope`-waarden, `lineitems` (de
URL van de line items van de uitvoering) en, als er een line item bij de resource link hoort, `lineitem` (de URL
daarvan). Een tool zonder cijferservices SHALL die claim niet krijgen. Een `LtiDeepLinkingRequest` bevat haar nooit.

#### Scenario: Launch van een tool met cijferservices
- **GIVEN** een tool met cijferservices en een tool link in "Cell biology"
- **WHEN** de `id_token` van de launch wordt gelezen
- **THEN** bevat de AGS-claim vier scopes en een `lineitems`-URL met het id van de uitvoering, en geen `lineitem`

#### Scenario: Launch met een bestaand line item
- **GIVEN** een line item dat de tool bij die resource link heeft aangemaakt
- **WHEN** de `id_token` van de launch wordt gelezen
- **THEN** bevat de AGS-claim ook `lineitem` met de URL van dat line item

#### Scenario: Tool zonder cijferservices krijgt geen AGS-claim
- **GIVEN** een tool met "Grade services" uit
- **WHEN** de `id_token` van de launch wordt gelezen
- **THEN** bevat het token geen AGS-claim
