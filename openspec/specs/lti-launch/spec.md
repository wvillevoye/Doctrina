# lti-launch Specification

## Purpose

Deelnemers starten een externe LTI 1.3-tool vanuit een item in de contentstructuur van hun uitvoering, met de
launch-flow van LTI 1.3 (OIDC third-party login initiation, `id_token` per `form_post`). Doctrina is daarbij het platform.

## Requirements

### Requirement: Tool links in een sectie
Een gebruiker die content mag beheren SHALL in een sectie een item van de soort tool link kunnen toevoegen dat naar een
geregistreerde tool verwijst. De keuzelijst en het formulier SHALL alleen bestaan als de module Lti is ingeschakeld.
Een tool link SHALL publiceren, verbergen, hernoemen, verplaatsen en verwijderen zoals andere items. Deelnemers SHALL
een zichtbare tool link zien met de knop "Launch", die de tool in een nieuw venster opent.

#### Scenario: Docent voegt een tool link toe
- **GIVEN** een docent met content-beheerrechten, een sectie en een geregistreerde tool "Quiz tool"
- **WHEN** de docent op de beheerpagina een tool link met titel "Weekly quiz" en tool "Quiz tool" toevoegt
- **THEN** staat "Weekly quiz" als concept in de sectie

#### Scenario: Tool link naar een onbekende tool wordt geweigerd
- **GIVEN** een docent met content-beheerrechten
- **WHEN** een tool link wordt toegevoegd met een tool die niet bestaat of is verwijderd
- **THEN** wordt niets opgeslagen en geeft de service de foutcode `Content.Item.UnknownTool`

#### Scenario: Zonder LTI-module geen tool links
- **GIVEN** de module Lti is niet ingeschakeld
- **WHEN** een docent de beheerpagina van de content opent
- **THEN** is er geen formulier voor een tool link

#### Scenario: Student ziet een gepubliceerde tool link
- **GIVEN** een gepubliceerde tool link "Weekly quiz" in een gepubliceerde sectie
- **WHEN** een student de pagina van de uitvoering opent
- **THEN** staat "Weekly quiz" in de sectie "Content" met een knop "Launch"

#### Scenario: Concept-tool link is onzichtbaar voor studenten
- **GIVEN** een tool link die nog een concept is
- **WHEN** een student de pagina van de uitvoering opent
- **THEN** staat de tool link er niet

### Requirement: Launch starten met login initiation
`GET /lti/launch/{item}` SHALL een ingelogde gebruiker die de tool link mag lezen doorsturen naar de login-URL van de tool met de OIDC-parameters van LTI 1.3 (`iss`, `login_hint`, `target_link_uri`, `lti_message_hint`, `client_id`, `lti_deployment_id`). `login_hint` SHALL ondoorzichtig zijn en `lti_message_hint` SHALL 5 minuten geldig zijn. Onzichtbare, onbekende of verwijderde items SHALL 404 geven, een gebruiker zonder leesrecht 403.

#### Scenario: Deelnemer start een launch
- **GIVEN** een student ingeschreven in de uitvoering en een zichtbare tool link
- **WHEN** de student `/lti/launch/{item}` opent
- **THEN** is het antwoord een redirect naar de login-URL van de tool met `iss`, `login_hint`, `target_link_uri`, `lti_message_hint`, `client_id` en `lti_deployment_id`

#### Scenario: Login hint is ondoorzichtig
- **GIVEN** een student met een e-mailadres en een ExternalId
- **WHEN** de student een launch start
- **THEN** komen noch het e-mailadres noch de ExternalId in `login_hint` of `lti_message_hint` voor

#### Scenario: Concept-tool link start niet voor een student
- **GIVEN** een tool link die nog een concept is
- **WHEN** een student `/lti/launch/{item}` opent
- **THEN** is het antwoord 404

#### Scenario: Niet-deelnemer mag niet starten
- **GIVEN** een gebruiker die niet is ingeschreven in de uitvoering en geen `Content.Read` heeft
- **WHEN** de gebruiker `/lti/launch/{item}` opent
- **THEN** is het antwoord 403

#### Scenario: Launch naar een verwijderde tool geeft 404
- **GIVEN** een tool link waarvan de tool is verwijderd
- **WHEN** een student `/lti/launch/{item}` opent
- **THEN** is het antwoord 404 met de foutcode `Lti.Tool.NotFound`

#### Scenario: Item dat geen tool link is geeft 404
- **GIVEN** een pagina-item in een uitvoering
- **WHEN** een student `/lti/launch/{item}` opent met de id van dat item
- **THEN** is het antwoord 404

#### Scenario: Niet-ingelogde bezoeker moet eerst inloggen
- **GIVEN** een niet-ingelogde bezoeker
- **WHEN** de bezoeker `/lti/launch/{item}` opent
- **THEN** wordt de bezoeker naar het inloggen gestuurd

### Requirement: Authenticatieverzoek van de tool
`GET /lti/authorize` SHALL het authenticatieverzoek van de tool voor de ingelogde gebruiker beantwoorden met een formulier dat `id_token` en de ongewijzigde `state` naar de geregistreerde `redirect_uri` post. Bij een ongeldig of onvolledig verzoek SHALL Doctrina nooit doorsturen, maar een foutmelding met een code `Lti.Launch.*` tonen (400 of 403).

#### Scenario: Geldig verzoek geeft een formulier met id_token
- **GIVEN** een student die een launch startte en de parameters van de tool terugstuurt
- **WHEN** de student `/lti/authorize` opent met `client_id`, `redirect_uri`, `login_hint`, `lti_message_hint`, `nonce` en `state`
- **THEN** bevat het antwoord een formulier met `action` gelijk aan de `redirect_uri`, een veld `id_token` en een veld `state` met de oorspronkelijke waarde

#### Scenario: Onbekende client_id wordt geweigerd
- **GIVEN** een authenticatieverzoek met een `client_id` die niet bestaat
- **WHEN** de gebruiker `/lti/authorize` opent
- **THEN** is het antwoord 400 met de foutcode `Lti.Launch.UnknownClient` en is er geen redirect

#### Scenario: Niet-geregistreerde redirect-URI wordt geweigerd
- **GIVEN** een authenticatieverzoek met een `redirect_uri` die niet bij de tool geregistreerd is
- **WHEN** de gebruiker `/lti/authorize` opent
- **THEN** is het antwoord 400 met de foutcode `Lti.Launch.InvalidRedirectUri` en bevat het geen formulier

#### Scenario: Verlopen lti_message_hint wordt geweigerd
- **GIVEN** een `lti_message_hint` die ouder is dan 5 minuten
- **WHEN** de gebruiker `/lti/authorize` opent
- **THEN** is het antwoord 400 met de foutcode `Lti.Launch.InvalidHint`

#### Scenario: Vervalste lti_message_hint wordt geweigerd
- **GIVEN** een `lti_message_hint` die niet door Doctrina is uitgegeven
- **WHEN** de gebruiker `/lti/authorize` opent
- **THEN** is het antwoord 400 met de foutcode `Lti.Launch.InvalidHint`

#### Scenario: Hint van een andere gebruiker wordt geweigerd
- **GIVEN** een `lti_message_hint` en `login_hint` die voor student A zijn uitgegeven
- **WHEN** student B `/lti/authorize` opent met die waarden
- **THEN** is het antwoord 403 met de foutcode `Lti.Launch.UserMismatch`

#### Scenario: Ontbrekende nonce of state wordt geweigerd
- **GIVEN** een authenticatieverzoek zonder `nonce` of zonder `state`
- **WHEN** de gebruiker `/lti/authorize` opent
- **THEN** is het antwoord 400 met de foutcode `Lti.Launch.InvalidRequest`

#### Scenario: Onjuiste response_type of response_mode wordt geweigerd
- **GIVEN** een authenticatieverzoek met `response_type=code` of `response_mode=query`
- **WHEN** de gebruiker `/lti/authorize` opent
- **THEN** is het antwoord 400 met de foutcode `Lti.Launch.InvalidRequest`

#### Scenario: Item dat intussen onzichtbaar is, start niet
- **GIVEN** een geldig authenticatieverzoek voor een tool link die na de login initiation is verborgen
- **WHEN** de student `/lti/authorize` opent
- **THEN** is het antwoord 403 met de foutcode `Lti.Launch.Forbidden` en bevat het geen `id_token`

### Requirement: Inhoud van de id_token
De `id_token` SHALL een met RS256 ondertekende JWT zijn die met de JWKS te controleren is, met de verplichte OIDC- en LTI-claims voor een `LtiResourceLinkRequest`: de resource link (het item), de context (de uitvoering), de rollen van de gebruiker en een korte geldigheid (hoogstens 5 minuten). Naam en e-mail SHALL alleen meegaan als de tool daarvoor is ingesteld.

#### Scenario: Token is ondertekend en controleerbaar
- **GIVEN** een geslaagd authenticatieverzoek
- **WHEN** de `id_token` wordt gecontroleerd met de sleutels uit `/lti/jwks.json`
- **THEN** is de handtekening geldig, is het algoritme RS256 en komt de `kid` uit de header in de JWKS voor

#### Scenario: Token bevat de verplichte claims
- **GIVEN** een geslaagd authenticatieverzoek met `nonce` "n-123"
- **WHEN** de `id_token` wordt gelezen
- **THEN** staan `iss`, `aud` (de `client_id`), `sub`, `iat`, `exp`, `nonce` "n-123", `message_type` `LtiResourceLinkRequest`, `version` `1.3.0` en `deployment_id` in het token, en ligt `exp` hoogstens 5 minuten na `iat`

#### Scenario: Token bevat resource link en context
- **GIVEN** een tool link "Weekly quiz" in de uitvoering "Cell biology"
- **WHEN** de `id_token` wordt gelezen
- **THEN** bevat `resource_link` de id en de titel "Weekly quiz", bevat `context` de id, het label en de titel van de uitvoering met het type `CourseOffering`, en is `target_link_uri` de launch-URL van de tool

#### Scenario: Student krijgt de rol Learner
- **GIVEN** een student ingeschreven met de rol student
- **WHEN** de `id_token` wordt gelezen
- **THEN** bevat `roles` `http://purl.imsglobal.org/vocab/lis/v2/membership#Learner`

#### Scenario: Docent krijgt de rol Instructor
- **GIVEN** een gebruiker ingeschreven als lecturer of coordinator
- **WHEN** de `id_token` wordt gelezen
- **THEN** bevat `roles` `http://purl.imsglobal.org/vocab/lis/v2/membership#Instructor`

#### Scenario: Teaching assistant krijgt de rol TeachingAssistant
- **GIVEN** een gebruiker ingeschreven als teaching assistant
- **WHEN** de `id_token` wordt gelezen
- **THEN** bevat `roles` `http://purl.imsglobal.org/vocab/lis/v2/membership/Instructor#TeachingAssistant`

#### Scenario: Beheerder zonder inschrijving krijgt een institutierol
- **GIVEN** een beheerder die niet is ingeschreven in de uitvoering
- **WHEN** de `id_token` wordt gelezen
- **THEN** bevat `roles` `http://purl.imsglobal.org/vocab/lis/v2/institution/person#Administrator` en geen rol uit de uitvoering

#### Scenario: Zonder instelling geen naam en geen e-mail
- **GIVEN** een tool met "Send name" en "Send email" uit
- **WHEN** de `id_token` wordt gelezen
- **THEN** bevat het token geen `name`, `given_name`, `family_name` en `email`

#### Scenario: Naam en e-mail gaan mee als de tool dat vraagt
- **GIVEN** een tool met "Send name" en "Send email" aan, en een student met een naam en e-mailadres
- **WHEN** de `id_token` wordt gelezen
- **THEN** bevat het token `name`, `given_name`, `family_name` en `email` van de student

### Requirement: Geen persoonsgegevens in logs
De module SHALL bij het afhandelen van een launch geen namen, e-mailadressen, `login_hint`, `lti_message_hint`, `nonce`,
`state` of tokens loggen (AVG, ISO 27001).

#### Scenario: Launch laat geen persoonsgegevens achter in de logs
- **GIVEN** een volledige launch met een student met naam en e-mailadres
- **WHEN** de logregels van de launch worden doorzocht
- **THEN** komen naam, e-mailadres, `login_hint`, `lti_message_hint`, `nonce`, `state` en het token er niet in voor

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
