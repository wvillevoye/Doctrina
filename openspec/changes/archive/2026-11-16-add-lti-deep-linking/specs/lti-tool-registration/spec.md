## ADDED Requirements

### Requirement: Deep linking per tool
Een tool SHALL optioneel een `Deep linking URL` (waar Doctrina het deep linking-verzoek naartoe stuurt) en een `Public keys URL` (JWKS van de tool) hebben. Beide SHALL samen worden ingevuld of samen leeg blijven en volgen dezelfde adresregels als de andere adressen. Fouten hebben de codes `Lti.Tool.*`.

#### Scenario: Tool met deep linking opslaan
- **GIVEN** een beheerder met `Lti.Manage` op `/lti/tools`
- **WHEN** de beheerder een tool opslaat met een deep linking URL en een public keys URL
- **THEN** zijn beide adressen bewaard en staat de tool in de lijst als geschikt voor deep linking

#### Scenario: Deep linking URL zonder public keys URL wordt geweigerd
- **GIVEN** een beheerder met `Lti.Manage`
- **WHEN** de beheerder een tool opslaat met alleen een deep linking URL
- **THEN** wordt niets opgeslagen en geeft de service de foutcode `Lti.Tool.PublicKeysUrlRequired`

#### Scenario: Public keys URL zonder deep linking URL wordt geweigerd
- **GIVEN** een beheerder met `Lti.Manage`
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
