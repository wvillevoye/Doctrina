# Voorstel

## Why

Volgens de taalafspraak van 2026-11-12 (`AGENTS.md`) is codecommentaar Engels. Bestaand commentaar is nog Nederlands:
zo'n 170 bestanden in `src/` en zo'n 120 in `tests/` hebben XML-doccomments of gewone commentaarregels in het
Nederlands. Nieuw commentaar is Engels, dus veel bestanden bevatten nu een mengvorm. Dat leest slecht, en
wie een bestand aanraakt moet kiezen welke taal hij volgt. De afspraak laat bestaand commentaar staan "tot het om een
andere reden wordt herschreven"; dat gebeurt niet vanzelf en laat de mengvorm onbeperkt bestaan. Omzetten kan nu nog in
één keer, zonder gedragsrisico, omdat commentaar niet compileert tot uitvoerbare code.

Deze change komt niet uit de roadmap (`docs/Doctrina-voorstel.md` §8). Ze volgt uit de taalafspraak en vult
`switch-ui-to-english` aan, dat commentaar bewust buiten de scope hield.

## What Changes

- **Commentaar Engels**: alle Nederlandse commentaar in de code wordt Engels, met behoud van betekenis. Dat zijn
  XML-doccomments (`///`), regelcommentaar (`//`), Razor-commentaar (`@* *@`), HTML-commentaar in `.razor` en
  commentaar in `.scss`, `.css`, `.js`, `.ps1`, `.sh`, `.csproj` en `.props` in `src/`, `tests/` en `dev-stack/`.
- **Verwijzingen blijven gelijk**: commentaar dat naar een spec, requirement, scenario of design verwijst
  (bv. `spec ui-components "De stijlgids toont elk component", design D11`) houdt de Nederlandse naam van het
  requirement of scenario, tussen aanhalingstekens, omdat die naam in de spec staat en erop gezocht wordt. De zin eromheen
  wordt Engels.
- **Geen gedragswijziging**: alleen commentaar verandert. Een controle op de diff (zie `tasks.md`) laat zien dat er
  geen regel buiten commentaar wijzigt.

## Buiten de scope

- Identifiers: namen van klassen, methoden, parameters en bestanden blijven zoals ze zijn, ook de Nederlandse
  (`StijlgidsSectie`, `FormulierVoorbeeld`, `Cursus`, `Stijlgids.Persoon.Invalid`). Hernoemen is een refactor met
  eigen risico en past niet bij "commentaar".
- Testnamen en testklassen: die volgen de Nederlandse requirement- en scenarionamen (huisregel 10).
- OpenSpec-artefacten (Nederlands, besluit 2026-11-12) en bestaande Nederlandse documentatie in `docs/` en README's.
- Tekenreeksen in code: foutmeldingen van exceptions voor ontwikkelaars, API-fouttitels in `*Errors.resx`,
  fallbacktitels en testdata. Dat is geen commentaar.
- EF Core-migraties (`Migrations/`): gegenereerd, zonder handgeschreven commentaar.
- Configuratiebestanden in de repo-root (`Directory.*.props`, `nuget.config`, `.editorconfig`, `.gitignore`, `.gitattributes`): vallen buiten `src/`, `tests/` en `dev-stack/`; apart te beslissen.
- Vendored bronnen van derden, zoals Bootstrap in `src/Doctrina.UI/Styles/vendor/`: die blijven ongewijzigd.

## Capabilities

### New Capabilities
<!-- geen -->

### Modified Capabilities
<!-- geen: er verandert geen requirement; daarom `skip_specs: true` in .openspec.yaml -->

## Impact

- `src/` (alle projecten, ongeveer 170 bestanden, zonder `Styles/vendor`), `tests/` (ongeveer 120 bestanden) en `dev-stack/scripts/`.
- Geen wijziging in gedrag, database, API-contract, UI of Keycloak-configuratie. Geen nieuwe tests; de bestaande tests
  en de build bewijzen dat er niets kapot ging.
- Het is een grote diff in veel bestanden. Dat maakt openstaande branches kwetsbaar voor mergeconflicten; er zijn nu geen
  open changes, dus het moment is gunstig.
