# Design

## Context

Zie `proposal.md`. Wat er al is:

- `CsvReader` (UTF-8 met of zonder BOM, `,` of `;`, kolommen op naam, grens op regels en bytes) en per type de upsert
  van `AcademicType`. Beide worden ook door REST gebruikt (`add-rest-api`).
- `AcademicImporter` verwerkt nu synchroon één bestand binnen de pagina `/academic/import`; het rapport staat alleen in
  het geheugen.
- `ApiSource` (bron per client), de rate limiting-policy `api-batch` en de permissie `Academic.Import`.
- `ModuleMigrationService` migreert in `StartAsync` van een hosted service. Hosted services starten na elkaar, in de
  volgorde waarin ze zijn geregistreerd.
- Modules kunnen alleen endpoints onder `/api/v1` registreren, en die vragen een bearer-token. Een download voor een
  ingelogde gebruiker (cookie) kan dus niet via die route.

## Goals / Non-Goals

**Goals:**
- Eén importpad voor scherm en API: upload → job → verwerking per regel via de bestaande upsert → opgeslagen rapport.
- Een dry-run die exact laat zien wat de echte import zou doen.

**Non-Goals:**
- Bulk via staging en `MERGE`, `fullSync`, `Idempotency-Key` (proposal).
- Een generieke importmodule voor meerdere modules (D1).

## Decisions

### D1: De import blijft in Academic

§3.1 van het voorstel noemt een module `Import`. Op dit moment is Academic de enige module met importeerbare data. Een
aparte module zou een contract nodig hebben voor typen, velden, upsert en dry-run over een modulegrens heen, en daarmee
ook transacties over twee DbContexts. Afwijking: de import, met jobs, rapporten, API en schermen, staat in Academic.
Pas als een tweede module (bv. Groups) moet kunnen importeren, wordt het geheel naar een eigen module verplaatst. Het
contract kan dan worden afgeleid uit wat `AcademicType` nu al heeft.

### D2: Hangfire in Infrastructure

`Doctrina.Infrastructure` krijgt `AddDoctrinaJobs()` en `MapDoctrinaJobsDashboard()`:

- `Hangfire.SqlServer` met `ConnectionStrings:Doctrina`, `SchemaName = "hangfire"`, `PrepareSchemaIfNecessary = true`.
- `AddHangfireServer()` wordt in `Program` geregistreerd ná `ModuleMigrationService`. Zo bestaat de database (in
  Development en tests maakt de eerste migratie haar aan) voordat Hangfire zijn schema aanmaakt.
- Het dashboard staat op `/hangfire` met een autorisatiefilter: ingelogd en `IsInRole("admin")`. Anoniem geeft 401,
  anderen krijgen 403.

Modules gebruiken `IBackgroundJobClient` en `IRecurringJobManager` direct; een eigen abstractie voor één implementatie
is niet nodig. Versies volgens `Directory.Packages.props`, vastgezet op de laatste stabiele 1.8.x.

Alternatief: een eigen `BackgroundService` met een wachtrijtabel. Afgewezen op keuze van de gebruiker: retries,
herstart, zichtbaarheid en terugkerende taken zouden we dan zelf bouwen.

### D3: Datamodel in schema `academic`

| Tabel | Velden |
|---|---|
| `ImportJobs` (`Entity`) | `Status` (`Queued`, `Running`, `Completed`, `Failed`), `DryRun`, `Source`, `StartedBy` (naam of client), `CreatedAt`, `StartedAt?`, `FinishedAt?`, `Message?`, `UploadName`, `UploadType?` (bij een losse CSV), `Upload` (`varbinary(max)`, null na de bewaartermijn), `Created`/`Updated`/`Unchanged`/`Failed` (aantallen), `RepeatOfId?` |
| `ImportJobRows` (`Entity`) | `ImportJobId` (FK, cascade), `File`, `Line`, `Key?`, `Outcome`, `Code?`, `Message?`; index op (`ImportJobId`, `File`, `Line`) |

De upload staat in de database in plaats van op schijf. Dan is er geen gedeelde opslag nodig tussen de webserver en de
jobserver, en verdwijnt alles met de bewaartermijn. 50 MB per upload is daarvoor klein genoeg.

### D4: Verwerking: één runner, dry-run via een teruggedraaide transactie

`ImportRunner.RunAsync(jobId)` is de Hangfire-taak, met `[AutomaticRetry(Attempts = 0)]`:

1. Zet de job op `Running` en verwijdert eventuele rapportregels van een eerdere, onderbroken poging. Upserts zijn
   idempotent, dus opnieuw beginnen na een herstart is veilig.
2. Leest de upload als lijst van (bestand, type, inhoud): één CSV met `UploadType`, of een zip (D5). Sorteert op de
   volgorde van `AcademicTypes.All`.
3. Bij een dry-run start een transactie op de `AcademicDbContext`.
4. Per bestand `CsvReader` (met de resterende regelruimte als grens), dan per regel de upsert van het type. Elke 500
   regels wordt de change tracker geleegd. Na een fout regel ook, zoals in de API-batch.
5. Een fout in het bestand als geheel (lege upload, ontbrekende verplichte kolom, onbekend bestand, te groot) stopt
   alles. Bij een echte import is er dan nog niets opgeslagen, want die controles gebeuren vóór de eerste upsert: eerst
   worden alle bestanden ingelezen, pas daarna verwerkt. De job wordt `Failed` met de melding.
6. Bij een dry-run wordt de transactie teruggedraaid. De rapportregels (in het geheugen verzameld) en de aantallen worden
   daarna in een nieuwe context opgeslagen, in delen van 1.000. De job wordt `Completed`.
7. Een onverwachte exceptie maakt de job `Failed` met "Unexpected error (trace …)", zonder details, en wordt gelogd.

De dry-run gebruikt dus exact dezelfde code als de echte import, inclusief verwijzingen naar records uit eerdere
bestanden in dezelfde zip. Nadeel: een dry-run van een groot bestand houdt tijdens de verwerking locks op de
Academic-tabellen vast (zie Risks).

Alternatief: valideren zonder opslaan (alleen `Validate`). Afgewezen: verwijzingen naar records uit dezelfde upload
zouden dan als fout verschijnen, en "created" of "updated" is niet te bepalen.

### D5: Zip en manifest

`System.IO.Compression.ZipArchive`. Mappen en `__MACOSX/` worden genegeerd. `manifest.csv` (of `MANIFEST.CSV`, in de
root) is optioneel en heeft de kolommen `file,type`. Zonder manifest is het type de bestandsnaam zonder `.csv`. Een
`.csv`-bestand zonder type, of met een onbekend type, maakt de job `Failed` met de bestandsnaam. Andere bestanden dan
`.csv` worden genegeerd. Tegen een zip-bom wordt per bestand hoogstens `MaxFileSize` uitgepakt, en in totaal hoogstens
vier keer `MaxFileSize`.

### D6: Aanmaken en herhalen van een job

`ImportJobs.CreateAsync(upload, name, type?, source, dryRun, startedBy)` controleert de grootte, slaat de job op met
`Queued` en roept `IBackgroundJobClient.Enqueue<ImportRunner>(r => r.RunAsync(id, CancellationToken.None))` aan.
"Import for real" (`RepeatAsync(jobId)`) kopieert de upload van een afgeronde dry-run naar een nieuwe job zonder
dry-run, met `RepeatOfId`. Zonder upload (na de bewaartermijn) volgt `Academic.Import.UploadExpired`.

### D7: Schermen en downloads

- `/academic/import` (interactief): bestand (CSV of zip), type (alleen voor een CSV), bron, "Dry run", Import. Daarna
  een redirect naar `/academic/imports/{id}`. Sjablonen staan per type als download op dezelfde pagina.
- `/academic/imports/{id}` (interactief): status, bron, bestanden en aantallen. Tijdens `Queued`/`Running` wordt elke
  2 seconden opnieuw gelezen. Daarna volgen de mislukte regels (hoogstens 200) en de knop "Download report (CSV)", en na
  een dry-run "Import for real".
- `/academic/imports`: de laatste 25 jobs.

Downloads voor ingelogde gebruikers (rapport-CSV, sjablonen) lopen via een kleine JS-module in de Academic-RCL, die een
`DotNetStreamReference` als bestand opslaat (het standaardpatroon van Blazor). De `/api/v1`-routes vragen een
bearer-token en zijn er dus niet voor bedoeld.

### D8: API

Onder `/api/v1/imports`, allemaal met `Academic.Import`:

- `POST ""`: multipart (`file`, `type?`), `?dryRun=true`. Antwoord 202 met `{ id, status }` en `Location`. Bron via
  `ApiSource`; `.RequireRateLimiting("api-batch")` en `.DisableAntiforgery()` (bearer-API, geen formulier).
- `GET "{id}"`: status, bron, `dryRun`, tijden, aantallen, melding.
- `GET "{id}/report"`: `{ items: [...] }`, gepagineerd zoals de lijsten (`pageNumber`, `pageSize` hoogstens 1.000), of
  met `?format=csv` de hele CSV als stream.
- `GET "templates/{type}.csv"`: de kopregel uit `AcademicType.Fields`.

`Development`: `doctrina.import` → `Academic.Import`.

### D9: Bewaartermijn

Een hosted service in Academic registreert bij het opstarten de terugkerende taak `academic-import-retention` (dagelijks
03:00 UTC). Die zet `Upload` op null voor jobs ouder dan `Academic:Import:RetentionDays`. Rapportregels bevatten sleutels
en meldingen, geen namen of e-mailadressen, en blijven bestaan.

### D10: Het oude pad verdwijnt

`AcademicImporter` en de synchrone verwerking in `Import.razor` verdwijnen. De tests die erop leunden, gaan over op
`ImportRunner` (zelfde scenario's, nu als job). `CsvReader` blijft, met een grens per upload in plaats van per bestand.

## Risks / Trade-offs

- [Een dry-run van een groot bestand houdt locks vast tijdens de verwerking] → gebruikers die tegelijk lezen, merken
  vertraging; dry-runs zijn een beheeractie. Snapshot-isolatie kan later.
- [Per regel verwerken is traag voor 100.000 regels (minuten)] → het draait op de achtergrond, met de voortgang zichtbaar.
  Bulk komt als metingen het vragen (proposal).
- [Hangfire is LGPL-3.0] → OSI-goedgekeurd en we linken alleen dynamisch (NuGet), dus geen commerciële licentie nodig;
  past bij de afspraak in `openspec/config.yaml`.
- [Het rapport in het geheugen bij een dry-run van 100.000 regels] → orde van tientallen MB; acceptabel binnen de grens.
- [Tests met een echte Hangfire-server zijn trager] → de runner wordt direct getest; maar een paar integratietests
  wachten op een echte job (poll tot 30 s).

## Migration Plan

Nieuwe migratie in schema `academic` (`ImportJobs`, `ImportJobRows`). Hangfire maakt bij de eerste start zijn eigen
schema `hangfire` aan. In productie: Hangfire-schema vooraf met het script van Hangfire, of `PrepareSchemaIfNecessary`
laten staan. Terugdraaien: de code terugzetten; de tabellen kunnen blijven staan. Archiveren na `add-academic-core`.
