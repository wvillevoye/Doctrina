# AGENTS.md — Doctrina

Doctrina is een modulair LMS (modulaire monoliet) op .NET 10 / Blazor Web App, spec-driven gebouwd met OpenSpec.
Achtergrond: [docs/Doctrina-voorstel.md](docs/Doctrina-voorstel.md). Inloggen/claims: [docs/doctrina-integratie.md](docs/doctrina-integratie.md).

## Werkwijze (OpenSpec)

- Projectcontext en regels voor artefacten: `openspec/config.yaml`.
- Nieuwe functionaliteit: eerst een change (`/opsx:propose` of `openspec new change <id>`), pas bouwen na akkoord (`/opsx:apply`).
- Valideren: `openspec validate <change-id> --strict`. Afronden: `/opsx:archive`.
- Talen (besluit 2026-11-12):
  - OpenSpec-artefacten (proposal, design, tasks, specs) in het Nederlands. De structuurwoorden die de validator nodig
    heeft blijven Engels: `## Why`, `## What Changes`, `## Purpose`, `## ADDED Requirements`, `### Requirement:`,
    `#### Scenario:`, SHALL/MUST en GIVEN/WHEN/THEN. Testnamen volgen de (Nederlandse) requirement- en scenarionamen.
  - Nieuwe documentatie (`docs/`, README's) en codecommentaar in het Engels; bestaande Nederlandse documentatie blijft
    staan tot ze om een andere reden wordt herschreven.
  - De gebruikersinterface is Engels (teksten op pagina's en in `Dc*`-componenten). De omzetting van de bestaande
    Nederlandse UI gebeurt in een eigen change.

## Huisregels

1. Geen code zonder goedgekeurde change; lees eerst `openspec/config.yaml` en de relevante specs in `openspec/specs/`.
2. Elke feature = een module; geen verwijzingen naar de implementatie van andere modules (alleen `.Contracts` of events).
3. Elke module heeft een eigen SQL-schema, eigen DbContext en eigen migraties.
4. Geen ruwe Bootstrap-classes of hardcoded kleuren in modules; alleen `Doctrina.UI`-componenten en `--dc-*`-tokens.
5. Entiteit-Id's zijn `uniqueidentifier`, gegenereerd door EF Core (sequentieel) — nooit `Guid.NewGuid()` of `Guid.CreateVersion7()` voor sleutels.
6. Uitwisselbare entiteiten hebben altijd `Id`, `ExternalId`, `Status`, `DateLastModified` (UTC) en `Source`.
7. Standaard eerst (OOAPI v5, LTI 1.3, …): volg naamgeving en datamodel van de standaard.
8. Geen commercieel gelicenseerde packages zonder akkoord (o.a. MediatR v13+, AutoMapper v15+, EFCore.BulkExtensions).
9. Async + `CancellationToken`, nullable aan, warnings = errors.
10. Elk spec-scenario heeft minstens één test; de testnaam verwijst naar requirement/scenario.
11. Verwachte fouten via `Result`/`Result<T>` met stabiele foutcodes (module-prefix, bv. `Academic.Course.NotFound`); de API vertaalt naar ProblemDetails via `ToHttpResult()`.
12. REST-endpoint en CSV-import gebruiken dezelfde validator en upsert-service — nooit validatielogica dupliceren.

## Techniek in het kort

| | |
|---|---|
| Runtime | .NET 10 (`net10.0`), ook al bestaat .NET 11; Microsoft-packages op `10.0.x` |
| UI | Blazor Web App, Interactive Server; Bootstrap 5.3 via SCSS achter `Doctrina.UI` |
| API | Minimal APIs onder `/api/v1`, OpenAPI + Scalar |
| Data | EF Core 10, SQL Server (lokaal en in tests: LocalDB `(localdb)\MSSQLLocalDB`), één schema per module |
| Auth | OIDC (Keycloak lokaal → Entra ID / SURFconext), `roles`-claim; systemen via client credentials |
| Tests | xUnit, bUnit, LocalDB (geen Docker), architectuurtests; omgeving `Test` |

## Lokaal ontwikkelen

De dev-stack (Keycloak 26, Mailpit) staat in `dev-stack/`: `cd dev-stack; docker compose up -d`
(compose-project `doctrina-dev`, zie `dev-stack/README.md`). Realm `doctrina`, client `doctrina-web`, testaccounts `beheerder`, `docent`, `student`
(wachtwoord = naam) en 250 nepgebruikers (`test123`). Doctrina.Host draait op `https://localhost:5001`.

Configuratie: `appsettings.json` + `appsettings.{Development,Test,Production}.json` (`Test` = de geautomatiseerde tests).
Secrets (client secrets, wachtwoorden) **alleen** in `appsettings.Secrets.json` — staat in `.gitignore`, voorbeeld in
`appsettings.Secrets.example.json`; nooit in de andere appsettings-bestanden.
