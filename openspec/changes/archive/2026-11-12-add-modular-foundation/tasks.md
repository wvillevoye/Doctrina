# Taken

## 1. Opzet van de solution

- [x] 1.1 Voeg `global.json` (SDK 10.0.403, `rollForward: latestFeature`), `Directory.Build.props` (net10.0, nullable, implicit usings, `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`) en `.editorconfig` toe; controleer dat `dotnet --version` in de root van de repo 10.0.x geeft
- [x] 1.2 Voeg `Directory.Packages.props` toe met central package management (Microsoft-packages vast op 10.0.x, Scalar.AspNetCore, xUnit, bUnit, Testcontainers.MsSql, NetArchTest.Rules, Microsoft.Extensions.TimeProvider.Testing); controleer dat geen projectbestand een `Version`-attribuut op een `PackageReference` heeft
- [x] 1.3 Maak `Doctrina.slnx` met `src/Doctrina.Host`, `Doctrina.Core`, `Doctrina.Infrastructure`, `Doctrina.UI` (lege RCL), `src/Modules/README.md` en de testprojecten `Doctrina.Core.Tests`, `Doctrina.Architecture.Tests`, `Doctrina.Host.IntegrationTests`, `Doctrina.TestKit`; controleer dat `dotnet build` zonder warnings slaagt
- [x] 1.4 Zet het launch-profiel van de host op `https://localhost:5001;http://localhost:5000` en voeg `appsettings.Development.json` toe met de waarden van de dev-stack (authority `http://localhost:8080/realms/doctrina`, client `doctrina-web`, audience `doctrina-api`, SQL Server-connection string); controleer dat `dotnet run` op poort 5001 start
- [x] 1.5 Richt de configuratie per omgeving in (design D13): `appsettings.Production.json`, `appsettings.Test.json` (instellingen van de nagebootste provider uit `DoctrinaHostFactory`, die voortaan in omgeving `Test` draait), het optionele `appsettings.Secrets.json` (in `.gitignore`, niet geladen in `Test`, niet gepubliceerd) met `appsettings.Secrets.example.json`; verplaats het client secret uit `appsettings.Development.json`; zet de connection string in Development op `(localdb)\MSSQLLocalDB`; controleer met tests dat `appsettings.Secrets.json` boven de omgevingsbestanden wint en in `Test` niet wordt geladen
- [x] 1.6 Haal de SQL Server-container uit de dev-stack en `Testcontainers.MsSql` uit `Directory.Packages.props`; werk `dev-stack/README.md`, `docs/doctrina-integratie.md` en `AGENTS.md` bij naar LocalDB en de nieuwe configuratiebestanden; controleer dat `docker compose config` in `dev-stack/` geldig is en `dotnet build` slaagt

## 2. Kernbouwstenen

- [x] 2.1 Implementeer `Error`, `ErrorType`, `Result`, `Result<T>` volgens voorstel §3.6 in `Doctrina.Core`; controleer met unittests voor succes/fout, impliciete conversies en `Match`
- [x] 2.2 Definieer `IModule`, `NavItem`, `Permission`, `IEventBus`/`IEventHandler<T>` en de basistypen voor entiteiten (`Entity`, `ExchangeableEntity`) in `Doctrina.Core` met XML-documentatie; controleer dat `Doctrina.Core` geen NuGet-packages gebruikt en alleen de framework reference `Microsoft.AspNetCore.App` heeft (nodig voor de `/api/v1`-routegroep in `IModule.MapEndpoints`, design D2; architectuurtest in 4.1)

## 3. Modulesysteem

- [x] 3.1 Implementeer de modulecatalogus en de opstartvalidatie (lijst ingeschakelde modules, onbekende namen, afhankelijkheidsvolgorde via topologische sortering, ontbrekende afhankelijkheid, cycli) met één samengevoegde opstart-exception; controleer met testklassen `ModulesWordenViaConfiguratieAangezet` en `ModuleAfhankelijkhedenWordenBijHetOpstartenOpgelost` voor de scenario's "Ingeschakelde module wordt gestart", "Uitgeschakelde module draagt niets bij", "Onbekende modulenaam laat het opstarten mislukken", "Afhankelijkheden starten eerst", "Ontbrekende afhankelijkheid laat het opstarten mislukken", "Cyclische afhankelijkheid laat het opstarten mislukken"
- [x] 3.2 Koppel de bijdragen van modules aan de host: services, `/api/v1`-routegroep, Razor-componentassemblies, navigatie; voeg de testmodules `Sample`, `A` en `B` toe aan `Doctrina.TestKit`; controleer met integratietests voor ingeschakelde vs. uitgeschakelde `Sample` (endpoint 200 vs. 404, navigatie aanwezig vs. afwezig) en "Module-endpoint onder het versieprefix"
- [x] 3.3 Bouw het navigatiemenu op uit ingeschakelde modules en filter items op vereiste permissie; controleer met bUnit-tests "Navigatie-item zichtbaar met permissie" en "Navigatie-item verborgen zonder permissie"
- [x] 3.4 Groepeer de health checks van modules per moduletag onder `/health/ready`; controleer met integratietest "Falende modulecheck maakt de host niet gereed"

## 4. Architectuurregels

- [x] 4.1 Voeg architectuurtests toe: module-implementaties verwijzen niet naar de implementatie van een andere module (op assembly- en `.csproj`-niveau), `Doctrina.Core` verwijst naar geen NuGet-packages en geen EF Core-, authenticatie- of UI-assemblies (framework reference `Microsoft.AspNetCore.App` toegestaan, design D2), modules bevatten geen ruwe Bootstrap-classes in `.razor`-bestanden; controleer met tests "Verwijzing naar de implementatie van een andere module laat de tests falen" (via een opzettelijk foute fixture-assembly) en "Verwijzing naar een contracts-project is toegestaan"

## 5. Opslag

- [x] 5.1 Implementeer `ModuleDbContext` (standaardschema + migratiegeschiedenis per schema), de conventie voor de unieke index op `(Source, ExternalId)` en de `DateLastModified`-interceptor met `TimeProvider`; voeg de LocalDB-databasefixture toe aan `Doctrina.TestKit` (eigen, lege database `Doctrina_Test_<id>` per test, aangemaakt en weer verwijderd; basis-connection string uit `appsettings.Test.json`, design D10/D13); controleer met integratietests "Tabellen worden in het moduleschema aangemaakt" en "Migratiegeschiedenis per module"
- [x] 5.2 Implementeer migreren bij het opstarten voor ingeschakelde modules achter `Database:MigrateOnStartup` (standaard true in Development); controleer met tests "Openstaande migraties toegepast in development", "Geen automatische migratie buiten development", "Uitgeschakelde module wordt niet gemigreerd"
- [x] 5.3 Controleer sleutelgeneratie en synchronisatievelden met integratietests "Bij invoegen gegenereerde sleutels zijn oplopend", "DateLastModified wordt automatisch bijgehouden" (FakeTimeProvider), "Dubbele ExternalId binnen een bron wordt geweigerd", "Zelfde ExternalId uit een andere bron is toegestaan"
- [x] 5.4 Beschrijf in `docs/ontwikkelen/database.md` hoe je een migratie voor een module toevoegt (`dotnet ef migrations add` met `--context` en uitvoermap) en uitvoert; controleer dat de beschreven commando's werken voor de testmodule

## 6. Domeinevents

- [x] 6.1 Implementeer de in-process eventbus met aflevering na de commit vanuit de `SaveChanges`-interceptor, foutisolatie per handler en logging; controleer met tests "Event bereikt handlers in andere modules", "Handler van een uitgeschakelde module wordt niet aangeroepen", "Event zonder handlers", "Eén handler faalt, de andere draaien gewoon", "Geen event als het opslaan mislukt"

## 7. Authenticatie

- [x] 7.1 Configureer cookie + OpenID Connect (code + PKCE, `MapInboundClaims = false`, claimtypen `name`/`roles`, `SaveTokens = false`, `IdpHint` → `kc_idp_hint`) vanuit `Authentication:Oidc`; voeg een nagebootste OIDC-provider toe aan `Doctrina.TestKit`; controleer met tests "Anonieme gebruiker wordt naar de provider gestuurd", "Na succesvol inloggen terug naar de gevraagde pagina", "Pagina's zonder inlogeis blijven openbaar", "Hint wordt meegestuurd als die is ingesteld", "Geen hint als die niet is ingesteld"
- [x] 7.2 Controleer de claimverwerking met tests "Rollen en naam uit het token" en "Meerdere rollen" (nagebootste provider die ID-tokens uitgeeft)
- [x] 7.3 Implementeer uitloggen (cookie + end-session bij de provider) en de revaliderende authentication state provider voor Interactive Server; controleer met test "Uitloggen beëindigt beide sessies"
- [x] 7.4 Configureer JwtBearer alleen voor de `/api/v1`-groep (`Authentication:Api`); controleer met tests "Geldig token van een systeem", "Ontbrekend token", "Token voor een andere audience", "Sessiecookie wordt niet door de API geaccepteerd"
- [x] 7.5 Hernoem de testklassen en -methoden van 7.1–7.4 naar de Nederlandse requirement- en scenarionamen (design D12); controleer dat de tests nog slagen

## 8. Toegangsbeheer

- [x] 8.1 Implementeer het permissieregister uit ingeschakelde modules, de koppeling `Authorization:RolePermissions` met `*`, de claims-transformatie en dynamische permissie-policies, plus de opstartvalidatie; controleer met tests "Gedeclareerde permissies zijn bekend", "Dubbele permissienaam laat het opstarten mislukken", "Permissies uit meerdere rollen worden samengevoegd", "Jokerteken geeft elke permissie", "Niet-gekoppelde rol geeft niets", "Koppeling naar een onbekende permissie laat het opstarten mislukken", "Clientpermissie geeft toegang tot de API"
- [x] 8.2 Voeg `RequirePermission` toe voor endpoints en pagina's, een pagina "Geen toegang" met status 403, en vereis standaard authenticatie op `/api/v1`; controleer met tests "Pagina geweigerd zonder permissie", "API geweigerd zonder permissie", "Pagina toegestaan met permissie", "Ongemarkeerd endpoint vereist authenticatie"

## 9. API-conventies

- [x] 9.1 Voeg `ToHttpResult()` toe, dat `ErrorType` vertaalt naar status en de extensievelden `code` en `errors`; controleer met tests "Fouttype bepaalt de status" (alle zes typen) en "Validatiefouten per veld"
- [x] 9.2 Voeg `.resx`-fouttitels toe (nl standaard, en) met request localization en terugval op de melding; controleer met tests "Engelse titel gevraagd" en "Ontbrekende vertaling valt terug op de melding"
- [x] 9.3 Voeg globale exceptionafhandeling toe met `Server.Unexpected`, trace-id en logging zonder persoonsgegevens; controleer met test "Onafgehandelde exception in productie"
- [x] 9.4 Voeg het OpenAPI-document toe met bearer-beveiligingsschema en Scalar op `/scalar`, afhankelijk van de omgeving / `Api:Documentation:Enabled`; controleer met tests "OpenAPI-document toont module-endpoints" en "Documentatie standaard verborgen in productie"
- [x] 9.5 Voeg `/health/live` en `/health/ready` toe (databasecheck + modulechecks), anoniem en zonder gevoelige gegevens; controleer met tests "Liveness terwijl de database onbereikbaar is" en "Readiness terwijl de database onbereikbaar is"

## 10. Host-UI en documentatie

- [x] 10.1 Voeg de kale host-layout toe (semantische HTML, nog geen Bootstrap), startpagina, "Inloggen"/"Uitloggen" en `/profiel` met naam, issuer, rollen en permissies zonder tokens; controleer met bUnit-/integratietests "Profielpagina toont de identiteit" en "Profielpagina zonder persoonsnummer"
- [x] 10.2 Schrijf `docs/ontwikkelen/starten.md`: dev-stack starten, host draaien, inloggen met `beheerder`/`student`, overschakelen op Entra via `IdpHint`, tests draaien; en `docs/ontwikkelen/modules.md`: een module toevoegen (projecten, `IModule`, schema, permissies, navigatie, aanzetten); controleer dat een nieuwe ontwikkelaar met `starten.md` als `student` kan inloggen (handmatige controle, vastgelegd in de PR)

## 11. Integratiecontrole

- [x] 11.1 Draai `dotnet test` voor de hele solution (LocalDB beschikbaar, geen Docker nodig) en bevestig dat elk scenario van de zes capability-specs een slagende test heeft (traceerbaarheidstabel in de PR-beschrijving)
- [x] 11.2 Handmatige rooktest tegen de dev-stack: inloggen als `beheerder` en `student` via Keycloak, `/api/v1` aanroepen met een client-credentials-token van `doctrina-sis-sync`, en (als er een tenant is ingesteld) inloggen via Entra met `IdpHint=entra`; resultaten vastleggen in de PR

## Vervolg in de workflow

- Beoordeel de change (proposal, ontwerpbesluiten D1–D13) vóór `/opsx:apply`.
- Archiveer na de merge met `/opsx:archive` en controleer daarna dat `openspec/specs/` de zes capabilities bevat.
