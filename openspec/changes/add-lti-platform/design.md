# Design

## Context

Zie `proposal.md` voor aanleiding en scope. Waar deze change op voortbouwt:

- **Content** (`add-content`): secties met items van de soorten `Page`, `File` en `Link` in één tabel `content.Items`;
  `ContentAccess` (intern) beslist wie mag beheren en lezen; `ContentService.SectionsAsync` bepaalt wat zichtbaar is.
  Het design van `add-content` (D1, D2, D9) kondigde aan dat LTI een vierde `Kind` wordt en dat het `.Contracts`-project
  van Content bij de change komt die het nodig heeft: dat is nu.
- **Modulesysteem**: `IModule.MapEndpoints` geeft alleen de `/api/v1`-groep met bearer-tokens (`ApiCaller`-policy). Een LTI
  launch is een browserstroom met de inlogcookie, en de JWKS moet anoniem bereikbaar zijn; beide kunnen daar niet (zie
  ook D6 van `add-content`).
- **Authenticatie**: pagina's autoriseren zelf; er is geen fallback-policy. De ingelogde gebruiker is een
  `ClaimsPrincipal` met cookie; `IAcademicDirectory` geeft de persoon (`CurrentPersonIdAsync`), de uitvoering
  (`FindOfferingAsync`) en de rollen (`CurrentRolesAsync`).
- **Libraries**: de host gebruikt al `Microsoft.IdentityModel.*` (via OIDC en JwtBearer). Er is geen goede officiële
  .NET-platformbibliotheek voor LTI (voorstel §6.5): de stroom is klein genoeg om zelf te bouwen met
  `Microsoft.IdentityModel.JsonWebTokens`.
- **Data Protection** is niet apart ingericht (standaardinstellingen van ASP.NET Core).

Keuze van de gebruiker vóór dit ontwerp (2026-11-14): alleen de core in deze change; Deep Linking, AGS en NRPS later.

## Goals / Non-Goals

**Goals:**
- Een tool starten vanuit een item in een sectie, volgens LTI 1.3 (`LtiResourceLinkRequest`), zodat een standaard tool
  (bv. de 1EdTech-referentietool) werkt.
- Een opzet waarop Deep Linking, AGS en NRPS later kunnen voortbouwen zonder het model om te gooien (eigen sleutels,
  registratie met `client_id`/`deployment_id`, rolmapping).

**Non-Goals:**
- Controleren van berichten van de tool (de JWKS van de tool, `client_assertion`): pas nodig voor Deep Linking en
  services (D9).
- Een algemeen mechanisme om items van andere modules in de contentstructuur te hangen: één nieuwe itemsoort, één
  contract (D4).

## Decisions

### D1: Module `Lti` met twee contractprojecten

`src/Modules/Lti/Doctrina.Modules.Lti` met schema `lti`, `DependsOn` Academic en Content, permissie `Lti.Manage`, menu-item
"LTI tools" (alleen met `Lti.Manage`), health check en fouttitels (`LtiErrors.resx`/`.en.resx`), zoals Groups en Content.

Twee `.Contracts`-projecten:
- **`Doctrina.Modules.Content.Contracts`**: `IContentDirectory.FindToolLinkAsync(user, itemId)` → `Result<ToolLinkInfo>`
  (`ItemId`, `OfferingId`, `Title`, `ToolId`); `Content.Item.NotFound` voor een item dat niet bestaat, geen tool link is
  of voor deze gebruiker niet zichtbaar is, en `Content.Access.Forbidden` zonder leesrecht. Zo blijft de zichtbaarheids- en
  leesregel van Content op één plek (`ContentService`/`ContentAccess`), en dupliceert Lti ze niet.
- **`Doctrina.Modules.Lti.Contracts`**: `ILtiToolCatalog` met `ListAsync()` (id en naam van actieve tools) en
  `ExistsAsync(toolId)`. Content gebruikt dit voor het formulier en de controle op `ToolId`.

Content verwijst dus naar `Lti.Contracts` en Lti naar `Content.Contracts`; niet naar elkaars implementatie. Alleen Lti
heeft `DependsOn` Content; Content hangt niet af van Lti en vraagt `ILtiToolCatalog` optioneel op (`GetService`): zonder
module Lti is er geen formulier voor tool links. De architectuurtests bewaken de richting.

### D2: Browser-endpoints voor modules (`IModule.MapBrowserEndpoints`)

`IModule` krijgt een tweede haak met een **standaardimplementatie die niets doet**, zodat bestaande modules en
testmodules niet veranderen:

```csharp
void MapBrowserEndpoints(IEndpointRouteBuilder app) { }
```

`Program.cs` roept `MapDoctrinaBrowserEndpoints()` aan, die per ingeschakelde module een groep `/{naam in kleine
letters}` aanmaakt met `RequireAuthorization()` (ingelogd met de cookie). Een endpoint dat anoniem moet zijn, zegt dat
zelf met `.AllowAnonymous()`. Het voorvoegsel voorkomt dat modules elkaars paden of de paden van de host overnemen.

Alternatief: de endpoints direct in de host koppelen (de host kent dan de Lti-module). Afgewezen: dat breekt het
modulesysteem. Alternatief 2: Blazor-pagina's voor de launch. Afgewezen: de stroom heeft ruwe HTTP-antwoorden nodig
(redirect, formulier-HTML, JSON).

### D3: Datamodel in schema `lti`

| Tabel | Velden |
|---|---|
| `Tools` (`ExchangeableEntity`) | `Name` (200), `ClientId` (100, uniek), `DeploymentId` (100), `LoginUrl`, `LaunchUrl`, `RedirectUris` (tekst, één adres per regel), `SendName`, `SendEmail` |
| `Keys` (`ExchangeableEntity`) | `Kid` (uniek), `PrivateKey` (`varbinary(max)`, versleuteld), `PublicKey` (RSA-parameters als JSON: `n` en `e`), `RetiredAt` (null = actief) |

- `ExternalId` is het eigen `Id` en `Source` is "Doctrina" (huisregel 6); verwijderen zet `Status` op `ToBeDeleted` en alle
  queries filteren op actieve records.
- `ClientId` en `DeploymentId` zijn willekeurige, ondoorzichtige strings (32 tekens uit `RandomNumberGenerator`, base64url).
  Dit zijn geen sleutels van entiteiten (huisregel 5 gaat over `Id`).
- Eén `deployment_id` per tool: LTI staat meer toe, maar tools per faculteit staan buiten de scope.
- Adressen worden gevalideerd door één klasse `LtiUrl` (absoluut, `https`, of `http` voor `localhost`), gebruikt door de
  service; de beheerpagina toont de foutcode van de service.

### D4: Itemsoort `ToolLink` in Content

`ContentItemKind` krijgt `ToolLink`; `content.Items` krijgt de kolom `ToolId` (`uniqueidentifier`, null; zonder foreign key,
zoals `OfferingId`) met een migratie. `ContentService` krijgt `AddToolLinkAsync(sectionId, title, toolId)`:
controleert via `ILtiToolCatalog.ExistsAsync` en geeft `Content.Item.UnknownTool` bij een onbekende of verwijderde tool.
Hernoemen, publiceren, verplaatsen en verwijderen werken ongewijzigd.

Zonder `ILtiToolCatalog` (module Lti uit) bestaat er geen formulier en geeft `AddToolLinkAsync` `Content.Item.UnknownTool`. Een
bestaande tool link verandert niet als Lti later uitgaat: hij toont dan een "Launch"-knop naar `/lti/launch/…`, die 404 geeft
omdat het pad niet bestaat. Dat is aanvaardbaar; een uitgezette module is een beheerkeuze.

Op de uitvoeringspagina en de itempagina toont een tool link een knop "Launch" (`<a href="/lti/launch/{item}"
target="_blank" rel="noopener noreferrer">` in een `DcButton`-variant). De itempagina van een tool link toont de titel en
de knop.

### D5: De launch-stroom

Eén `LaunchService` in Lti, drie endpoints (D2: groep `/lti`):

1. `GET /lti/launch/{item}` (ingelogd). `IContentDirectory.FindToolLinkAsync` (leesrecht en zichtbaarheid), de tool opzoeken, een
   `lti_message_hint` maken (D8) en een redirect (302) naar `LoginUrl` met `iss`, `login_hint` (de persoon als `Guid.ToString("N")`,
   ondoorzichtig), `target_link_uri` (`LaunchUrl`), `lti_message_hint`, `client_id`, `lti_deployment_id`.
2. De tool stuurt de browser naar het authenticatie-endpoint:
3. `GET /lti/authorize` (ingelogd; zonder cookie eerst inloggen en dan terug naar dit adres). Valideert in deze volgorde:
   `client_id` → tool; `redirect_uri` exact in `RedirectUris` (vóór alles wat redirect, zodat er nooit naar een onbekend adres
   gestuurd wordt); `scope`, `response_type`, `response_mode`, `prompt`, `nonce`, `state`; de hint (D8); `login_hint` en hint horen bij de
   ingelogde persoon; het item is nog leesbaar en zichtbaar (opnieuw `FindToolLinkAsync`). Dan de `id_token` (D6, D9)
   en een HTML-pagina met een formulier (`method=post`, `action=redirect_uri`, velden `id_token` en `state`) dat met
   `onload` automatisch verzonden wordt, met een `<noscript>`-knop "Continue". Antwoord met `Cache-Control: no-store`.
4. `GET /lti/jwks.json` (anoniem): de publieke sleutels (D7).

Fouten zijn `Result`-foutcodes `Lti.Launch.*`; een ongeldig verzoek geeft een eenvoudige foutpagina met de code
(400/403), nooit een redirect naar de tool. Alleen `GET` wordt ondersteund op `/lti/authorize`: dat is wat OIDC verplicht stelt
voor een autorisatie-endpoint; `POST` zou antiforgery en een tweede pad vragen en komt als een tool het nodig heeft.

De launch opent in een nieuw venster (`launch_presentation.document_target = window`): geen iframe, dus geen problemen met
cookies van derden of `X-Frame-Options`.

### D6: Tokens met `Microsoft.IdentityModel.JsonWebTokens`

`JsonWebTokenHandler.CreateToken` met een `SecurityTokenDescriptor` (`SigningCredentials` met `RsaSecurityKey` en `kid`, RS256), claims
als dictionary zodat de geneste LTI-claims (`resource_link`, `context`, `roles`, `launch_presentation`) JSON-objecten zijn.
Geldigheid: `iat` = nu, `exp` = nu + 5 minuten, met `TimeProvider` zodat tests de tijd kunnen sturen. Expliciet package
`Microsoft.IdentityModel.JsonWebTokens` in `Directory.Packages.props`, op dezelfde 8.x-versie als de al gebruikte
`Microsoft.IdentityModel.Protocols.OpenIdConnect` (nu 8.19.2).

### D7: Sleutels

`KeyService` (scoped): `ActiveAsync()` geeft de actieve sleutel (RSA 2048) en maakt er een aan als er geen is; `RotateAsync()`
zet `RetiredAt` op de actieve en maakt een nieuwe; `JwksAsync()` geeft de actieve en de sleutels die minder dan
`Lti:Keys:RetireAfterDays` dagen geleden zijn gepensioneerd (standaard 7) en markeert oudere als `ToBeDeleted`.

- Opslag: de privésleutel (PKCS#8) gaat door `IDataProtector("Lti.Keys")` voordat hij in `Keys.PrivateKey` komt. De publieke
  sleutel staat niet versleuteld (hij is publiek).
- Kan de privésleutel niet ontsleuteld worden (sleutelring kwijt of veranderd), dan wordt die sleutel op `ToBeDeleted` gezet en
  `ActiveAsync` maakt een nieuwe. Tools halen de JWKS opnieuw op (of de beheerder meldt het); dat is beter dan een
  launch die faalt tot iemand ingrijpt.
- Gelijktijdige eerste aanvragen: een unieke index op "één actieve sleutel" (gefilterd op `RetiredAt IS NULL AND Status =
  'Active'`) voorkomt twee actieve sleutels; de verliezer leest de winnaar.

Alternatief: sleutels in een bestand of in een key vault. Afgewezen voor nu: de database is er al, en het versleutelen met Data
Protection geeft hetzelfde als een bestand met rechten. Een vault kan later de `IDataProtector` vervangen.

### D8: `lti_message_hint` met Data Protection

De hint is een `ITimeLimitedDataProtector` (doel `Lti.MessageHint`, levensduur 5 minuten) over `"{itemId:N}.{personId:N}"`.
Er is geen tabel voor launches; `/lti/authorize` ontsleutelt de hint en vergelijkt de persoon met de huidige persoon (via
`CurrentPersonIdAsync`) en met `login_hint`. De hint is niet eenmalig: de tool koppelt een launch aan de `state`/`nonce`, en
`exp` van de token is kort; replay-bescherming is de taak van de tool (LTI-security framework: `nonce`).

Alternatief: een tabel met launch-records die bij gebruik verwijderd worden. Afgewezen: meer opslag en opruimwerk voor
dezelfde veiligheidswinst.

### D9: Claims, rollen en persoonsgegevens

| Claim | Waarde |
|---|---|
| `iss` | `Lti:Issuer`, anders `{scheme}://{host}` van het verzoek; dit moet stabiel zijn (beheerpagina toont hem) |
| `aud` | `client_id` |
| `sub` | de persoon (`Guid.ToString("N")`), gelijk aan `login_hint` |
| `…/deployment_id` | `Tools.DeploymentId` |
| `…/resource_link` | `{ id: itemId, title }` |
| `…/context` | `{ id: offeringId, label: ExternalId van de uitvoering, title: naam, type: [CourseOffering] }` |
| `name`, `given_name`, `family_name` | alleen met `SendName`; `family_name` = tussenvoegsel + achternaam |
| `email` | alleen met `SendEmail` en als de persoon een mailadres heeft |

Rollen (`OfferingRole` uit `Academic.Contracts`): Student → `membership#Learner`; Lecturer en Coordinator →
`membership#Instructor`; TeachingAssistant → `membership/Instructor#TeachingAssistant`. Een gebruiker zonder inschrijving die
toch mag lezen (`Content.Read` of `Content.Manage`, bv. een beheerder) krijgt `institution/person#Administrator`. Heeft
iemand meerdere rollen, dan komen alle bijbehorende rol-URI's mee.

Persoonsgegevens gaan alleen mee als de tool dat vraagt (AVG). In logs komt niets uit de launch: alleen de foutcode en de
tool-id.

### D10: Beheerpagina `/lti/tools`

Interactief (zoals `ManageGroups`), elke actie in een eigen scope, `Dc*`-componenten. Bovenaan de platformgegevens met kopieerbare
waarden (issuer, authenticatie-endpoint, JWKS-URL, en de actieve `kid`), met de knop "Rotate key". Daaronder de lijst met tools
(naam, `client_id`, `deployment_id`, adressen) met "Edit" en "Delete" (in een `DcModal`), en een formulier voor een nieuwe tool
met de keuzes "Send name" en "Send email". Alle teksten zijn Engels.

### D11: Afwijkingen van het voorstel

- §8 (roadmap) noemt rij 8 als "LTI 1.3 core + Deep Linking + AGS + NRPS". Besluit (gebruiker, 2026-11-14): deze change is alleen de
  core; `docs/Doctrina-voorstel.md` krijgt vervolgrijen voor Deep Linking, AGS en NRPS. Onderbouwing: elk is een eigen protocol met
  eigen eisen (tool-JWKS, `client_credentials` voor services) en eigen tests.
- §6.5 noemt de JWKS-URL van de tool in de registratie en Dynamic Registration. Beide volgen met Deep Linking/AGS (de eerste
  functie die berichten van de tool controleert) respectievelijk later.
- §3 en `IModule` kennen alleen `/api/v1`-endpoints; de haak uit D2 is nieuw en wordt in `docs/ontwikkelen/modules.md` beschreven.
- §6.5 noemt conformance testen met de 1EdTech-suite: niet in deze change; een handmatige controle met de referentietool wel
  (tasks).

## Risks / Trade-offs

- [De Data Protection-sleutelring staat standaard per machine en gaat verloren bij een nieuwe server of container] → Privésleutels
  worden dan onleesbaar; D7 maakt dan vanzelf een nieuwe sleutel, de launches blijven werken zodra tools de JWKS verversen. Voor
  productie moet beheer de sleutelring persistent maken (documenteren in `starten.md`); buiten de scope van deze change.
- [Tools cachen de JWKS: na rotatie kan een launch tijdelijk een onbekende `kid` hebben] → De oude sleutel blijft `RetireAfterDays`
  dagen in de JWKS; de `kid` staat in de header. Een tool die de JWKS niet ververst, moet dat zelf oplossen.
- [De issuer uit het verzoek kan achter een proxy verschillen van het publieke adres] → `Lti:Issuer` is instelbaar en de beheerpagina
  toont de gebruikte waarde, zodat beheerders het verschil zien.
- [Het authenticatie-endpoint met `GET` kan door een kwaadwillende pagina worden aangeroepen] → Het vereist de ingelogde gebruiker
  en een door Doctrina uitgegeven, korte `lti_message_hint` voor dezelfde persoon, en post alleen naar een geregistreerde
  `redirect_uri`; zonder de hint is er niets te doen.
- [Een tool link blijft staan als de tool is verwijderd] → De launch geeft `Lti.Tool.NotFound`; de docent ziet de link in de
  beheerpagina en kan hem verwijderen. Een melding bij het verwijderen van een tool in gebruik valt buiten de scope.
- [Geen eenmalig gebruik van de hint] → Zie D8; de geldigheid is kort en de tool controleert `nonce`.

## Migration Plan

Nieuw schema `lti` met één migratie en één migratie in `content` (kolom `ToolId`, nullable). Bestaande data verandert niet. De module
staat alleen aan waar `Modules:Enabled` haar noemt; `Lti:Issuer` instellen voor productie. Terugdraaien: de module uitzetten;
schema, kolom en tool links blijven staan.

## Open Questions

- Welke tool gebruiken we voor de eerste handmatige test? De 1EdTech-referentietool (Saltire) vraagt een publiek bereikbaar
  adres voor de JWKS; lokaal kan een minimale eigen testtool volstaan. De taak met de handmatige controle (6.2) kiest dat bij
  het uitvoeren; dit beantwoordt de open vraag uit §10 van het voorstel.
