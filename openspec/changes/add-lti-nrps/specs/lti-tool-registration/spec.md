## MODIFIED Requirements

### Requirement: Deep linking per tool
Een tool SHALL optioneel een `Deep linking URL` (waar Doctrina het deep linking-verzoek naartoe stuurt) en een `Public keys URL` (JWKS van de tool) hebben. Een deep linking URL SHALL altijd samen met een public keys URL worden ingevuld; een public keys URL MAG ook zonder deep linking URL bestaan als de tool cijferservices of ledenservices heeft. Alle adressen volgen dezelfde adresregels als de andere adressen. Fouten hebben de codes `Lti.Tool.*`.

#### Scenario: Tool met deep linking opslaan
- **GIVEN** een beheerder met `Lti.Manage` op `/lti/tools`
- **WHEN** de beheerder een tool opslaat met een deep linking URL en een public keys URL
- **THEN** zijn beide adressen bewaard en staat de tool in de lijst als geschikt voor deep linking

#### Scenario: Deep linking URL zonder public keys URL wordt geweigerd
- **GIVEN** een beheerder met `Lti.Manage`
- **WHEN** de beheerder een tool opslaat met alleen een deep linking URL
- **THEN** wordt niets opgeslagen en geeft de service de foutcode `Lti.Tool.PublicKeysUrlRequired`

#### Scenario: Public keys URL zonder deep linking URL wordt geweigerd
- **GIVEN** een beheerder met `Lti.Manage` en een tool met cijferservices en ledenservices uit
- **WHEN** de beheerder een tool opslaat met alleen een public keys URL
- **THEN** wordt niets opgeslagen en geeft de service de foutcode `Lti.Tool.DeepLinkingUrlRequired`

#### Scenario: Deep linking-adres zonder https wordt geweigerd
- **GIVEN** een beheerder met `Lti.Manage`
- **WHEN** de beheerder een tool opslaat met een public keys URL `http://tool.example.org/jwks`
- **THEN** wordt niets opgeslagen en geeft de service de foutcode `Lti.Tool.InvalidUrl`

#### Scenario: Bestaande tool blijft werken zonder deep linking
- **GIVEN** een tool die vóór deze change is geregistreerd
- **WHEN** een student een tool link van die tool start
- **THEN** werkt de launch ongewijzigd en biedt de beheerpagina van de content geen "Add from tool" voor die tool

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
