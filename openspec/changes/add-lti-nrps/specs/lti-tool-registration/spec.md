## ADDED Requirements

### Requirement: Ledenservices per tool
Een tool SHALL een instelling "Membership services" hebben (standaard uit). Staat ze aan, dan SHALL de tool een `Public
keys URL` hebben (de foutcode is anders `Lti.Tool.PublicKeysUrlRequired`) en SHALL Doctrina haar de NRPS-scope
`contextmembership.readonly` toestaan. De naam en het e-mailadres van deelnemers gaan alleen mee volgens de bestaande
instellingen "Send name" en "Send email".

#### Scenario: Tool met ledenservices opslaan
- **GIVEN** een beheerder met `Lti.Manage`
- **WHEN** de beheerder een tool opslaat met "Membership services" aan en een public keys URL
- **THEN** is de instelling bewaard en staat de tool als geschikt voor ledenservices in de lijst

#### Scenario: Ledenservices zonder public keys URL wordt geweigerd
- **GIVEN** een beheerder met `Lti.Manage`
- **WHEN** de beheerder een tool opslaat met "Membership services" aan en zonder public keys URL
- **THEN** wordt niets opgeslagen en geeft de service de foutcode `Lti.Tool.PublicKeysUrlRequired`

#### Scenario: Bestaande tool heeft geen ledenservices
- **GIVEN** een tool die vóór deze change is geregistreerd
- **WHEN** de tool de NRPS-scope vraagt
- **THEN** wordt die scope geweigerd
