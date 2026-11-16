# Design

## Context

Zie `proposal.md` voor aanleiding en scope. Waar deze change op voortbouwt (alles gelezen in de code):

- **Lti-module** (`add-lti-platform`, `add-lti-deep-linking`): `LtiTool` heeft `ClientId`, `DeploymentId`, adressen en
  (sinds deep linking) `PublicKeysUrl`. `KeyService` ondertekent met de actieve platformsleutel (RS256). `JwksClient`
  haalt de JWKS van een tool veilig op (https, 5 s, geen redirects, 64 kB, cache) en `DeepLinkingService` controleert er
  een door de tool ondertekende JWT mee, inclusief het eenmalig gebruik van `nonce` (unieke index per tool).
- **`sub` in de `id_token`** is `LaunchService.SubjectAsync`: de `PersonId` (formaat "N") van de ingelogde persoon, en
  alleen voor gebruikers zonder persoon een hash. Een score met `userId` = `sub` wijst dus direct een persoon aan.
- **Resource link** in de launch is de id van het contentitem (`ToolLinkInfo.ItemId`); `ToolLinkInfo` en
  `IContentDirectory` staan in `Content.Contracts`. De context is de offering (`OfferingInfo.Id`).
- **Modulesysteem**: `IModule.MapBrowserEndpoints` mapt op de root van de app (zo staan `/lti/jwks.json` en
  `/lti/authorize`); `MapEndpoints` is alleen voor `/api/v1`, dat tokens van de IdP (Keycloak) valideert. Tokens van
  Doctrina zelf kunnen dus niet via `/api/v1`.
- **Pagina's van een module op de offering-pagina** lopen via `OfferingPageSection` in `Academic.Contracts`.
- Het voorstel noemt "LTI-cijfer-retries" met Hangfire (§3); dat gaat over terugsturen naar een tool en komt niet voor
  zolang Doctrina alleen ontvangt (zie Non-Goals).

Keuzes van de gebruiker vóór dit ontwerp (2026-11-16): scores worden bewaard én getoond; de volledige AGS-kern (line items,
scores, results).

## Goals / Non-Goals

**Goals:**
- Een standaard AGS-tool (bv. de 1EdTech-referentietool) kan line items maken, scores posten en results lezen.
- Student en docent zien de scores bij de tool link.
- Een tokenendpoint dat ook NRPS (8c) gebruikt, zonder het opnieuw te bouwen.

**Non-Goals:**
- Cijferlijst, eindcijfers, koppeling met `Association`-resultaten of het SIS.
- Terugschrijven naar de tool en dus geen Hangfire-retries.
- Per-scope toestemming per tool; dynamische registratie.

## Decisions

### D1: Tokenendpoint `POST /lti/token` met `client_assertion`

Volgens het LTI Advantage Security Framework (client credentials + `private_key_jwt`). Controle in vaste volgorde:
formaat → tool opzoeken op `iss` → handtekening met de JWKS van de tool (hergebruik `JwksClient`) → `iss`/`sub` =
`client_id`, `aud` = het tokenendpoint, `exp` binnen 5 minuten na `iat` (60 s speling) → `jti` eenmalig → scopes. Elke
fout in de assertion geeft `invalid_client` (400, zonder te verraden welke controle faalde); een scopefout
`invalid_scope`. Het eenmalig gebruik van `jti` hergebruikt het mechanisme van de deep linking-nonces: de tabel
`DeepLinkNonce` wordt `JwtNonce` met een kolom `Purpose` (migratie), zodat één opruimactie en één unieke index
volstaan.

*Alternatief:* een tweede tabel. Verworpen: zelfde vorm, zelfde opruimregel.

### D2: Access token is een JWT, door het platform ondertekend

RS256 met de actieve platformsleutel (`KeyService`), `sub` = `client_id`, `scope` (spatiegescheiden), `aud` = de
issuer van Doctrina, `exp` 1 uur. Stateloos: geen tokentabel, en een sleutelrotatie werkt door (oude sleutels blijven
7 dagen in de JWKS). Een service-endpoint valideert het met dezelfde sleutels en leest er de tool en scopes uit.

*Alternatief:* ondoorzichtig token in de database. Verworpen: extra tabel en opruiming zonder voordeel hier.

### D3: AGS-endpoints onder `/lti/ags` op de root, met eigen tokencontrole

`MapBrowserEndpoints` mapt `/lti/token` en `/lti/ags/{context}/lineitems`, `…/{lineitem}`, `…/{lineitem}/scores` en
`…/{lineitem}/results`. Ze zijn voor de host anoniem (geen cookie, geen `ApiCaller`) en autoriseren zelf met het
access token (D2). Antiforgery is uit (geen cookie). De hoofdrol van de URL: de `{context}` is de offering-id.
`{lineitem}` is de line item-id (Guid, "D").

### D4: Welke line items mag een tool zien

Een tool werkt alleen in uitvoeringen waarin ze minstens één tool link heeft en alleen op line items die ze zelf heeft
aangemaakt (`LineItem.ToolId`). Content krijgt voor die vraag één methode in `IContentDirectory`
(`ToolLinksOfToolAsync(offeringId, toolId)`: de items van die tool in die offering, of leeg); een `resourceLinkId` moet
een item uit dat resultaat zijn. Onbekend of van een andere tool geeft overal 404, zodat het bestaan niet lekt.

### D5: Datamodel (schema `lti`, migratie `AddGradeServices`)

- `LineItem` (uitwisselbaar, huisregel 6: `Id`, `ExternalId`, `Status`, `DateLastModified`, `Source`): `ToolId`,
  `OfferingId`, `ResourceLinkId?` (contentitem), `Label`, `ScoreMaximum` (decimal), `ResourceId?`, `Tag?`,
  `StartDateTime?`, `EndDateTime?`. De publieke `id` is de URL met de Guid.
- `Score` (niet uitwisselbaar): `LineItemId`, `PersonId` (de `userId`), `ScoreGiven?`, `ScoreMaximum`, `Comment?`,
  `ActivityProgress`, `GradingProgress`, `Timestamp`. Eén rij per (line item, persoon): unieke index; een nieuwere
  `Timestamp` vervangt de rij, een oudere geeft 409.
- `LtiTool.GradeServices` (bool, standaard onwaar; bestaande tools blijven uit).
- `DeepLinkNonce` → `JwtNonce` met `Purpose` (D1).

`Score` verwijst naar de persoon alleen met de `PersonId` (geen naam, mail of externe id); namen komen bij het tonen uit
`IAcademicDirectory`.

### D6: Validatie

Eén `GradeService` met de regels (positief maximum; `scoreGiven` en `scoreMaximum` samen; `scoreGiven` niet negatief;
bekende waarden van `activityProgress` en `gradingProgress`; `comment` ≤ 1000; `userId` is een lid van de uitvoering via
`IAcademicDirectory.MembersAsync`), met foutcodes `Lti.Ags.*` en titels in `LtiErrors.resx`/`.en.resx`; de endpoints
vertalen naar de statuscodes van de standaard (404 onbekend, 409 oudere score, 422 ongeldig). Fouten van AGS zijn
ProblemDetails met `code`, zoals de rest van Doctrina.

### D7: Tonen

- **Student**: een sectie op de offering-pagina ("Scores", via `OfferingPageSection`, alleen zichtbaar als de student
  scores heeft) met tool link, label en `score / maximum`, alleen de eigen rijen.
- **Docent**: een pagina `/lti/ags/results/{itemId}` per tool link met de line items en per ingeschreven student de score
  (naam via `IAcademicDirectory`); alleen voor wie de content mag beheren (`FindSectionForManaging`-regel uit Content).
  De beheerpagina van de content krijgt een link "Scores" bij tool links van tools met cijferservices.

### D8: Launch-claim

`IdToken.Payload` voegt voor tools met `GradeServices` de claim `…/lti-ags/claim/endpoint` toe: `scope` (vier scopes),
`lineitems` = `{issuer}/lti/ags/{offering}/lineitems` en, als er een line item met `ResourceLinkId` = het item is
(`lineitem` = `{lineitems}/{id}`). De deep linking-request krijgt hem niet.

### D9: Geen persoonsgegevens in logs

Zoals bij de eerdere LTI-changes: niets van `userId`, score, opmerking, naam, assertion of token wordt gelogd; getest
met `CapturingLoggerProvider` op `Trace`.

## Risks / Trade-offs

- **Zelf een OAuth-tokenendpoint bouwen** is beveiligingsgevoelig. Beperking: het klein houden (alleen client
  credentials, alleen de ene `client_assertion`-route), alle controles testen met een testtool met eigen sleutelpaar, en
  geen detail naar de client bij fouten (`invalid_client`).
- **JWKS van de tool wordt server-side opgehaald** (zoals bij deep linking): alleen tools vertrouwen die de beheerder
  registreert; zelfde begrenzingen als `JwksClient`.
- **Vervangende scores**: één rij per student per line item bewaart geen geschiedenis. Voldoende voor tonen; een
  scorelog is een latere uitbreiding als docenten dat vragen.
- **`ScoreMaximum` per score én per line item**: een tool kan een ander maximum meesturen dan het line item; we bewaren
  wat ze stuurt en tonen dat, en laten het line item-maximum alleen als standaard dienen.
- **`sub` voor gebruikers zonder persoon** (een beheerder) is een hash; zo iemand kan geen score krijgen (`userId` moet
  een lid van de uitvoering zijn).

## Open vragen

- Of docenten scores ook handmatig moeten kunnen aanpassen of verwijderen (nu alleen de tool). Voorlopig niet.
