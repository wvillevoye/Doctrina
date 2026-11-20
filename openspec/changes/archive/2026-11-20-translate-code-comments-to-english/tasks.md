# Taken

Elke groep eindigt met `dotnet build` (warnings = errors) en de tests van de geraakte projecten; controleer dat beide
slagen. Vertaal volgens design D1 en D2 (niets inhoudelijks wijzigen, specnamen letterlijk laten staan). Sla
`Migrations/` en `src/Doctrina.UI/Styles/vendor/` over.

## 1. Bronprojecten

- [x] 1.1 Zet het commentaar in `src/Doctrina.Core` en `src/Doctrina.Infrastructure` om naar Engels; controleer met `dotnet build` en `dotnet test tests/Doctrina.Core.Tests tests/Doctrina.Architecture.Tests` dat alles slaagt
- [x] 1.2 Zet het commentaar in `src/Doctrina.UI` (componenten, `Theming`, eigen `Styles/*.scss`, `wwwroot/js`, `wwwroot/css`) om naar Engels; controleer met `dotnet build` en `dotnet test tests/Doctrina.UI.Tests`
- [x] 1.3 Zet het commentaar in `src/Doctrina.Host` om naar Engels (`.cs` en `.razor`, inclusief de stijlgids); controleer met `dotnet build` en `dotnet test tests/Doctrina.Host.IntegrationTests`
- [x] 1.4 Zet het commentaar in `src/Modules/Academic`, `Content`, `Groups`, `Interop` en `Lti` om naar Engels; controleer met `dotnet build` en `dotnet test tests/Modules`

## 2. Testprojecten en scripts

- [x] 2.1 Zet het commentaar in `tests/Doctrina.TestKit`, `tests/Doctrina.Architecture.Tests.Fixtures`, `tests/Doctrina.Architecture.Tests`, `tests/Doctrina.Core.Tests` en `tests/Doctrina.UI.Tests` om naar Engels; laat testnamen ongemoeid; controleer met `dotnet test` voor die projecten
- [x] 2.2 Zet het commentaar in `tests/Doctrina.Host.IntegrationTests` en `tests/Modules/*` om naar Engels; laat testnamen ongemoeid; controleer met `dotnet test` voor die projecten
- [x] 2.3 Zet het commentaar in `dev-stack/scripts/generate-realm.ps1`, `dev-stack/scripts/smoke-login.sh`, `dev-stack/samples/Doctrina.AuthCheck` (`Program.cs`, `test-e2e.sh`) en de twee `Directory.*.props` in `dev-stack/` om naar Engels; controleer dat `generate-realm.ps1` nog hetzelfde realm-bestand oplevert (`git diff --exit-code` op het gegenereerde `realm`-bestand na een run) en dat `bash -n dev-stack/scripts/smoke-login.sh` slaagt
- [x] 2.4 Zet het XML-commentaar in de `.csproj`- en `Directory.*.props`-bestanden onder `src/` en `tests/` om naar Engels (bij het apply-werk ontdekt: de eerste inventarisatie keek niet naar XML); controleer met `dotnet build`

## 3. Afronding

- [x] 3.1 Draai de diffcontrole uit design D3 over de hele wijziging; controleer dat de uitvoer leeg is of alleen commentaar achter code en meerregelige blokken bevat die met de hand zijn nagekeken
- [x] 3.2 Zoek met de woordenlijst uit design D5 in commentaarregels onder `src/`, `tests/` en `dev-stack/` (zonder `Migrations/` en `vendor/`); controleer dat er alleen aangehaalde specnamen en eigennamen overblijven
- [x] 3.3 Draai `dotnet test` voor de hele solution en `openspec validate translate-code-comments-to-english --strict`; controleer dat alles slaagt
- [x] 3.4 Noteer in de samenvatting van de apply-stap welk commentaar achterhaald leek maar bewust is blijven staan (design D1)

## Workflow follow-up

- Beoordeel de change vóór `/opsx:apply`.
- Archiveren zonder spec-sync (`skip_specs`): geen hoofdspec wijzigt.
