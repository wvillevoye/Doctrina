# Proposal

## Why

Doctrina biedt het onderwijskernmodel sinds `add-ooapi-interop` alleen-lezen aan als OOAPI v5, maar kan zelf geen
OOAPI-bron lezen. Instellingen vullen Doctrina nu met CSV-bestanden (`add-bulk-import`) of via de eigen REST-API. Veel
SIS'en (en de SURF/Edu-V-koppelingen) spreken OOAPI; die bron rechtstreeks bevragen scheelt een exportstap en maakt
een periodieke, herhaalbare sync mogelijk. De change volgt op rij 6 van de fasering (§8 van
`docs/Doctrina-voorstel.md`), waar "consumeren later" stond, en wordt als rij 6a toegevoegd.

## What Changes

- De module `Ooapi` (`Doctrina.Modules.Interop.OOAPI`) krijgt een **OOAPI-consument**: per geconfigureerde bron
  (`Ooapi:Sources:<naam>`) leest ze `organizations`, `academic-sessions`, `courses`, de `offerings` van elke cursus,
  `persons` en de `associations` van elke uitvoering uit een OOAPI v5-bron, met paginering en OAuth 2.0 client credentials.
- De gelezen records worden naar de veldnamen van de CSV-import vertaald en gaan via **dezelfde validator en
  upsert-service** als de CSV-import en de REST-API (huisregel 12), met `Source` gelijk aan de naam van de bron.
- Een sync is een **importjob**: dezelfde jobs, rapportage (per record `created`, `updated`, `unchanged`, `failed`) en
  dry-run als bij de CSV-import, zichtbaar op de bestaande importpagina's.
- Een sync draait **volgens schema** (Hangfire, cron per bron) en **op verzoek** via een nieuw endpoint, achter de
  permissie `Ooapi.Sync`.
- `Doctrina.Modules.Academic.Contracts` krijgt `IAcademicRecordImport`, waarmee een andere module een reeks records
  aanlevert zonder naar de implementatie van Academic te verwijzen.
- Documentatie (Engels): een bron instellen, secrets, schema en rapport.

**Buiten de scope:**
- Records die in de bron verdwijnen automatisch verwijderen of op `toBeDeleted` zetten (design D7); een sync maakt en wijzigt alleen.
- Delta-sync: OOAPI v5 kent geen `modifiedSince`; elke run leest de volledige bron.
- OOAPI-resources die Doctrina niet heeft (`programs`, `components`, `groups`, …) en veldwaarden die Doctrina niet bewaart.
- Schrijven naar een OOAPI-bron, en een beheerscherm voor bronnen (configuratie in `appsettings`; de uitkomst staat op de bestaande importpagina's).
- Authenticatie anders dan client credentials (geen basic auth, geen API-sleutel).
- Een bepaald SIS als doel: de consument volgt de standaard; leveranciersspecifieke afwijkingen volgen per bron in een eigen change.

## Capabilities

### New Capabilities
- `ooapi-consumer`: Doctrina leest een OOAPI v5-bron periodiek of op verzoek uit en voert de records via de
  gedeelde validatie en upsert in Academic in, met een rapport per record.

### Modified Capabilities

## Impact

- `src/Modules/Interop/Doctrina.Modules.Interop.OOAPI`: client, mapping, sync-job en endpoint; `OoapiModule` registreert opties, `HttpClient` en het recurring job.
- `src/Modules/Academic`: `IAcademicRecordImport` in `.Contracts`, implementatie op de bestaande `ImportRunner`-logica (jobrapportage zonder upload).
- Host: `Ooapi:Sources` in `appsettings`; secrets alleen in `appsettings.Secrets.json`; permissie `Ooapi.Sync` in `Authorization:RolePermissions`.
- Geen nieuwe packages (`HttpClient`, `System.Text.Json` en Hangfire zijn er al). Geen schemawijziging: de sync gebruikt de bestaande tabellen van de importjobs.
- `docs/Doctrina-voorstel.md` §6.7/§8 en `docs/doctrina-integratie.md`.
