# lti-tool-registration Specification

## Purpose

Beheerders registreren de externe LTI 1.3-tools die Doctrina mag starten, en Doctrina publiceert de sleutels waarmee
een tool de launches kan controleren. Dit is de basis van het LTI-platform (zie `lti-launch`).

## Requirements

### Requirement: Beheerders registreren LTI-tools
De module Lti SHALL gebruikers met de permissie `Lti.Manage` toelaten tools te registreren, te wijzigen en te verwijderen op `/lti/tools`: naam, login-URL, launch-URL en redirect-URI's. Doctrina SHALL een `client_id` en `deployment_id` genereren en SHALL alleen `https://`-adressen toestaan (`http://` alleen voor `localhost`). Fouten hebben stabiele codes `Lti.Tool.*`.

#### Scenario: Tool registreren
- **GIVEN** een beheerder met `Lti.Manage` op `/lti/tools`
- **WHEN** de beheerder een tool "Quiz tool" opslaat met een login-URL, een launch-URL en een redirect-URI
- **THEN** staat de tool in de lijst met een gegenereerde `client_id` en `deployment_id`

#### Scenario: Tool zonder naam wordt geweigerd
- **GIVEN** een beheerder met `Lti.Manage`
- **WHEN** de beheerder een tool zonder naam opslaat
- **THEN** wordt niets opgeslagen en geeft de service de foutcode `Lti.Tool.NameRequired`

#### Scenario: Adres zonder https wordt geweigerd
- **GIVEN** een beheerder met `Lti.Manage`
- **WHEN** de beheerder een tool opslaat met een login-URL `http://tool.example.org/login`
- **THEN** wordt niets opgeslagen en geeft de service de foutcode `Lti.Tool.InvalidUrl`

#### Scenario: Http is toegestaan voor localhost
- **GIVEN** een beheerder met `Lti.Manage`
- **WHEN** de beheerder een tool opslaat waarvan alle adressen `http://localhost:…` zijn
- **THEN** wordt de tool opgeslagen

#### Scenario: Tool zonder redirect-URI wordt geweigerd
- **GIVEN** een beheerder met `Lti.Manage`
- **WHEN** de beheerder een tool opslaat zonder redirect-URI
- **THEN** wordt niets opgeslagen en geeft de service de foutcode `Lti.Tool.RedirectUriRequired`

#### Scenario: Tool wijzigen behoudt de identifiers
- **GIVEN** een geregistreerde tool
- **WHEN** de beheerder de naam en de adressen wijzigt
- **THEN** zijn `client_id` en `deployment_id` ongewijzigd

#### Scenario: Tool verwijderen
- **GIVEN** een geregistreerde tool
- **WHEN** de beheerder de tool verwijdert
- **THEN** staat de tool niet meer in de lijst en kan hij niet meer gestart worden

#### Scenario: Alleen beheerders beheren tools
- **GIVEN** een gebruiker zonder `Lti.Manage`
- **WHEN** de gebruiker `/lti/tools` opent
- **THEN** wordt de toegang geweigerd ("Access denied")

### Requirement: Persoonsgegevens per tool
Doctrina SHALL per tool bepalen welke persoonsgegevens in een launch meegaan: de naam (`name`, `given_name`,
`family_name`) en het e-mailadres (`email`). Beide SHALL standaard uit staan (AVG: dataminimalisatie).

#### Scenario: Nieuwe tool deelt standaard geen persoonsgegevens
- **GIVEN** een beheerder die een tool registreert zonder de opties aan te vinken
- **WHEN** de tool is opgeslagen
- **THEN** staat "Send name" uit en staat "Send email" uit

#### Scenario: Beheerder kiest welke gegevens meegaan
- **GIVEN** een geregistreerde tool
- **WHEN** de beheerder "Send name" aanzet en opslaat
- **THEN** is die keuze bewaard en is "Send email" nog uit

### Requirement: Platformgegevens voor de tool
Doctrina SHALL op de beheerpagina de gegevens tonen die een tool nodig heeft om Doctrina te configureren: de issuer,
het authenticatie-endpoint, de JWKS-URL, en per tool de `client_id` en de `deployment_id`. De issuer SHALL uit de
instelling `Lti:Issuer` komen en anders uit het adres waarop Doctrina bereikt wordt.

#### Scenario: Beheerpagina toont de platformgegevens
- **GIVEN** een beheerder en een geregistreerde tool
- **WHEN** de beheerder `/lti/tools` opent
- **THEN** toont de pagina de issuer, het authenticatie-endpoint en de JWKS-URL, en bij de tool zijn `client_id` en `deployment_id`

#### Scenario: Ingestelde issuer wordt gebruikt
- **GIVEN** `Lti:Issuer` is `https://lms.example.org`
- **WHEN** de beheerder `/lti/tools` opent
- **THEN** is de getoonde issuer `https://lms.example.org`

### Requirement: Doctrina publiceert eigen ondertekeningssleutels
De module SHALL RSA-sleutelparen (RS256) beheren, de privésleutel versleuteld opslaan en de publieke sleutels zonder inloggen publiceren als JWKS op `/lti/jwks.json`. Een beheerder SHALL de sleutel kunnen roteren; de vorige sleutel blijft `Lti:Keys:RetireAfterDays` dagen (standaard 7) in de JWKS. Is een privésleutel niet te ontsleutelen, dan SHALL Doctrina een nieuwe aanmaken.

#### Scenario: Eerste aanvraag maakt een sleutel aan
- **GIVEN** een lege database
- **WHEN** een bezoeker `/lti/jwks.json` opent
- **THEN** bevat de JWKS één RSA-sleutel met `kid`, `alg` RS256 en `use` sig

#### Scenario: JWKS bevat alleen publieke sleutelgegevens
- **GIVEN** een actieve sleutel
- **WHEN** een bezoeker `/lti/jwks.json` opent
- **THEN** bevat de JWKS de modulus en exponent en geen privéonderdelen (`d`, `p`, `q`, `dp`, `dq`, `qi`)

#### Scenario: JWKS is anoniem bereikbaar
- **GIVEN** een niet-ingelogde bezoeker
- **WHEN** de bezoeker `/lti/jwks.json` opent
- **THEN** is het antwoord 200 met `application/json`

#### Scenario: Privésleutel staat versleuteld in de database
- **GIVEN** een actieve sleutel
- **WHEN** de opgeslagen privésleutel uit de database wordt gelezen
- **THEN** is hij niet als RSA-sleutel te importeren zonder ontsleuteling

#### Scenario: Roteren maakt een nieuwe actieve sleutel
- **GIVEN** een actieve sleutel
- **WHEN** een beheerder "Rotate key" kiest
- **THEN** bevat de JWKS twee sleutels en ondertekent Doctrina voortaan met de nieuwe

#### Scenario: Oude sleutel verdwijnt na de bewaartermijn
- **GIVEN** een geroteerde sleutel waarvan de bewaartermijn is verstreken
- **WHEN** een bezoeker `/lti/jwks.json` opent
- **THEN** bevat de JWKS alleen de actieve sleutel

#### Scenario: Onleesbare privésleutel wordt vervangen
- **GIVEN** een actieve sleutel die niet meer te ontsleutelen is
- **WHEN** Doctrina een launch moet ondertekenen
- **THEN** wordt een nieuw sleutelpaar aangemaakt en gebruikt, en staat de onleesbare sleutel niet meer in de JWKS

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
