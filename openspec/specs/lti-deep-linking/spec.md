# lti-deep-linking Specification

## Purpose

Een docent kiest content in een externe LTI 1.3-tool (LTI Deep Linking 2.0) en voegt die als tool link aan een sectie
toe. Doctrina is daarbij het platform: het stuurt het verzoek, controleert het antwoord van de tool en laat de docent
de selectie bevestigen.

## Requirements

### Requirement: Een deep linking-selectie starten
`GET /lti/deep-linking/{section}/{tool}` SHALL een ingelogde gebruiker die de content mag beheren doorsturen naar de login-URL van de tool (login initiation). De daarop volgende `id_token` SHALL een `LtiDeepLinkingRequest` zijn met `deep_linking_settings` (return-URL, alleen `ltiResourceLink`, meerdere items, geen `auto_create`, ondoorzichtige `data`). Zonder beheerrecht SHALL het antwoord 403 zijn, bij een tool zonder deep linking of een onbekende sectie of tool 404.

#### Scenario: Docent start een selectie
- **GIVEN** een docent met content-beheerrechten, een sectie en een tool met deep linking
- **WHEN** de docent `/lti/deep-linking/{section}/{tool}` opent
- **THEN** is het antwoord een redirect naar de login-URL van de tool met `iss`, `login_hint`, `target_link_uri` (de deep linking URL), `lti_message_hint`, `client_id` en `lti_deployment_id`

#### Scenario: Het verzoek is een LtiDeepLinkingRequest
- **GIVEN** een docent die een selectie startte en de tool die zijn authenticatieverzoek terugstuurt
- **WHEN** de `id_token` wordt gelezen
- **THEN** is `message_type` `LtiDeepLinkingRequest`, bevat `deep_linking_settings` de `deep_link_return_url` `/lti/deep-linking/return`, `accept_types` `ltiResourceLink`, `accept_multiple` `true`, `auto_create` `false` en een `data`, en bevat het token de context en de rollen maar geen `resource_link`

#### Scenario: Student mag geen selectie starten
- **GIVEN** een student ingeschreven in de uitvoering
- **WHEN** de student `/lti/deep-linking/{section}/{tool}` opent
- **THEN** is het antwoord 403

#### Scenario: Tool zonder deep linking geeft 404
- **GIVEN** een tool zonder deep linking URL
- **WHEN** een docent `/lti/deep-linking/{section}/{tool}` opent
- **THEN** is het antwoord 404 met de foutcode `Lti.DeepLinking.NotSupported`

#### Scenario: Onbekende sectie geeft 404
- **GIVEN** een sectie die niet bestaat of is verwijderd
- **WHEN** een docent `/lti/deep-linking/{section}/{tool}` opent
- **THEN** is het antwoord 404

#### Scenario: Niet-ingelogde bezoeker moet eerst inloggen
- **GIVEN** een niet-ingelogde bezoeker
- **WHEN** de bezoeker `/lti/deep-linking/{section}/{tool}` opent
- **THEN** wordt de bezoeker naar het inloggen gestuurd

### Requirement: Het antwoord van de tool controleren
`POST /lti/deep-linking/return` SHALL zonder sessie een ondertekende JWT in het veld `JWT` alleen accepteren als de handtekening klopt met de JWKS van de tool, de claims (`iss`, `aud`, `exp`, `message_type`) kloppen, de `nonce` nieuw is en de `data` door Doctrina voor deze tool is uitgegeven (hoogstens 15 minuten oud). Alleen `ltiResourceLink`-items met een adres op de herkomst van de launch-URL SHALL worden bewaard. Bij een fout geeft Doctrina 400 met een code `Lti.DeepLinking.*`.

#### Scenario: Geldig antwoord wordt bewaard als selectie
- **GIVEN** een docent die een selectie startte en een tool die twee items kiest
- **WHEN** de tool een geldig ondertekend antwoord naar `/lti/deep-linking/return` post
- **THEN** bewaart Doctrina een wachtende selectie met twee items en is het antwoord een redirect naar de bevestigingspagina

#### Scenario: Onjuiste handtekening wordt geweigerd
- **GIVEN** een antwoord dat met een andere sleutel is ondertekend dan die in de JWKS van de tool
- **WHEN** het antwoord wordt gepost
- **THEN** is het antwoord 400 met de foutcode `Lti.DeepLinking.InvalidToken` en is er niets bewaard

#### Scenario: Verkeerde issuer of audience wordt geweigerd
- **GIVEN** een antwoord met een `iss` die niet de `client_id` is, of een `aud` die niet de issuer van Doctrina is
- **WHEN** het antwoord wordt gepost
- **THEN** is het antwoord 400 met de foutcode `Lti.DeepLinking.InvalidToken`

#### Scenario: Verlopen antwoord wordt geweigerd
- **GIVEN** een antwoord waarvan `exp` verstreken is
- **WHEN** het antwoord wordt gepost
- **THEN** is het antwoord 400 met de foutcode `Lti.DeepLinking.InvalidToken`

#### Scenario: Hergebruikt antwoord wordt geweigerd
- **GIVEN** een geldig antwoord dat al eerder is verwerkt
- **WHEN** hetzelfde antwoord opnieuw wordt gepost
- **THEN** is het antwoord 400 met de foutcode `Lti.DeepLinking.Replayed` en is er geen tweede selectie

#### Scenario: Vervalste of verlopen data wordt geweigerd
- **GIVEN** een antwoord met een `data` die niet door Doctrina is uitgegeven, of ouder is dan 15 minuten
- **WHEN** het antwoord wordt gepost
- **THEN** is het antwoord 400 met de foutcode `Lti.DeepLinking.InvalidData`

#### Scenario: Data van een andere tool wordt geweigerd
- **GIVEN** een geldig antwoord van tool B met de `data` van een selectie die voor tool A is gestart
- **WHEN** het antwoord wordt gepost
- **THEN** is het antwoord 400 met de foutcode `Lti.DeepLinking.InvalidData`

#### Scenario: Item met een ander adres dan de tool wordt geweigerd
- **GIVEN** een antwoord met een `ltiResourceLink` waarvan de `url` op een andere herkomst staat dan de launch-URL van de tool
- **WHEN** het antwoord wordt gepost
- **THEN** is het antwoord 400 met de foutcode `Lti.DeepLinking.InvalidItem` en is er niets bewaard

#### Scenario: Andere itemsoorten worden geweigerd
- **GIVEN** een antwoord met een item van het type `html`
- **WHEN** het antwoord wordt gepost
- **THEN** is het antwoord 400 met de foutcode `Lti.DeepLinking.InvalidItem`

#### Scenario: Antwoord zonder items geeft een lege selectie
- **GIVEN** een geldig antwoord zonder `content_items`
- **WHEN** het antwoord wordt gepost
- **THEN** is het antwoord een redirect naar de sectie zonder selectie en is er niets bewaard

### Requirement: De docent bevestigt de selectie
`GET /lti/deep-linking/selection/{id}` SHALL aan de ingelogde gebruiker die de selectie startte de gekozen titels tonen met de knop "Add to section"; die knop SHALL de items als concept-tool links onderaan de sectie toevoegen en de selectie verwijderen. Een verlopen of onbekende selectie SHALL 404 geven met `Lti.DeepLinking.SelectionNotFound`, een andere gebruiker 403 met `Lti.DeepLinking.UserMismatch`, een gebruiker die de content niet meer mag beheren 403.

#### Scenario: Docent voegt de selectie toe
- **GIVEN** een wachtende selectie met de titels "Week 1 quiz" en "Week 2 quiz"
- **WHEN** de docent de bevestigingspagina opent en "Add to section" kiest
- **THEN** staan "Week 1 quiz" en "Week 2 quiz" als concept-tool links van de tool onderaan de sectie en is de selectie verwijderd

#### Scenario: De bevestigingspagina toont de titels
- **GIVEN** een wachtende selectie met de titel "Week 1 quiz"
- **WHEN** de docent de bevestigingspagina opent
- **THEN** staat "Week 1 quiz" op de pagina en is er nog niets aan de sectie toegevoegd

#### Scenario: Student ziet de nieuwe tool link niet
- **GIVEN** een toegevoegde selectie
- **WHEN** een student de pagina van de uitvoering opent
- **THEN** staan de nieuwe tool links er niet, want ze zijn concepten

#### Scenario: Andere gebruiker mag de selectie niet bevestigen
- **GIVEN** een wachtende selectie van docent A
- **WHEN** docent B de bevestigingspagina opent
- **THEN** is het antwoord 403 met de foutcode `Lti.DeepLinking.UserMismatch`

#### Scenario: Verlopen selectie geeft 404
- **GIVEN** een selectie die ouder is dan 15 minuten
- **WHEN** de docent de bevestigingspagina opent
- **THEN** is het antwoord 404 met de foutcode `Lti.DeepLinking.SelectionNotFound`

#### Scenario: Verwijderde sectie voegt niets toe
- **GIVEN** een wachtende selectie waarvan de sectie intussen is verwijderd
- **WHEN** de docent "Add to section" kiest
- **THEN** wordt niets toegevoegd en geeft de service de foutcode `Content.Section.NotFound`

### Requirement: Een tool link met eigen adres start dat adres
Een tool link uit deep linking SHALL de gekozen `url` bewaren. De launch van zo'n tool link SHALL die `url` als `target_link_uri` gebruiken, in de login initiation en in de `id_token`; zonder eigen adres geldt de launch-URL van de tool.

#### Scenario: Launch gebruikt het gekozen adres
- **GIVEN** een tool link uit deep linking met het adres `https://quiz.example.org/launch?quiz=7`
- **WHEN** een student `/lti/launch/{item}` opent
- **THEN** is `target_link_uri` in de redirect naar de tool gelijk aan dat adres

#### Scenario: Token bevat het gekozen adres
- **GIVEN** een tool link uit deep linking met het adres `https://quiz.example.org/launch?quiz=7`
- **WHEN** de `id_token` van de launch wordt gelezen
- **THEN** is `target_link_uri` in het token gelijk aan dat adres

#### Scenario: Gekopieerde tool link behoudt het adres
- **GIVEN** een tool link uit deep linking in een eerdere uitvoering
- **WHEN** de content naar een lege uitvoering wordt gekopieerd
- **THEN** heeft de gekopieerde tool link hetzelfde adres

### Requirement: Geen persoonsgegevens in logs bij deep linking
De module SHALL bij deep linking geen namen, e-mailadressen, `login_hint`, `lti_message_hint`, `nonce`, `state`, `data`, tokens of de titels en adressen van gekozen items loggen.

#### Scenario: Deep linking laat geen persoonsgegevens achter in de logs
- **GIVEN** een volledige deep linking-selectie van een docent met naam en e-mailadres
- **WHEN** de logregels van de selectie worden doorzocht
- **THEN** komen naam, e-mailadres, hints, `nonce`, `state`, `data`, tokens, titels en adressen er niet in voor
