# Voorstel

## Why

De CSV-import van `add-academic-core` verwerkt een bestand synchroon binnen de pagina, één type per keer, tot 10.000
regels, en het rapport verdwijnt als de pagina sluit. Voor een volledige aanlevering uit het SIS (alle personen en
inschrijvingen van een instelling) is dat te klein en te kwetsbaar: een groot bestand houdt de verbinding minutenlang
bezet, zes bestanden moeten in de juiste volgorde met de hand, er is geen manier om eerst te zien wat er zou gebeuren,
en een systeem kan niet via de API importeren.

Deze change is de tweede helft van rij 5 van de roadmap (`docs/Doctrina-voorstel.md` §8; inhoud in §6.6 B, C, D en E).
De eerste helft, de REST-endpoints, is `add-rest-api`.

## What Changes

- **Asynchrone imports met Hangfire**: een upload maakt een importjob (`queued` → `running` → `completed`/`failed`) die
  op de achtergrond draait. Doctrina krijgt daarmee een jobinfrastructuur (Hangfire, schema `hangfire`) die latere
  changes ook kunnen gebruiken. Het Hangfire-dashboard staat op `/hangfire`, alleen voor beheerders (rol `admin`).
- **Zip met manifest**: één upload met meerdere CSV's. De volgorde komt uit de typen (organizations → academic-sessions →
  courses → course-offerings → persons → associations), niet uit de zip. Het type per bestand komt uit `manifest.csv`
  (`file,type`) of, zonder manifest, uit de bestandsnaam (`persons.csv`).
- **Dry-run**: dezelfde import, die aan het eind wordt teruggedraaid. Het rapport laat precies zien wat er zou gebeuren,
  inclusief verwijzingen tussen bestanden in dezelfde zip. Daarna kan dezelfde upload echt worden geïmporteerd.
- **Opgeslagen rapporten**: per regel bestand, regelnummer, sleutel, resultaat en foutcode, als JSON en als CSV te
  downloaden. Het geüploade bestand wordt na `Academic:Import:RetentionDays` (standaard 30) verwijderd; job en rapport
  blijven.
- **Grenzen hoger**: standaard 100.000 regels en 50 MB per upload, nog steeds instelbaar. Elke regel gaat via de bestaande
  upsert (één schrijfpad, huisregel 12).
- **API** (§6.6 B): `POST /api/v1/imports` (multipart, `?dryRun=true`) geeft 202 met de job;
  `GET /api/v1/imports/{id}`, `GET /api/v1/imports/{id}/report` (JSON, of CSV met `?format=csv`) en
  `GET /api/v1/imports/templates/{type}.csv`. De bron komt van de client (`Api:Sources`), net als bij REST. Upload is
  rate-limited zoals de batch-endpoints. Scope `doctrina.import` geeft in Development `Academic.Import`.
- **Importscherm vervangen** (§6.6 E): `/academic/import` uploadt (CSV of zip, met optie "dry run") en gaat naar de
  jobpagina `/academic/imports/{id}` met voortgang, samenvatting, rapport en download. Na een dry-run staat daar een knop
  om dezelfde upload echt te importeren. `/academic/imports` toont de recente imports. Sjablonen zijn op het scherm te
  downloaden.

## Buiten de scope

- `fullSync` (records van de bron die niet in het bestand staan op `toBeDeleted` zetten) en de `Idempotency-Key`-header:
  op keuze van de gebruiker niet in deze change.
- Bulk via `SqlBulkCopy` naar staging en `MERGE` (§6.6 C): ook grote bestanden gaan per regel via de upsert. Pas als
  metingen erom vragen.
- OneRoster 1.2-CSV (`Interop.OneRoster`).
- Groepen importeren.
- Een eigen module `Import` (§3.1): de import blijft in Academic, het enige module met importeerbare data (design D1).

## Capabilities

### New Capabilities
- `background-jobs`: Hangfire als jobinfrastructuur van Doctrina, met het dashboard alleen voor beheerders.

### Modified Capabilities
- `academic-csv-import`: de import wordt een asynchrone job met dry-run, zip en opgeslagen rapporten, hogere grenzen en
  een API; de requirements over het importscherm, het rapport en de grens wijzigen.

`academic-csv-import` komt uit `add-academic-core`; die change moet eerst gearchiveerd zijn.

## Impact

- Nieuwe packages: `Hangfire.Core`, `Hangfire.SqlServer` en `Hangfire.AspNetCore` (LGPL-3.0, OSI-goedgekeurd; zoals
  voorzien in §2 van het voorstel).
- `Doctrina.Infrastructure`: Hangfire-registratie, storage in schema `hangfire`, dashboard met autorisatie.
- `Doctrina.Modules.Academic`: tabellen voor jobs, bestanden en rapportregels (migratie), de jobverwerking, API-endpoints,
  het nieuwe importscherm en de jobpagina's. De synchrone importservice verdwijnt; `CsvReader` en de upsert blijven.
- `src/Doctrina.Host`: Hangfire aanzetten, rolkoppeling `doctrina.import` in `appsettings.Development.json`.
- Documentatie: `docs/ontwikkelen/starten.md` (§5 testdata importeren als zip), `docs/doctrina-integratie.md` (import-API).
