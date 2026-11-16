# Voorstel

## Why

Doctrina heeft een fundament (modules, database, inloggen, autorisatie) en een design system, maar nog geen onderwijsdata.
Zonder cursussen, uitvoeringen, personen en inschrijvingen is er niets om een LMS omheen te bouwen: groepen, content,
LTI en templates hangen allemaal aan een `CourseOffering` en de mensen die erin zitten. Deze change is rij 3 van de
roadmap (`docs/Doctrina-voorstel.md` §8, `add-academic-core`) en de eerste productmodule.

Afspraken die al vastliggen: het model volgt OOAPI v5 (§4), personen komen uit het SIS en niet van de identity provider
(§4.4), en ze komen binnen via de CSV-route (§6.1).

## What Changes

- **Nieuwe module `Academic`** (`src/Modules/Academic`, schema `academic`, eigen DbContext en migraties) met de
  kern van het OOAPI-model: `Organization`, `AcademicSession`, `Course`, `CourseOffering`, `Person` en `Association`
  (inschrijving van een persoon in een uitvoering, met rol). Alle zes zijn uitwisselbaar (`ExternalId`, `Source`,
  `Status`, `DateLastModified`). Verwijzingen tussen records lopen bij invoer via `ExternalId`.
- **Eén validator en één upsert-service per type**, met `Result` en foutcodes `Academic.*`. Upsert matcht op
  `ExternalId` + `Source`. De CSV-import van deze change gebruikt ze; de REST-endpoints en de generieke import van
  `add-api-and-import` (§8 rij 5) hergebruiken ze later (huisregel 12).
- **Eenvoudige CSV-import** op een beheerscherm: één bestand per type, synchroon verwerkt, met een rapport per regel
  op het scherm. Foute regels stoppen de rest niet; hetzelfde bestand twee keer geeft geen dubbele data.
- **Beheerschermen, alleen-lezen**: per type een lijst met zoeken en paginering, en een detailpagina. Wijzigen gaat via
  de bron (CSV, later REST). Achter de permissie `Academic.Read`; importeren achter `Academic.Import`.
- **Koppeling van login aan persoon**: zodra een pagina de persoon nodig heeft, zoekt Doctrina de persoon met `ExternalId` = claim `personId`
  en onthoudt daarna issuer + subject. Wie geen persoon heeft, krijgt een duidelijke melding; er wordt nooit een
  persoon aangemaakt bij het inloggen (§4.4).
- **"My courses"**: een pagina (permissie `Academic.MyCourses`, standaard voor `employee` en `student`) met de actieve uitvoeringen waarin de gebruiker een actieve
  inschrijving heeft, met de eigen rol.
- **Dev-stack**: `generate-realm.ps1` maakt naast de realm ook CSV-bestanden in Doctrina-formaat (personen met hetzelfde
  `personId` als in Keycloak, plus een kleine set organisaties, perioden, cursussen, uitvoeringen en inschrijvingen),
  zodat de testaccounts na import direct "My courses" kunnen gebruiken.

## Buiten de scope

- `Program`/`ProgramOffering` en `Component`/`ComponentOffering`: in een vervolgchange.
- REST-endpoints per resource, batch, `modifiedSince`, de generieke asynchrone import (zip, manifest, dry-run, Hangfire,
  opgeslagen rapporten, `fullSync`) en scopes voor systemen: `add-api-and-import`.
- OOAPI-endpoints aanbieden of consumeren: `add-ooapi-interop`.
- Aanmaken, bewerken of verwijderen van records in de UI. Ook records met `Source` = Doctrina krijgen in deze change
  geen formulieren.
- Groepen, content, LTI en templates (eigen changes).
- Rechten per cursus (bv. een docent die alleen zijn eigen uitvoeringen beheert). In deze change bepaalt de rol uit het
  token (via `Authorization:RolePermissions`) wat iemand in de beheerschermen mag.

## Capabilities

### New Capabilities
- `academic-data`: het kernmodel, validatie, upsert op `ExternalId` + `Source` en verwijzingen tussen records.
- `academic-csv-import`: CSV-bestanden per type importeren met een rapport per regel.
- `academic-admin`: alleen-lezen beheerschermen met zoeken en detail.
- `person-linking`: een login koppelen aan een persoon uit het SIS.
- `my-courses`: de pagina "My courses".

### Modified Capabilities
- `access-control`: een rolkoppeling naar een permissie van een bekende maar uitgeschakelde module wordt genegeerd in
  plaats van het opstarten te laten mislukken. Zo kan `appsettings.Development.json` `Academic.MyCourses` koppelen en
  kan een ontwikkelaar de module toch uitzetten (design D8).

## Impact

- Nieuwe projecten `src/Modules/Academic/Doctrina.Modules.Academic` en `.Contracts`, met testprojecten
  `tests/Modules/Doctrina.Modules.Academic.Tests` (unit) en `.IntegrationTests` (LocalDB).
- `src/Doctrina.Host`: module registreren in `KnownModules`, aanzetten in `appsettings.Development.json`,
  rolkoppeling voor `Academic.MyCourses`. Geen wijziging in de inlogcode zelf: de koppeling zit in de module.
- `dev-stack/scripts/generate-realm.ps1` en `dev-stack/README.md`: CSV-uitvoer.
- Documentatie: `docs/ontwikkelen/starten.md` (data importeren), `src/Modules/README.md`.
- Database: nieuw schema `academic`. Geen wijziging in bestaande schema's, API-contracten of Keycloak-configuratie.
