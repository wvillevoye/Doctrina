# Voorstel

## Why

Doctrina heeft nog geen code. Elk volgend roadmap-item (design system, academische kern, groepen, import, OOAPI, LTI)
hangt van hetzelfde fundament af: een host die onafhankelijke modules laadt, hun gegevens afschermt, ze zonder koppeling
laat communiceren, gebruikers laat inloggen via instelbare OpenID Connect (lokaal Keycloak, later Microsoft Entra ID /
SURFconext), gebruikers en systemen autoriseert en fouten consistent meldt. Door dat eerst te bouwen, en de modulegrenzen
vanaf dag één af te dwingen, hoeft geen volgende change het opnieuw uit te vinden of uit te hollen.
Roadmap: `docs/Doctrina-voorstel.md` §8, rij 1.

## What Changes

- Nieuwe .NET 10-solution zoals beschreven in het voorstel §3.1: `Doctrina.Host`, `Doctrina.Core`, `Doctrina.Infrastructure`,
  `Doctrina.UI` (lege schil tot `add-design-system`), testprojecten en `Doctrina.TestKit`; central package management,
  vastgezette SDK, warnings als errors.
- Modulesysteem: modules melden zich bij de host aan, kunnen via configuratie aan/uit, declareren afhankelijkheden en
  leveren API-endpoints, Razor-componenten, navigatie-items, permissies en health checks.
- Opslag per module op één SQL Server-database: eigen schema, eigen migraties, sequentiële GUID-sleutels, automatisch
  bijgehouden `DateLastModified`.
- In-process domeinevents tussen modules.
- Inloggen van gebruikers via OpenID Connect (authorization code + PKCE) met een provider-neutraal claimcontract
  (`roles`, `name`); bearer-token-authenticatie voor `/api/v1` (gebruikers en systemen met client credentials).
- Autorisatie op basis van permissies: modules declareren permissies, configuratie koppelt waarden van de
  `roles`-claim aan permissies.
- API-conventies: geversioneerde `/api/v1`, OpenAPI-document + Scalar-UI, `Result`/`Result<T>` vertaald naar RFC 9457
  ProblemDetails met stabiele foutcodes, generieke 500 bij onverwachte fouten, Nederlandse/Engelse fouttitels.
- Architectuurtests die de build laten falen als een module naar de implementatie van een andere module verwijst.
- Een minimale host-UI: startpagina, inloggen/uitloggen, pagina "Mijn profiel" met de ingelogde identiteit en permissies.

## Out of scope

- Vormgeving, tokens, `Dc*`-componenten (→ `add-design-system`); de host-UI blijft bewust kaal.
- Domeinmodules (Academic, Groups, …) en het koppelen van een ingelogde gebruiker aan een `Person`-record
  (→ `add-academic-core`).
- Achtergrondtaken / Hangfire (→ `add-api-and-import`), gegarandeerde/outbox-aflevering van events, LTI.
- Rollen per cursus (docent/student per aanbod) en autorisatie per resource (→ `add-academic-core`).
- Multi-tenancy, deployment-pipelines en container-images.
- Het inrichten van de identity providers zelf (de Keycloak-dev-stack is een apart project).

## Capabilities

### New Capabilities
- `module-system`: registreren, aanzetten, ordenen en afschermen van modules en wat ze aan de host leveren.
- `data-persistence`: schema's en migraties per module, sleutelgeneratie en wijzigingsvelden.
- `domain-events`: publiceren en afhandelen van in-process events tussen modules.
- `user-authentication`: gebruikers in- en uitloggen via OpenID Connect en API-aanroepers authenticeren met bearer-tokens.
- `access-control`: door modules gedeclareerde permissies, rollen koppelen aan permissies, en die afdwingen in UI en API.
- `api-conventions`: API-versionering, OpenAPI-documentatie, foutresponses en health-endpoints.

### Modified Capabilities
<!-- geen: er zijn nog geen specs -->

## Impact

- Nieuwe inhoud in `src/` en `tests/`; er is geen bestaande code die geraakt wordt.
- Nieuwe afhankelijkheden (allemaal MIT/Apache-2.0): ASP.NET Core / EF Core 10.0.x, `Microsoft.AspNetCore.OpenApi`,
  `Scalar.AspNetCore`, xUnit, bUnit, een bibliotheek voor architectuurtests (NetArchTest.Rules of
  ArchUnitNET).
- Vereist de lokale dev-stack (Keycloak-realm `doctrina`) voor handmatig inloggen en SQL Server LocalDB
  (`(localdb)\MSSQLLocalDB`) voor de database; geautomatiseerde tests draaien zonder Keycloak (nagebootste provider) en
  op LocalDB, zonder Docker (design D13).
- Configuratie per omgeving in `appsettings.{Development,Test,Production}.json`; secrets in het niet-gecommitte
  `appsettings.Secrets.json` (design D13).
