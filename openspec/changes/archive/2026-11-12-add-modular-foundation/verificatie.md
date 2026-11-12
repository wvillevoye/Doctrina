# Verificatie add-modular-foundation

Bron voor de PR-beschrijving (taken 11.1 en 11.2). Datum: 2026-11-12, Windows 11, .NET SDK 10.0.403, LocalDB 16.0.

## 11.1 Geautomatiseerde tests

`dotnet test Doctrina.slnx` (LocalDB beschikbaar, geen Docker of Keycloak nodig): **104 geslaagd, 0 mislukt**
(Doctrina.Core.Tests, Doctrina.Architecture.Tests, Doctrina.Host.IntegrationTests).

Elk van de **60 scenario's** uit de zes capability-specs heeft een slagende test. Testklasse = requirement,
testmethode = scenario (huisregel 10, design D12). De tabel is gemaakt uit de specs en de TRX-resultaten
(`dotnet test -- --report-xunit-trx`); de koppeling gebeurt op naam.

| Capability | Requirement | Scenario | Test | Uitkomst |
|---|---|---|---|---|
| `access-control` | Modules declareren permissies | Gedeclareerde permissies zijn bekend | `Doctrina.Host.IntegrationTests.AccessControl.ModulesDeclarerenPermissies.GedeclareerdePermissiesZijnBekend` | geslaagd |
| `access-control` | Modules declareren permissies | Dubbele permissienaam laat het opstarten mislukken | `Doctrina.Host.IntegrationTests.AccessControl.ModulesDeclarerenPermissies.DubbelePermissienaamLaatHetOpstartenMislukken` | geslaagd |
| `access-control` | Rollen worden via configuratie aan permissies gekoppeld | Permissies uit meerdere rollen worden samengevoegd | `Doctrina.Host.IntegrationTests.AccessControl.RollenWordenViaConfiguratieAanPermissiesGekoppeld.PermissiesUitMeerdereRollenWordenSamengevoegd` | geslaagd |
| `access-control` | Rollen worden via configuratie aan permissies gekoppeld | Jokerteken geeft elke permissie | `Doctrina.Host.IntegrationTests.AccessControl.RollenWordenViaConfiguratieAanPermissiesGekoppeld.JokertekenGeeftElkePermissie` | geslaagd |
| `access-control` | Rollen worden via configuratie aan permissies gekoppeld | Niet-gekoppelde rol geeft niets | `Doctrina.Host.IntegrationTests.AccessControl.RollenWordenViaConfiguratieAanPermissiesGekoppeld.NietGekoppeldeRolGeeftNiets` | geslaagd |
| `access-control` | Rollen worden via configuratie aan permissies gekoppeld | Koppeling naar een onbekende permissie laat het opstarten mislukken | `Doctrina.Host.IntegrationTests.AccessControl.RollenWordenViaConfiguratieAanPermissiesGekoppeld.KoppelingNaarEenOnbekendePermissieLaatHetOpstartenMislukken` | geslaagd |
| `access-control` | Systeempermissies gebruiken dezelfde koppeling | Clientpermissie geeft toegang tot de API | `Doctrina.Host.IntegrationTests.AccessControl.SysteempermissiesGebruikenDezelfdeKoppeling.ClientpermissieGeeftToegangTotDeApi` | geslaagd |
| `access-control` | Pagina's en endpoints vereisen permissies | Pagina geweigerd zonder permissie | `Doctrina.Host.IntegrationTests.AccessControl.PaginasEnEndpointsVereisenPermissies.PaginaGeweigerdZonderPermissie` | geslaagd |
| `access-control` | Pagina's en endpoints vereisen permissies | API geweigerd zonder permissie | `Doctrina.Host.IntegrationTests.AccessControl.PaginasEnEndpointsVereisenPermissies.ApiGeweigerdZonderPermissie` | geslaagd |
| `access-control` | Pagina's en endpoints vereisen permissies | Pagina toegestaan met permissie | `Doctrina.Host.IntegrationTests.AccessControl.PaginasEnEndpointsVereisenPermissies.PaginaToegestaanMetPermissie` | geslaagd |
| `access-control` | Endpoints zijn standaard beveiligd | Ongemarkeerd endpoint vereist authenticatie | `Doctrina.Host.IntegrationTests.AccessControl.EndpointsZijnStandaardBeveiligd.OngemarkeerdEndpointVereistAuthenticatie` | geslaagd |
| `api-conventions` | Module-endpoints staan geversioneerd onder /api/v1 | Module-endpoint onder het versieprefix | `Doctrina.Host.IntegrationTests.ApiConventions.ModuleEndpointsStaanGeversioneerdOnderApiV1.ModuleEndpointOnderHetVersieprefix` | geslaagd |
| `api-conventions` | De API is gedocumenteerd met OpenAPI | OpenAPI-document toont module-endpoints | `Doctrina.Host.IntegrationTests.ApiConventions.DeApiIsGedocumenteerdMetOpenApi.OpenApiDocumentToontModuleEndpoints` | geslaagd |
| `api-conventions` | De API is gedocumenteerd met OpenAPI | Documentatie standaard verborgen in productie | `Doctrina.Host.IntegrationTests.ApiConventions.DeApiIsGedocumenteerdMetOpenApi.DocumentatieStandaardVerborgenInProductie` | geslaagd |
| `api-conventions` | Verwachte fouten worden als ProblemDetails teruggegeven | Fouttype bepaalt de status | `Doctrina.Host.IntegrationTests.ApiConventions.VerwachteFoutenWordenAlsProblemDetailsTeruggegeven.FouttypeBepaaltDeStatus` | geslaagd (6×) |
| `api-conventions` | Verwachte fouten worden als ProblemDetails teruggegeven | Validatiefouten per veld | `Doctrina.Host.IntegrationTests.ApiConventions.VerwachteFoutenWordenAlsProblemDetailsTeruggegeven.ValidatiefoutenPerVeld` | geslaagd |
| `api-conventions` | Fouttitels zijn vertaald | Engelse titel gevraagd | `Doctrina.Host.IntegrationTests.ApiConventions.FouttitelsZijnVertaald.EngelseTitelGevraagd` | geslaagd |
| `api-conventions` | Fouttitels zijn vertaald | Ontbrekende vertaling valt terug op de melding | `Doctrina.Host.IntegrationTests.ApiConventions.FouttitelsZijnVertaald.OntbrekendeVertalingValtTerugOpDeMelding` | geslaagd |
| `api-conventions` | Onverwachte fouten lekken geen details | Onafgehandelde exception in productie | `Doctrina.Host.IntegrationTests.ApiConventions.OnverwachteFoutenLekkenGeenDetails.OnafgehandeldeExceptionInProductie` | geslaagd |
| `api-conventions` | Liveness- en readiness-endpoints | Liveness terwijl de database onbereikbaar is | `Doctrina.Host.IntegrationTests.ApiConventions.LivenessEnReadinessEndpoints.LivenessTerwijlDeDatabaseOnbereikbaarIs` | geslaagd |
| `api-conventions` | Liveness- en readiness-endpoints | Readiness terwijl de database onbereikbaar is | `Doctrina.Host.IntegrationTests.ApiConventions.LivenessEnReadinessEndpoints.ReadinessTerwijlDeDatabaseOnbereikbaarIs` | geslaagd |
| `data-persistence` | Elke module heeft een eigen databaseschema | Tabellen worden in het moduleschema aangemaakt | `Doctrina.Host.IntegrationTests.DataPersistence.ElkeModuleHeeftEenEigenDatabaseschema.TabellenWordenInHetModuleschemaAangemaakt` | geslaagd |
| `data-persistence` | Elke module migreert zelfstandig | Migratiegeschiedenis per module | `Doctrina.Host.IntegrationTests.DataPersistence.ElkeModuleMigreertZelfstandig.MigratiegeschiedenisPerModule` | geslaagd |
| `data-persistence` | Elke module migreert zelfstandig | Uitgeschakelde module wordt niet gemigreerd | `Doctrina.Host.IntegrationTests.DataPersistence.ElkeModuleMigreertZelfstandig.UitgeschakeldeModuleWordtNietGemigreerd` | geslaagd |
| `data-persistence` | Automatisch migreren is instelbaar | Openstaande migraties toegepast in development | `Doctrina.Host.IntegrationTests.DataPersistence.AutomatischMigrerenIsInstelbaar.OpenstaandeMigratiesToegepastInDevelopment` | geslaagd |
| `data-persistence` | Automatisch migreren is instelbaar | Geen automatische migratie buiten development | `Doctrina.Host.IntegrationTests.DataPersistence.AutomatischMigrerenIsInstelbaar.GeenAutomatischeMigratieBuitenDevelopment` | geslaagd |
| `data-persistence` | Entiteitsleutels zijn sequentiële GUID's | Bij invoegen gegenereerde sleutels zijn oplopend | `Doctrina.Host.IntegrationTests.DataPersistence.EntiteitsleutelsZijnSequentieleGuids.BijInvoegenGegenereerdeSleutelsZijnOplopend` | geslaagd |
| `data-persistence` | Uitwisselbare entiteiten hebben synchronisatievelden | DateLastModified wordt automatisch bijgehouden | `Doctrina.Host.IntegrationTests.DataPersistence.UitwisselbareEntiteitenHebbenSynchronisatievelden.DateLastModifiedWordtAutomatischBijgehouden` | geslaagd |
| `data-persistence` | Uitwisselbare entiteiten hebben synchronisatievelden | Dubbele ExternalId binnen een bron wordt geweigerd | `Doctrina.Host.IntegrationTests.DataPersistence.UitwisselbareEntiteitenHebbenSynchronisatievelden.DubbeleExternalIdBinnenEenBronWordtGeweigerd` | geslaagd |
| `data-persistence` | Uitwisselbare entiteiten hebben synchronisatievelden | Zelfde ExternalId uit een andere bron is toegestaan | `Doctrina.Host.IntegrationTests.DataPersistence.UitwisselbareEntiteitenHebbenSynchronisatievelden.ZelfdeExternalIdUitEenAndereBronIsToegestaan` | geslaagd |
| `domain-events` | Events bereiken alle handlers van ingeschakelde modules | Event bereikt handlers in andere modules | `Doctrina.Host.IntegrationTests.DomainEvents.EventsBereikenAlleHandlersVanIngeschakeldeModules.EventBereiktHandlersInAndereModules` | geslaagd |
| `domain-events` | Events bereiken alle handlers van ingeschakelde modules | Handler van een uitgeschakelde module wordt niet aangeroepen | `Doctrina.Host.IntegrationTests.DomainEvents.EventsBereikenAlleHandlersVanIngeschakeldeModules.HandlerVanEenUitgeschakeldeModuleWordtNietAangeroepen` | geslaagd |
| `domain-events` | Events bereiken alle handlers van ingeschakelde modules | Event zonder handlers | `Doctrina.Host.IntegrationTests.DomainEvents.EventsBereikenAlleHandlersVanIngeschakeldeModules.EventZonderHandlers` | geslaagd |
| `domain-events` | Een falende handler raakt de publicerende module en andere handlers niet | Eén handler faalt, de andere draaien gewoon | `Doctrina.Host.IntegrationTests.DomainEvents.EenFalendeHandlerRaaktDePublicerendeModuleEnAndereHandlersNiet.EenHandlerFaaltDeAndereDraaienGewoon` | geslaagd |
| `domain-events` | Events worden pas na het opslaan gepubliceerd | Geen event als het opslaan mislukt | `Doctrina.Host.IntegrationTests.DomainEvents.EventsWordenPasNaHetOpslaanGepubliceerd.GeenEventAlsHetOpslaanMislukt` | geslaagd |
| `module-system` | Modules worden via configuratie aangezet | Ingeschakelde module wordt gestart | `Doctrina.Host.IntegrationTests.ModuleSystem.ModulesWordenViaConfiguratieAangezet.IngeschakeldeModuleWordtGestart` | geslaagd |
| `module-system` | Modules worden via configuratie aangezet | Uitgeschakelde module draagt niets bij | `Doctrina.Host.IntegrationTests.ModuleSystem.ModulesWordenViaConfiguratieAangezet.UitgeschakeldeModuleDraagtNietsBij` | geslaagd |
| `module-system` | Modules worden via configuratie aangezet | Onbekende modulenaam laat het opstarten mislukken | `Doctrina.Host.IntegrationTests.ModuleSystem.ModulesWordenViaConfiguratieAangezet.OnbekendeModulenaamLaatHetOpstartenMislukken` | geslaagd |
| `module-system` | Module-afhankelijkheden worden bij het opstarten opgelost | Afhankelijkheden starten eerst | `Doctrina.Host.IntegrationTests.ModuleSystem.ModuleAfhankelijkhedenWordenBijHetOpstartenOpgelost.AfhankelijkhedenStartenEerst` | geslaagd |
| `module-system` | Module-afhankelijkheden worden bij het opstarten opgelost | Ontbrekende afhankelijkheid laat het opstarten mislukken | `Doctrina.Host.IntegrationTests.ModuleSystem.ModuleAfhankelijkhedenWordenBijHetOpstartenOpgelost.OntbrekendeAfhankelijkheidLaatHetOpstartenMislukken` | geslaagd |
| `module-system` | Module-afhankelijkheden worden bij het opstarten opgelost | Cyclische afhankelijkheid laat het opstarten mislukken | `Doctrina.Host.IntegrationTests.ModuleSystem.ModuleAfhankelijkhedenWordenBijHetOpstartenOpgelost.CyclischeAfhankelijkheidLaatHetOpstartenMislukken` | geslaagd |
| `module-system` | Modules leveren navigatie | Navigatie-item zichtbaar met permissie | `Doctrina.Host.IntegrationTests.ModuleSystem.ModulesLeverenNavigatie.NavigatieItemZichtbaarMetPermissie` | geslaagd |
| `module-system` | Modules leveren navigatie | Navigatie-item verborgen zonder permissie | `Doctrina.Host.IntegrationTests.ModuleSystem.ModulesLeverenNavigatie.NavigatieItemVerborgenZonderPermissie` | geslaagd |
| `module-system` | Modules melden hun gezondheid | Falende modulecheck maakt de host niet gereed | `Doctrina.Host.IntegrationTests.ModuleSystem.ModulesMeldenHunGezondheid.FalendeModulecheckMaaktDeHostNietGereed` | geslaagd |
| `module-system` | Modulegrenzen worden tijdens de build bewaakt | Verwijzing naar de implementatie van een andere module laat de tests falen | `Doctrina.Architecture.Tests.ModulegrenzenWordenTijdensDeBuildBewaakt.VerwijzingNaarDeImplementatieVanEenAndereModuleLaatDeTestsFalen` | geslaagd |
| `module-system` | Modulegrenzen worden tijdens de build bewaakt | Verwijzing naar een contracts-project is toegestaan | `Doctrina.Architecture.Tests.ModulegrenzenWordenTijdensDeBuildBewaakt.VerwijzingNaarEenContractsProjectIsToegestaan` | geslaagd |
| `user-authentication` | Gebruikers loggen in via OpenID Connect | Anonieme gebruiker wordt naar de provider gestuurd | `Doctrina.Host.IntegrationTests.UserAuthentication.GebruikersLoggenInViaOpenIdConnect.AnoniemeGebruikerWordtNaarDeProviderGestuurd` | geslaagd |
| `user-authentication` | Gebruikers loggen in via OpenID Connect | Na succesvol inloggen terug naar de gevraagde pagina | `Doctrina.Host.IntegrationTests.UserAuthentication.GebruikersLoggenInViaOpenIdConnect.NaSuccesvolInloggenTerugNaarDeGevraagdePagina` | geslaagd |
| `user-authentication` | Gebruikers loggen in via OpenID Connect | Pagina's zonder inlogeis blijven openbaar | `Doctrina.Host.IntegrationTests.UserAuthentication.GebruikersLoggenInViaOpenIdConnect.PaginasZonderInlogeisBlijvenOpenbaar` | geslaagd |
| `user-authentication` | De identiteit gebruikt een provider-neutraal claimcontract | Rollen en naam uit het token | `Doctrina.Host.IntegrationTests.UserAuthentication.DeIdentiteitGebruiktEenProviderNeutraalClaimcontract.RollenEnNaamUitHetToken` | geslaagd |
| `user-authentication` | De identiteit gebruikt een provider-neutraal claimcontract | Meerdere rollen | `Doctrina.Host.IntegrationTests.UserAuthentication.DeIdentiteitGebruiktEenProviderNeutraalClaimcontract.MeerdereRollen` | geslaagd |
| `user-authentication` | Het inlogscherm van de provider kan worden overgeslagen | Hint wordt meegestuurd als die is ingesteld | `Doctrina.Host.IntegrationTests.UserAuthentication.HetInlogschermVanDeProviderKanWordenOvergeslagen.HintWordtMeegestuurdAlsDieIsIngesteld` | geslaagd |
| `user-authentication` | Het inlogscherm van de provider kan worden overgeslagen | Geen hint als die niet is ingesteld | `Doctrina.Host.IntegrationTests.UserAuthentication.HetInlogschermVanDeProviderKanWordenOvergeslagen.GeenHintAlsDieNietIsIngesteld` | geslaagd |
| `user-authentication` | Gebruikers loggen uit bij Doctrina en bij de provider | Uitloggen beëindigt beide sessies | `Doctrina.Host.IntegrationTests.UserAuthentication.GebruikersLoggenUitBijDoctrinaEnBijDeProvider.UitloggenBeeindigtBeideSessies` | geslaagd |
| `user-authentication` | De API authenticeert bearer-tokens | Geldig token van een systeem | `Doctrina.Host.IntegrationTests.UserAuthentication.DeApiAuthenticeertBearerTokens.GeldigTokenVanEenSysteem` | geslaagd |
| `user-authentication` | De API authenticeert bearer-tokens | Ontbrekend token | `Doctrina.Host.IntegrationTests.UserAuthentication.DeApiAuthenticeertBearerTokens.OntbrekendToken` | geslaagd |
| `user-authentication` | De API authenticeert bearer-tokens | Token voor een andere audience | `Doctrina.Host.IntegrationTests.UserAuthentication.DeApiAuthenticeertBearerTokens.TokenVoorEenAndereAudience` | geslaagd |
| `user-authentication` | De API authenticeert bearer-tokens | Sessiecookie wordt niet door de API geaccepteerd | `Doctrina.Host.IntegrationTests.UserAuthentication.DeApiAuthenticeertBearerTokens.SessiecookieWordtNietDoorDeApiGeaccepteerd` | geslaagd |
| `user-authentication` | De ingelogde identiteit is zichtbaar voor de gebruiker | Profielpagina toont de identiteit | `Doctrina.Host.IntegrationTests.UserAuthentication.DeIngelogdeIdentiteitIsZichtbaarVoorDeGebruiker.ProfielpaginaToontDeIdentiteit` | geslaagd |
| `user-authentication` | De ingelogde identiteit is zichtbaar voor de gebruiker | Profielpagina zonder persoonsnummer | `Doctrina.Host.IntegrationTests.UserAuthentication.DeIngelogdeIdentiteitIsZichtbaarVoorDeGebruiker.ProfielpaginaZonderPersoonsnummer` | geslaagd |

Naast deze scenario's zijn er tests voor: het Result-type (Core), de configuratiebestanden en secrets (D13), de
architectuurregels voor `Doctrina.Core` en ruwe Bootstrap, de permissie-claims-transformatie, en de host-UI (skiplink,
foutpagina, "Geen toegang" in de router).

## 11.2 Rooktest tegen de dev-stack

Host gestart met `dotnet run --project src/Doctrina.Host --launch-profile https` (Development, met
`appsettings.Secrets.json` volgens `docs/ontwikkelen/starten.md`), dev-stack met Keycloak 26.6.1. Daarna
`bash dev-stack/scripts/smoke-login.sh`:

| Controle | Uitkomst |
|---|---|
| `student` logt in via Keycloak; naam, `personId` (`I6000001`) en issuer op `/profiel`; geen beheerder | OK |
| API weigert de sessiecookie (`/api/v1/me` → 401) | OK |
| `beheerder` logt in via Keycloak; beheerder = ja | OK |
| nepgebruiker `femke.coenen` logt in; naam en `personId` | OK |
| uitloggen `student`: naar Keycloak-logout, terug naar Doctrina, `/profiel` vraagt weer om inloggen | OK |
| systeem `doctrina-sis-sync` (client credentials): `/api/v1/me` met token 200, zonder token 401 | OK |
| `/scalar` en `/openapi/v1.json` in Development | 200 |
| `/health/live` | 200 |
| `/health/ready` | 503 (`database: Unhealthy`), verwacht: de database `Doctrina` bestaat pas na de eerste module met migraties |

**Entra ID via `IdpHint=entra`: niet uitgevoerd.** Er is geen Entra-tenant ingericht (`dev-stack/.env` ontbreekt, de
realm `doctrina` heeft geen identity provider `entra`). Inrichten: `docs/doctrina-integratie.md`, daarna deze stap
herhalen.
