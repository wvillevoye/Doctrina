# Doctrina

*Doctrina* (Latijn): onderwijs, onderricht, leer — een eenvoudig, modulair LMS op .NET 10 / Blazor, spec-driven gebouwd met [OpenSpec](https://github.com/Fission-AI/OpenSpec).

**Status:** in opbouw. Change `add-modular-foundation`: solution-basis en inloggen (OIDC via Keycloak, API met bearer-tokens) zijn gebouwd; modules, database, events, autorisatie en API-conventies volgen.

| | |
|---|---|
| Voorstel & architectuur | [docs/Doctrina-voorstel.md](docs/Doctrina-voorstel.md) |
| Inloggen (Keycloak → Entra ID), claims | [docs/doctrina-integratie.md](docs/doctrina-integratie.md) |
| Huisregels voor ontwikkelaars en AI | [AGENTS.md](AGENTS.md) |
| Specs / changes | `openspec/specs/`, `openspec/changes/` |

## Werken met OpenSpec

    openspec list                                   # lopende changes
    openspec show add-modular-foundation            # change bekijken
    openspec validate add-modular-foundation --strict

In Claude Code: `/opsx:explore`, `/opsx:propose "<idee>"`, `/opsx:apply`, `/opsx:archive`.

## Zo start je (inloggen via Keycloak)

    cd dev-stack; docker compose up -d; cd ..          # Keycloak, Mailpit (database: LocalDB)
    copy src\Doctrina.Hostppsettings.Secrets.example.json src\Doctrina.Hostppsettings.Secrets.json
    dotnet run --project src/Doctrina.Host --launch-profile https

Open https://localhost:5001 → **Inloggen** → bijv. `student` / `student`, `beheerder` / `beheerder` of `femke.coenen` / `test123`.
Via Entra ID: zet `Authentication:Oidc:IdpHint` op `entra` (na `dev-stack/scripts/configure-entra.ps1`).

    dotnet test                                         # tests (zonder Keycloak en Docker; nagebootste provider, LocalDB)
    bash dev-stack/scripts/smoke-login.sh               # rooktest tegen de echte Keycloak (Git Bash, Host moet draaien)

## Lokale omgeving

De dev-stack (Keycloak, Mailpit) staat in [`dev-stack/`](dev-stack/README.md): `cd dev-stack; docker compose up -d`.
De database is SQL Server LocalDB `(localdb)\MSSQLLocalDB`. Configuratie: `appsettings.json` +
`appsettings.{Development,Test,Production}.json`; secrets in het niet-gecommitte `appsettings.Secrets.json`.
