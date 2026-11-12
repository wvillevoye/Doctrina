# Ontwerp

## Context

Nieuwe repository: nog geen code. Aanleiding en reikwijdte: `proposal.md`. Doelarchitectuur: `docs/Doctrina-voorstel.md`
§3. De lokale dev-stack (`dev-stack/` in deze repo, zie D11) levert al Keycloak-realm `doctrina` met de clients
`doctrina-web` (code + PKCE, redirect `https://localhost:5001/signin-oidc`), `doctrina-api` (audience) en
`doctrina-sis-sync` (client credentials), die tokens in Entra-vorm uitgeven (`roles`, `name`, `oid`); zie
`docs/doctrina-integratie.md`. De database draait lokaal op SQL Server LocalDB (`(localdb)\MSSQLLocalDB`, zie D13).
Geïnstalleerde SDK op de ontwikkelmachine: 10.0.403; .NET 11 is uit, maar de stack ligt
vast op .NET 10 (LTS).

## Doelen / Geen doelen

**Doelen:**
- Een host die latere changes alleen uitbreiden door moduleprojecten toe te voegen, zonder de binnenkant van de host aan
  te passen.
- Elke requirement in de specs van deze change is gedekt door geautomatiseerde tests die zonder Keycloak draaien.
- Identiek applicatiegedrag tegen Keycloak, Entra ID en SURFconext; alleen de configuratie verschilt.

**Geen doelen:**
- Modules laden uit DLL's die niet in de build zaten (plugins).
- Gegarandeerde/duurzame aflevering van events over herstarts van het proces heen.
- Vormgeving voorbij kale semantische HTML (het design system volgt in `add-design-system`).

## Besluiten

### D1 — Modules worden meegecompileerd en via configuratie aangezet
Het hostproject verwijst naar elk moduleproject; een statische modulecatalogus somt ze op; `Modules:Enabled` kiest
welke starten. *Alternatief:* assemblies scannen tijdens runtime / `AssemblyLoadContext`-plugins — afgewezen: Blazor
heeft de componentassemblies bij het opstarten nodig (`AddAdditionalAssemblies`), trimming/AOT en debuggen worden
lastiger, en er is geen eis om modules los te leveren. Gevolg: een uitgeschakelde module zit wel in de binary, maar
draagt niets bij.

### D2 — Modulecontract
`IModule` uit voorstel §3.4 blijft, met deze verfijningen:
- `MapEndpoints` krijgt de `/api/v1`-routegroep (met standaard verplichte autorisatie) in plaats van de root-builder,
  zodat modules het versieprefix en de regel "standaard beveiligd" niet kunnen omzeilen.
- Health checks worden via `ConfigureServices` toegevoegd met een moduletag; de host groepeert de resultaten op die tag.
- Componenten worden uit de eigen assembly van de module gehaald.
- Omdat `MapEndpoints` een ASP.NET Core-type gebruikt, heeft `Doctrina.Core` een framework reference naar
  `Microsoft.AspNetCore.App` (besloten 2026-11-12). Core heeft nog steeds **geen NuGet-packages** en geen EF Core-,
  authenticatie- of UI-afhankelijkheden; de architectuurtests (taak 4.1) bewaken dat. *Alternatieven:* een apart
  abstractieproject of een losse interface `IModuleEndpoints` — afgewezen: extra project of afwijking van §3.4 zonder
  voordeel.

De opstartvalidatie (onbekende namen, ontbrekende afhankelijkheden, cycli via topologische sortering, dubbele
permissies, onbekende permissies in de rolkoppeling) draait voordat de app wordt gebouwd en gooit één samengevoegde
`ModuleConfigurationException`.

### D3 — Eén database, schema per module, migratiegeschiedenis per schema
Een basisklasse `ModuleDbContext` zet `HasDefaultSchema(<module>)` en `MigrationsHistoryTable("__EFMigrationsHistory",
<module>)`. *Alternatief:* een database per module — afgewezen: overhead voor transacties, back-ups en beheer zonder
voordeel op dit moment. Lokaal is dat ene database `Doctrina` op `(localdb)\MSSQLLocalDB` (D13). Migraties bij het opstarten staan achter `Database:MigrateOnStartup` (standaard alleen true in
Development); buiten Development voert de deployment-pipeline EF Core-migratiebundels uit (de pipeline zelf valt buiten
de scope).

### D4 — Sleutels en synchronisatievelden
Het basistype voor entiteiten heeft `Id` (`Guid`, `ValueGeneratedOnAdd`), zodat de sequentiële GUID-generator van EF
Core voor SQL Server hem toekent; geen `Guid.NewGuid()`/`CreateVersion7()` (huisregel 5). Een basistype
`ExchangeableEntity` voegt `ExternalId` (`nvarchar(255)`), `Status`, `DateLastModified` en `Source` (`nvarchar(64)`)
toe, met een unieke index op (`Source`, `ExternalId`) via een gedeelde conventie. Een `SaveChanges`-interceptor zet
`DateLastModified`.

### D5 — `TimeProvider` in plaats van een eigen `IClock`
Afwijking van voorstel §3.1 (`IClock`): de ingebouwde `TimeProvider` van .NET (met `FakeTimeProvider` in tests) geeft
dezelfde testbaarheid zonder eigen abstractie. *Alternatief:* eigen `IClock` — geen toegevoegde waarde.

### D6 — In-process eventbus met aflevering na de commit
Events die bij een gegevenswijziging horen, laat een entiteit ontstaan met `Entity.RaiseEvent`; de entiteit is zelf de
verzamelaar. De `DomainEventsInterceptor` (per scope, op elke module-DbContext) onthoudt die entiteiten bij
`SavingChanges` en levert hun events af na een geslaagde `SaveChanges` (spec: "pas na het opslaan"); mislukt of
annuleert het opslaan, dan wordt niets afgeleverd en blijven de events op de entiteit staan voor een volgende poging.
`IEventBus.PublishAsync` levert direct af, voor events die niet bij een opslagactie horen. Aflevering gebeurt na
`SaveChanges`, niet na een expliciete, omvattende transactie; wie meerdere `SaveChanges` in één transactie zet, moet
daar rekening mee houden (de outbox lost dit later op). Handlers (`IEventHandler<T>`) komen uit DI; alleen ingeschakelde
modules registreren handlers, waarmee "uitgeschakelde modules worden niet aangeroepen" is gedekt. Handlers draaien na
elkaar in dezelfde DI-scope; exceptions worden opgevangen, gelogd (eventtype + handler, geen inhoud) en niet
doorgegeven. *Alternatieven:* MediatR (v13+ commercieel, huisregel 8 — afgewezen); outbox + Hangfire — uitgesteld tot
een module gegarandeerde aflevering nodig heeft (zie risico's).

### D7 — Authenticatie: cookie + OIDC voor de UI, JwtBearer voor `/api/v1`
- UI: cookieschema als standaard, OpenID Connect als challenge; code flow + PKCE; `MapInboundClaims = false`;
  `NameClaimType = "name"`, `RoleClaimType = "roles"`; `SaveTokens = false` (de UI roept services in-process aan, dus er
  wordt geen access token bewaard); **alleen het ID-token wordt opgeslagen** in de versleutelde sessiecookie, omdat
  providers het als `id_token_hint` nodig hebben bij uitloggen — zonder toont Keycloak een bevestigingspagina en stuurt
  de gebruiker niet terug (gevonden in de rooktest met de dev-stack); `GetClaimsFromUserInfoEndpoint = false`.
  `IdpHint` → `kc_idp_hint` in `OnRedirectToIdentityProvider` (genegeerd door Entra/SURFconext).
- API: JwtBearer met `Authority` (standaard de OIDC-authority) en `Audience` uit `Authentication:Api`; de
  `/api/v1`-groep zet `AuthenticationSchemes = Bearer`, zodat cookies nooit geaccepteerd worden en er geen redirects
  plaatsvinden.
- Blazor Interactive Server: `AddCascadingAuthenticationState`; een revaliderende authentication state provider beëindigt
  circuits waarvan de cookie verlopen is (interval 30 minuten).
Dit is de configuratie die end-to-end is bewezen in het dev-stack-voorbeeld (`samples/Doctrina.AuthCheck`), zonder
`SaveTokens`.

### D8 — Permissiemodel: rollen → permissies via configuratie
Modules declareren `Permission(Name, Description)`. `Authorization:RolePermissions` koppelt elke waarde van de
`roles`-claim — gebruikersrollen (`admin`, `employee`, `student`) en systeempermissies (`doctrina.persons.read`, …)
gelijk — aan permissienamen; `*` betekent alle. Een `IClaimsTransformation` voegt eenmaal per verzoek
`permission`-claims toe; één dynamische policy provider maakt van `RequirePermission("X")` een policy.
*Alternatieven:* permissies in de database met een beheerscherm — uitgesteld (nog geen eis; configuratie volstaat voor
een ontwikkel-/pilotfase); hard gecodeerde rolcontroles — afgewezen (koppelt modules aan rolnamen van de IdP).
Standaardkoppeling in `appsettings.json`: `admin: ["*"]`, `employee: []`, `student: []`.
Uitwerking: de claims heten `doctrina:permission` en staan in een eigen identity; permissieclaims die van buiten komen
(uit een token) worden verwijderd, zodat alleen de rolkoppeling telt. Permissienamen moeten met `<Module>.` beginnen.
De opstartvalidatie (dubbele of ongeldige permissies, koppeling naar een onbekende permissie) zit in dezelfde
samengevoegde `ModuleConfigurationException` als de modulefouten (D2). Pagina's gebruiken
`[RequirePermission("X")]`, endpoints `.RequirePermission("X")`. Kale 401/403-antwoorden krijgen in ProblemDetails de
codes `Auth.Unauthorized` en `Auth.Forbidden`.

### D9 — API-conventies
`Microsoft.AspNetCore.OpenApi` (ingebouwd in .NET 10) genereert het document; `Scalar.AspNetCore` (MIT) toont de UI op
`/scalar`. `AddProblemDetails` + `UseExceptionHandler` + `UseStatusCodePages` geven ProblemDetails bij onverwachte
fouten en kale 401/403/404; `Result.ToHttpResult()` vertaalt `ErrorType` volgens voorstel §3.6 en voegt `code` toe (en
`errors` bij validatie). Titels komen uit `.resx`-resources met de foutcode als sleutel (nl standaard, en), met
`Error.Message` als terugval; request localization ondersteunt `nl` en `en`.
Uitwerking: modules registreren hun fouttitels met `services.AddErrorTitles("<namespace>.<Module>Errors", assembly)`
(neutrale `.resx` = Nederlands, plus `.en.resx`); Doctrina's eigen codes (`Auth.*`, `Server.Unexpected`) staan in
`Doctrina.Infrastructure/Api/DoctrinaErrors.resx`. Eén `CustomizeProblemDetails`-stap vult `code` voor kale 401/403
en vertaalt de `title`. Onverwachte fouten in `/api` worden gelogd met trace-id, route (patroon, geen waarden),
exceptiontype en stacktrace, maar **zonder exceptionmelding**, omdat die persoonsgegevens kan bevatten. Het
OpenAPI-document staat op `/openapi/v1.json` (alleen `/api/v1`), Scalar op `/scalar`. `/health/live` heeft geen
checks; `/health/ready` controleert de database (`SELECT 1`) en de modulechecks en toont alleen namen en statussen.

### D10 — Teststrategie
- Unittests per project; integratietests met `WebApplicationFactory` in de omgeving `Test` en SQL Server LocalDB
  (`Doctrina.TestKit` levert de databasefixture, een testauthenticatiehandler met instelbare `roles`/`name`, en een
  `FakeTimeProvider`). De fixture maakt per test een eigen, lege database `Doctrina_Test_<id>` op
  `(localdb)\MSSQLLocalDB` aan, past de echte migraties toe en verwijdert de database na afloop (D13).
  *Afwijking van voorstel §3.3/§2 (Testcontainers):* besloten door de producteigenaar op 2026-11-12; LocalDB is de
  echte SQL Server-engine (geen in-memory provider), start sneller en vraagt geen Docker.
- Scenario's gebruiken **testmodules** (`Sample`, `A`, `B`) in `Doctrina.TestKit`/testprojecten, omdat er nog geen
  productmodule bestaat. `Doctrina.Host` verwijst er nooit naar.
- Het redirect- en uitloggedrag van OIDC wordt getest tegen een nagebootst OIDC-metadata-/authorize-endpoint in de
  testserver (geen Keycloak nodig); een handmatige rooktest tegen de dev-stack wordt beschreven, niet geautomatiseerd.
- Architectuurtests: NetArchTest.Rules (MIT) op gecompileerde assemblies plus een controle van projectverwijzingen in
  `.csproj`-bestanden.
- Testklassen zijn genoemd naar de requirement en testmethoden naar het scenario, in het Nederlands zoals in de specs
  (huisregel 10).

### D11 — Repository-indeling volgt voorstel §3.1, met drie aanpassingen
- **De dev-stack staat in deze repository onder `dev-stack/`** (afwijking van §3.3 "apart project", besloten door de
  producteigenaar op 2026-11-12): één checkout geeft code, specs en een werkende inlogomgeving. Daarin staan
  `docker-compose.yml` (compose-project `doctrina-dev`, met Keycloak en Mailpit; SQL Server draait niet in de
  dev-stack maar op LocalDB, D13), de gegenereerde realm-bestanden (`dev-stack/realm/`, in plaats
  van het geplande `infra/doctrina-realm.json`), de realm-generator en het Entra-script. `infra/` komt later, alleen
  voor de Dockerfile.
- **Hangfire wordt nog niet ingericht** (het voorstel noemt het onder Infrastructure): er bestaat geen taak vóór
  `add-api-and-import`.
- **Geen productmodule** in deze change; `src/Modules/` begint leeg, op een `README.md` met de indeling na.
Vastgezette SDK: `global.json` `10.0.403`, `rollForward: latestFeature`. Central package management met alle
Microsoft-packages op `10.0.x`. `Directory.Build.props`: `net10.0`, nullable, implicit usings, `TreatWarningsAsErrors`,
`AnalysisLevel latest-recommended`.

### D12 — OpenSpec-artefacten in het Nederlands
Besloten door de producteigenaar op 2026-11-12: proposal, design, tasks en specs zijn in het Nederlands. De
structuurwoorden die `openspec validate --strict` nodig heeft blijven Engels: koppen `## Why`, `## What Changes`,
`## Purpose`, `## ADDED Requirements`, `### Requirement:`, `#### Scenario:`, de sleutelwoorden SHALL/MUST en
GIVEN/WHEN/THEN. Identifiers in code blijven Engels; testnamen volgen de Nederlandse requirement- en scenarionamen.

### D13 — LocalDB en configuratie per omgeving
Besloten door de producteigenaar op 2026-11-12.
- **Database:** Development en de geautomatiseerde tests gebruiken SQL Server LocalDB `(localdb)\MSSQLLocalDB` met
  Windows-authenticatie (`Trusted_Connection=True`), dus zonder wachtwoord. De SQL Server-container verdwijnt uit de
  dev-stack; `Testcontainers.MsSql` vervalt. Productie gebruikt een gewone SQL Server-instantie.
- **Configuratiebestanden** van `Doctrina.Host`, in volgorde van laden (later wint):
  | Bestand | Inhoud | In git |
  |---|---|---|
  | `appsettings.json` | standaardwaarden voor elke omgeving | ja |
  | `appsettings.Development.json` | lokaal ontwikkelen: dev-stack-Keycloak, LocalDB `Doctrina` | ja |
  | `appsettings.Test.json` | omgeving `Test` = geautomatiseerde tests: nagebootste OIDC-provider, LocalDB-testdatabase | ja |
  | `appsettings.Production.json` | productie zonder secrets: `RequireHttpsMetadata`, logging, documentatie uit, niet automatisch migreren | ja |
  | `appsettings.Secrets.json` | secrets (client secrets, connection strings met wachtwoord) | **nee** (`.gitignore`) |
  Daarna volgen, zoals standaard in ASP.NET Core, user secrets (alleen Development), omgevingsvariabelen en
  command-line-argumenten. `appsettings.Secrets.example.json` staat wél in git en toont welke sleutels erin horen.
- `appsettings.Secrets.json` is optioneel en wordt **niet** geladen in de omgeving `Test`, zodat lokale secrets of een
  afwijkende connection string nooit in de tests terechtkomen. `appsettings.Test.json` en `appsettings.Secrets.json`
  worden niet gepubliceerd (`CopyToPublishDirectory="Never"`); op een server plaatst beheer het secrets-bestand zelf of
  gebruikt omgevingsvariabelen.
- Strenge DI-validatie (`ValidateScopes`, `ValidateOnBuild`) staat aan in Development én Test.
*Alternatieven:* alleen user secrets — afgewezen: werkt alleen in Development en niet op een server; alleen
omgevingsvariabelen — blijft mogelijk, maar een bestand is overzichtelijker voor beheer.

## Risico's / Afwegingen

- [Events gaan verloren bij een crash tussen commit en aflevering] → acceptabel voor in-process meldingen; voer een
  outbox (met Hangfire) in bij de eerste change die gegarandeerde aflevering nodig heeft.
- [Rolkoppeling in configuratie loopt uiteen tussen omgevingen] → de opstartvalidatie weigert onbekende permissies; de
  koppeling staat op de profielpagina van de beheerder.
- [Blazor-circuits houden een verouderde principal vast tot de revalidatie] → revalidatie elke 30 minuten;
  rolwijzigingen gelden bij de volgende keer inloggen of revalideren, beschreven in de gebruikershandleiding.
- [`oid`/`sub` van Keycloak en Entra verschillen voor dezelfde persoon] → het fundament toont alleen issuer +
  `sub`/`oid`; de koppeling aan `Person` wordt ontworpen in `add-academic-core`.
- [LocalDB bestaat alleen op Windows] → ontwikkelaars werken op Windows (projectcontext); CI heeft een Windows-runner
  met LocalDB nodig (bv. `windows-latest` op GitHub of een Windows-runner in GitLab). Testcode gaat via de
  connection string uit `appsettings.Test.json`, zodat een andere SQL Server later alleen configuratie vraagt.
- [Een vergeten `appsettings.Secrets.json` laat inloggen mislukken] → `appsettings.Secrets.example.json` en
  `docs/ontwikkelen/starten.md` beschrijven het kopiëren; de foutmelding bij een ontbrekend client secret noemt het bestand.
- [Strikte warnings-als-errors vertraagt het vroege werk] → bedoeld; onderdrukkingen alleen met een toelichting.

## Open vragen

- Git-hosting (GitLab of GitHub) en CI-runner — raakt alleen pipelinebestanden, volgt in een latere change.
- Of Scalar/OpenAPI in acceptatie/productie aan moet — geregeld via `Api:Documentation:Enabled`.
