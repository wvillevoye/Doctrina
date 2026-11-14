# Taken

## 1. Hangfire

- [x] 1.1 Voeg `Hangfire.Core`, `Hangfire.SqlServer` en `Hangfire.AspNetCore` (laatste stabiele 1.8.x) toe aan `Directory.Packages.props` en maak `AddDoctrinaJobs()`/`MapDoctrinaJobsDashboard()` in `Doctrina.Infrastructure` (design D2), geregistreerd in `Program` ná `ModuleMigrationService`; controleer met integratietests `AchtergrondtakenDraaienMetHangfire.IngeplandeTaakWordtUitgevoerd` (een testtaak via `IBackgroundJobClient` die een vlag zet) en `…SchemaVanHangfire`
- [x] 1.2 Beveilig het dashboard (D2); controleer met integratietests `HetDashboardIsAlleenVoorBeheerders.BeheerderOpentHetDashboard` en `…StudentOpentHetDashboard`, plus 401 voor anoniem

## 2. Jobs en verwerking

- [x] 2.1 Voeg `ImportJob` en `ImportJobRow` toe met migratie (D3) en `ImportJobs.CreateAsync`/`RepeatAsync` (D6) met foutcodes en titels; controleer met LocalDB-tests dat een job `Queued` wordt opgeslagen en ingepland, dat herhalen een nieuwe job zonder dry-run maakt en dat herhalen zonder upload `Academic.Import.UploadExpired` geeft
- [x] 2.2 Maak het lezen van een upload (losse CSV of zip met/zonder manifest, D5) en pas `CsvReader` aan op een grens per upload; controleer met unit-tests `EenZipBevatMeerdereTypenInEenVasteVolgorde.ManifestMetEigenBestandsnamen` en `…OnbekendBestand`, de volgorde ongeacht de zip, genegeerde mappen en niet-CSV-bestanden, en de grens op uitgepakte grootte
- [x] 2.3 Maak `ImportRunner` (D4) en verwijder `AcademicImporter` (D10); controleer met LocalDB-tests `EenImportDraaitAlsAchtergrondjob.JobDoorlooptDeStatussen` en `…OntbrekendeVerplichteKolom`, `HetRapportToontHetResultaatPerRegel.EenFouteRegelTussenGoede`, `DeImportHeeftEenGrens.TeGrootBestand`, `EenZipBevatMeerdereTypenInEenVasteVolgorde.ZipMetVerwijzingenTussenBestanden`, `EenDryRunLaatZienWatErZouGebeurenZonderOpTeSlaan.DryRunVanEenZip`, plus `ImporterenIsIdempotent.ZelfdeBestandOpnieuw` op de runner en een herstart (job al `Running`) die opnieuw begint
- [x] 2.4 Maak de bewaartermijn (D9); controleer met test `GeuploadeBestandenWordenNaDeBewaartermijnVerwijderd.OudeUploadOpruimen` (met `FakeTimeProvider`) en dat de terugkerende taak bij het opstarten is geregistreerd

## 3. API

- [x] 3.1 Maak de import-endpoints (D8) en de rolkoppeling `doctrina.import` in `appsettings.Development.json`; controleer met integratietests `SystemenImporterenViaDeApi.ImportViaDeApi` (wacht op `completed`) en `…ZonderAcademicImport`, `DeImportApiVraagtAcademicImportEnGebruiktDeBronVanDeClient.BronVanDeClient`, `ErIsEenLeegSjabloonPerType.SjabloonVoorPersonen`, het rapport als JSON en als CSV (`HetRapportToontHetResultaatPerRegel.RapportAlsCsv`), en 404 voor een onbekende job

## 4. Schermen

- [x] 4.1 Vervang `/academic/import` en maak `/academic/imports/{id}`, `/academic/imports` en de JS-download (D7); controleer met bUnit-tests dat uploaden een job maakt en naar de jobpagina gaat, dat de jobpagina de aantallen en mislukte regels toont en na een dry-run "Import for real" (`EenDryRunLaatZienWatErZouGebeurenZonderOpTeSlaan.NaEenDryRunEchtImporteren`) een nieuwe job start, en met integratietests `BeheerdersImporterenEenCsvBestandPerType.PersonenImporteren` (wacht op de job) en `…ImportZonderPermissie`
- [x] 4.2 Werk de testdatabestanden en documentatie bij: `docs/ontwikkelen/starten.md` §5 (de zes CSV's als één zip met dry-run eerst; het script maakt `dev-stack/data/academic-testdata.zip` erbij) en `docs/doctrina-integratie.md` (import-API, nieuwe tekst in het Engels); controleer dat de zip van de dev-stack in een test foutloos importeert

## 5. Afronding

- [x] 5.1 Draai `dotnet test` voor de hele solution en `openspec validate add-bulk-import --strict`; controleer dat alles slaagt en dat elk scenario uit de spec-delta een test heeft
- [x] 5.2 Handmatig met de lokale host: als `beheerder` de testdata-zip eerst als dry run, dan echt importeren; het rapport downloaden; het Hangfire-dashboard openen; via de API met `doctrina-sis-sync` een CSV importeren en de status volgen

## Vervolg in de workflow

- Beoordeel de change vóór `/opsx:apply`.
- Bouwen op `feature/add-bulk-import` (voortbouwend op `feature/add-rest-api`).
- Archiveren pas na `add-academic-core`.
