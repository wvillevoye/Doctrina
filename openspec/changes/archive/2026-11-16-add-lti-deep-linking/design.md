# Design

## Context

Zie `proposal.md` voor aanleiding en scope. Waar deze change op voortbouwt (gelezen in de code):

- **Lti** (`add-lti-platform`): `LtiTool` heeft `ClientId`, `DeploymentId`, `LoginUrl`, `LaunchUrl`, `RedirectUris`,
  `SendName`, `SendEmail`. `LaunchService.StartAsync` doet de login initiation (hint = Data Protection, `{item}.{subject}.{expires}`,
  5 minuten); `AuthorizeAsync` controleert het authenticatieverzoek en geeft `LaunchForm` (auto-post naar de `redirect_uri`).
  `IdToken` bouwt de `LtiResourceLinkRequest`. `KeyService` tekent met RS256. Niets persoonlijks in de logs.
- **Content**: `ContentItem` (soort `ToolLink`) heeft `ToolId`; `Url` is bij een tool link nog ongebruikt.
  `IContentDirectory.FindToolLinkAsync` (Content.Contracts) is de enige ingang van Lti in Content; `ContentAccess.CanManageAsync`
  beslist wie mag beheren. `CopyFromAsync` kopieert `Url` al mee.
- **Voorstel**: §7 noemt Deep Linking 2.0 (docent kiest content in de tool) als onderdeel van LTI Advantage, rij 8a.
  Er is geen afwijking van het voorstel.

Er is geen sessie op een cross-site POST van de tool: het sessiecookie (SameSite Lax) wordt niet meegestuurd. Daarom
wordt het antwoord eerst bewaard en daarna door de ingelogde docent bevestigd (D4).

## Goals / Non-Goals

**Goals:** een docent kiest één of meer `ltiResourceLink`-items in een tool en voegt ze met één bevestiging als concept
toe aan een sectie; Doctrina vertrouwt het antwoord alleen na controle van handtekening en eigen `data`.

**Non-Goals:** zie "Buiten de scope" in het voorstel (andere itemsoorten, custom-parameters, AGS/NRPS, auto_create,
dynamic registration).

## Decisions

### D1: Twee extra adressen per tool

`LtiTool` krijgt `DeepLinkingUrl` en `PublicKeysUrl` (beide `string?`), samen leeg of samen gevuld (`Lti.Tool.DeepLinkingUrlRequired`,
`Lti.Tool.PublicKeysUrlRequired`), met dezelfde adresregels als de andere adressen (https, of http voor localhost,
`Lti.Tool.InvalidUrl`). Migratie in schema `lti`. Een bestaande tool heeft beide leeg: de launch verandert niet.
`ILtiToolCatalog` krijgt `LtiToolSummary.SupportsDeepLinking` zodat Content alleen geschikte tools toont.

### D2: Het verzoek

`GET /lti/deep-linking/{section}/{tool}` (in `LtiEndpoints`, achter de bestaande inlogplicht) vraagt via een nieuwe
Content-methode `IContentDirectory.FindSectionAsync(user, sectionId)` of de gebruiker de sectie mag beheren en wat de uitvoering
is. Dan volgt dezelfde login initiation als bij een launch, met `target_link_uri` = `DeepLinkingUrl`. De hint van
`LaunchService` krijgt een soort-veld (`resource` of `deeplink`) en een doel (item of sectie), zodat één hint-formaat
en één `AuthorizeAsync` volstaan; de `id_token` is dan een `LtiDeepLinkingRequest` met `deep_linking_settings`:

- `deep_link_return_url`: `{issuer}/lti/deep-linking/return`; `accept_types` `["ltiResourceLink"]`;
  `accept_presentation_document_targets` `["window"]`; `accept_multiple` `true`; `auto_create` `false`.
- `data`: Data Protection (doel `Lti.DeepLinking.Data`) over `{section}.{tool}.{subject}.{expires}`, 15 minuten geldig.
  De tool geeft hem ongewijzigd terug in het antwoord (`https://purl.imsglobal.org/spec/lti-dl/claim/data`).

Het token bevat context en rollen als bij een launch, maar geen `resource_link`.

### D3: Het antwoord controleren

`POST /lti/deep-linking/return` is anoniem (`AllowAnonymous`; antiforgery uitgezet voor dit ene endpoint, want de
bescherming is de handtekening) en leest het formulierveld `JWT`. Volgorde:

1. Lees de ongecontroleerde claims alleen voor `iss` en `kid`; zoek de tool op `client_id` = `iss` (`Lti.DeepLinking.InvalidToken` als hij ontbreekt).
2. Haal de JWKS van de tool op (D6), valideer de handtekening (alleen RS256), `aud` = issuer, `exp` en `iat` (tolerantie 60 s).
3. `message_type` = `LtiDeepLinkingResponse`, `version` = `1.3.0`, `deployment_id` = die van de tool.
4. `data` ontsleutelen (`Lti.DeepLinking.InvalidData` bij fout, verlopen of een andere tool dan `iss`).
5. `nonce` is niet eerder gezien: tabel `DeepLinkNonce` (`Nonce`, `ToolId`, `Expires`), unieke index op (`ToolId`, `Nonce`);
   een tweede insert geeft `Lti.DeepLinking.Replayed`. Verlopen rijen worden bij elk antwoord opgeruimd.
6. Elk item moet `type` `ltiResourceLink` hebben en een `url` op dezelfde herkomst (schema, host, poort) als `LaunchUrl`
   (`Lti.DeepLinking.InvalidItem`); titel hoogstens 200 tekens, leeg valt terug op de naam van de tool. Hoogstens 50 items.

Het antwoord geen items: redirect naar de beheerpagina van de sectie zonder selectie.

### D4: Wachtende selectie en bevestiging

Tabel `DeepLinkSelection` in `LtiDbContext`: `Id` (EF), `ToolId`, `SectionId`, `Subject` (de ondoorzichtige gebruikers-id uit de
hint), `ItemsJson` (titel en url per item), `Expires` (15 minuten), met de ExchangeableEntity-velden van de huisregels.
Het antwoord maakt één rij en stuurt (303) naar `/lti/deep-linking/selection/{id}`. Die pagina (Razor, `Dc*`-componenten)
vraagt de gebruiker in te loggen als dat nog niet zo is, vergelijkt het subject met de ingelogde gebruiker
(`Lti.DeepLinking.UserMismatch`), toont de titels en de knop "Add to section". De knop roept
`IContentDirectory.AddToolLinksAsync(user, sectionId, toolId, items)` aan en verwijdert de rij. Content controleert zelf
opnieuw of de gebruiker mag beheren (`Content.Access.Forbidden`) en of de sectie bestaat (`Content.Section.NotFound`).
Waarom niet direct toevoegen: er is geen sessie op de POST, en de docent ziet zo wat er komt.

Verlopen selecties worden bij het aanmaken van een nieuwe opgeruimd (geen achtergrondjob).

### D5: Tool link met eigen adres

`AddToolLinksAsync` maakt `ContentItem`s van soort `ToolLink` met `ToolId`, titel en `Url` = het gekozen adres, als
concept, onderaan de sectie. `ToolLinkInfo` krijgt `LaunchUrl` (`Url` of null). `LaunchService` gebruikt
`LaunchUrl ?? tool.LaunchUrl` voor `target_link_uri`. Geen nieuwe kolom: `Url` bestaat en `CopyFromAsync` kopieert hem al.
Een handmatig gemaakte tool link heeft `Url` = null en gedraagt zich als voorheen.

### D6: De JWKS van de tool ophalen (SSRF en beschikbaarheid)

De beheerder voert het adres in, maar Doctrina haalt het op vanaf de server; dat is een SSRF-risico. Maatregelen:
de adresregels van D1 (https; http alleen voor localhost), een `HttpClient` (`IHttpClientFactory`) met 5 seconden
timeout, geen redirects, antwoord hoogstens 64 kB en `application/json`, en een geheugencache van 5 minuten per tool
(bij een onbekende `kid` één keer opnieuw ophalen). Er worden geen andere adressen opgehaald dan de `PublicKeysUrl`
van een geregistreerde tool. Bij een fout geeft het antwoord `Lti.DeepLinking.InvalidToken` en wordt niets bewaard.
Een intern netwerkbereik blokkeren is hier niet ingebouwd: de bron is een beheerder (`Lti.Manage`), niet de tool zelf.

### D7: Foutcodes en logging

Nieuwe codes (titels in `LtiErrors.resx`/`.en.resx` en in `LtiErrors.All`): `Lti.DeepLinking.NotSupported` (404),
`InvalidToken`, `InvalidData`, `InvalidItem` (400), `Replayed` (400), `SelectionNotFound` (404), `UserMismatch` (403),
`Lti.Tool.DeepLinkingUrlRequired`, `Lti.Tool.PublicKeysUrlRequired` (validatie). Hergebruik: `Content.Access.Forbidden`,
`Content.Section.NotFound`. Net als bij de launch worden geen namen, e-mailadressen, hints, `nonce`, `state`, `data`,
tokens, titels of adressen gelogd; de test gebruikt `CapturingLoggerProvider` met `Trace`.

### D8: De knop op de beheerpagina

`ManageContent.razor` toont per sectie een keuzelijst met de tools met `SupportsDeepLinking` en de knop "Add from tool",
een gewone link naar `/lti/deep-linking/{section}/{tool}` (volledige navigatie, geen Blazor-circuit), alleen als de module Lti is
ingeschakeld (zoals het bestaande formulier voor een tool link) en er zulke tools zijn.

## Risks / Trade-offs

- **Een kwaadwillende tool** kan titels en adressen kiezen: beperkt tot dezelfde herkomst als de launch-URL, concepten, en de
  docent bevestigt. De titel wordt alleen als tekst getoond (geen Markdown of HTML).
- **JWKS-opvraag** (D6) hangt van de beschikbaarheid van de tool af; de docent probeert het opnieuw.
- **Gelijktijdige antwoorden met dezelfde nonce**: de unieke index laat er één door.
- Er is geen "undo": een verkeerde selectie verwijdert de docent met de bestaande knoppen.

## Migration Plan

Eén migratie in schema `lti` (twee kolommen op `LtiTool`, twee tabellen). Geen gegevensmigratie; bestaande tools behouden
hun gedrag. Terugdraaien kan door de migratie terug te draaien.

## Open Questions

- Geen. De tool-JWKS-opvraag blokkeert geen interne adressen (D6); dat kan later met een instelling als de beheerders niet langer
  vertrouwd worden.
