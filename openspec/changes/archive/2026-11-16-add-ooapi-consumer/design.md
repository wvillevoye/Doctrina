# Design

## Context

Zie `proposal.md`. Waar deze change op voortbouwt:

- **Importpijplijn** (`add-bulk-import`): `ImportRunner` leest een upload (CSV of zip), vertaalt elke regel naar
  `IReadOnlyDictionary<string, string?>` met de OOAPI-veldnamen van `AcademicType.Fields` en roept
  `AcademicType.UpsertAsync(db, source, fields, ct)` aan; per regel komt een `ImportJobRow` (`created`/`updated`/
  `unchanged`/`failed`), per job een `ImportJob`, met dry-run in een transactie die wordt teruggedraaid. De logica voor
  uniekheid per `(Source, ExternalId)` en verwijzingen binnen dezelfde `Source` (`ReferenceNotFound`) zit in de upsert en blijft ongewijzigd.
  Alle typen en de runner zijn `internal` in Academic.
- **Ooapi-module** (`add-ooapi-interop`): bevat de mapping Doctrina → OOAPI (`Mapping.cs`), de opties `Ooapi:*` (taal,
  onderwijstaal, niveau) en `DependsOn = ["Academic"]`; geen eigen schema.
- **Achtergrondjobs**: Hangfire, ook voor `ImportRetentionSchedule` (terugkerend job via een `IHostedService`).
- Modules verwijzen alleen naar `.Contracts` of events (huisregel 2); de sync mag Academic dus niet rechtstreeks aanroepen.
- OOAPI v5 heeft geen top-level lijst van `offerings` of `associations` (zie `ooapi-provider`): offerings staan onder
  `/courses/{id}/offerings`, associations onder `/offerings/{id}/associations`.

## Goals / Non-Goals

**Goals:**
- Eén OOAPI-bron periodiek en op verzoek inlezen, idempotent, zonder validatielogica te dupliceren.
- Elke run is terug te zien (aantallen, fouten per record) op de bestaande importpagina's.

**Non-Goals:** zie `proposal.md` (verdwenen records, delta's, schrijven, beheerscherm, andere authenticatie).

## Decisions

### D1: De consument woont in de bestaande module `Ooapi`

`Ooapi` biedt al OOAPI aan en kent de veldmapping; de consument deelt de opties en de namen van de standaard. Een aparte
module `Ooapi.Client` zou die kennis dupliceren. De module blijft zonder eigen schema: de uitkomst van een run staat in
de importjobs van Academic.

*Alternatief:* de consument in Academic. Verworpen: Academic zou dan HTTP en OOAPI kennen, terwijl het voorstel
interoperabiliteit in `Interop.*` houdt.

### D2: Naad naar Academic: `IAcademicRecordImport` in `.Contracts`

```
Task<Result<Guid>> BeginAsync(RecordImportRequest request, CancellationToken ct)
Task<Result<ImportRunSummary>> RunAsync(Guid jobId, IAsyncEnumerable<ImportRecord> records, ICollection<string> warnings, CancellationToken ct)
record RecordImportRequest(string Source, string Name, string StartedBy, bool DryRun);
record ImportRecord(string Type, int Line, IReadOnlyDictionary<string, string?> Fields);
record ImportRunSummary(Guid JobId, bool Succeeded, string? Message, int Created, int Updated, int Unchanged, int Failed);
class RecordSourceException : Exception   // door de recordstroom gegooid als de bron niet verder kan
```

Twee stappen, zodat een aanroeper (het endpoint van D9) het job-id kan teruggeven voordat de run start: `BeginAsync`
maakt de job als `queued`, `RunAsync` verwerkt hem. `warnings` vult de recordstroom zelf terwijl hij wordt gelezen; ze
komen op de job. Een `RecordSourceException` uit de stroom laat de job `failed` eindigen met haar melding (wat al is
opgeslagen blijft staan); een ongeldige aanvraag (bronnaam leeg of langer dan 64 tekens) is een mislukt `Result` en maakt
geen job; een al afgeronde job opnieuw runnen is een conflict, een onderbroken job (nog `running`) begint opnieuw.

De implementatie in Academic maakt een `ImportJob` (`UploadName` = `Name`, geen `Upload`), roept per
record `UpsertAsync` van het type aan met dezelfde code als `ImportRunner.ProcessAsync` (die wordt gesplitst in "één
rij verwerken" en "bestand lezen") en sluit de job af. Dry-run gebruikt dezelfde transactie met rollback. `Type` is de
importnaam (`persons`, `courses`, `course-offerings`, …); een onbekend type geeft een `failed` regel.

*Alternatief:* de consument bouwt een CSV en roept de bestaande upload aan. Verworpen: dat zet gestructureerde data om
naar tekst en terug, verliest de herkomst per record en laat `MaxFileSize` gelden voor een HTTP-sync.

### D3: Leesvolgorde en nesting

Per run, in afhankelijkheidsvolgorde, elk met paginering tot `hasNextPage` onwaar is:
1. `organizations`, 2. `academic-sessions`, 3. `courses`, 4. per cursus `courses/{id}/offerings`, 5. `persons`,
6. per offering `offerings/{id}/associations`.

`courses/{id}/offerings` toont volgens de standaard (release 5.0) standaard alleen offerings met een toekomstige
startdatum (`since` = vandaag). De consument geeft daarom altijd `since=1900-01-01` mee, anders mist hij lopende en
afgelopen uitvoeringen. De paginering vertrouwt op `hasNextPage`; `totalPages` is in de standaard optioneel. Fouten
komen als `application/problem+json`; alleen de statuscode telt.

Binnen `organizations` en `academic-sessions` kan een record naar een `parent` verwijzen die later in de lijst staat (de
OOAPI van Doctrina zelf sorteert op naam). De consument leest die twee lijsten daarom helemaal in en voert ze ouder-eerst in;
een ouder die de bron niet noemt of een cyclus houdt niets tegen, want de import meldt wat hij niet kan oplossen.

Zo bestaan verwijzingen (`parent`, `organization`, `course`, `academicSession`, `person`, `offering`) voordat ze nodig
zijn; een `ReferenceNotFound` is dan een echte fout van het record, geen volgordeprobleem. Een fout bij één pagina of
één cursus (HTTP ≠ 2xx na de herhalingen van D8) beëindigt de run als `failed`; wat al is opgeslagen blijft staan en een
nieuwe run herhaalt het idempotent.

*Alternatief:* de lijsten parallel lezen. Verworpen: de volgorde is eenvoudiger te beredeneren en een SIS wil niet
overbelast worden.

### D4: Bronnen in configuratie, secrets apart

```
Ooapi:Sources:<naam>:
  BaseUrl     https://sis.example.org/ooapi/v5
  TokenUrl    https://idp.example.org/token
  ClientId    …
  Scope       optioneel
  Schedule    cron; leeg = alleen op verzoek
  PageSize    standaard 100 (de standaard staat 10, 20, 50, 100 en 250 toe)
  Enabled     standaard true
```

`ClientSecret` staat **alleen** in `appsettings.Secrets.json` (AGENTS.md). `<naam>` wordt de `Source` van alle records
en is begrensd op 64 tekens (`ExchangeableEntity.SourceMaxLength`); bij het starten wordt een langere naam geweigerd. Het token wordt per run opgehaald en tot kort voor het verlopen
hergebruikt; het wordt nooit gelogd. Buiten Development is alleen `https` toegestaan (`BaseUrl` en `TokenUrl`).

### D5: Mapping OOAPI → Doctrina-velden (omgekeerd van `ooapi-provider`)

- `primaryCode.code` wordt de sleutel van het type (de `ExternalId`). Ontbreekt `primaryCode`, dan krijgt het record
  geen sleutel en is het `failed` (`Academic.<Type>.Invalid`): het OOAPI-`…Id` is de interne sleutel van de bron en niet
  stabiel genoeg om `ExternalId` te zijn. Dit sluit aan op `DeSleutelVanHetBronsysteemStaatAlsCode` in de provider.
  **Uitzondering:** een association heeft in de standaard geen `primaryCode`; haar sleutel is het `associationId` van de bron.
- Verwijzingen (`parent`, `organization`, `course`, `academicSession`, `person`, `offering`) zijn OOAPI-id's van de
  bron; de consument vertaalt ze met een tabel in het geheugen (OOAPI-id → `primaryCode`) die tijdens de run wordt
  opgebouwd, zodat de upsert op `ExternalId` kan verwijzen.
- Teksten (`name`, `description`): de waarde in `Ooapi:Language`, anders de eerste.
- Enums: `organizationType` "institute" → `Institution`; `academicSessionType` "academic year" → `AcademicYear`,
  "testing period" → `TestingPeriod`; `role` "teaching assistant" → `TeachingAssistant`. De standaard kent ook de
  rol "guest", die Doctrina niet heeft: zo'n association is een `failed` regel. Onbekende waarden geven een
  `failed` record met de gebruikelijke validatiecode, geen stille standaardwaarde.
- Alleen `associationType` `courseOfferingAssociation` wordt ingelezen; andere typen worden overgeslagen met een
  waarschuwing in de job. `state` ≠ `associated` wordt overgeslagen (geen actieve inschrijving).
- `activeEnrollment`, `affiliations`, `level`, `teachingLanguage` en andere velden die Doctrina niet bewaart worden genegeerd.

### D6: Een sync is een importjob

Het resultaat gebruikt `ImportJob`/`ImportJobRow` en verschijnt op dezelfde jobpagina, met `Source` gelijk aan de
bronnaam, `StartedBy` "schedule" of de aanvrager, en het type (`persons`, `courses`, …) als bestandsnaam in de regels.
Er komt geen nieuwe tabel. Het rapport is als CSV te downloaden zoals nu.

### D7: Verdwenen records worden niet verwijderd

Een record dat de bron niet meer teruggeeft blijft in Doctrina staan. Verwijderen op afwezigheid in een lijst is
riskant (een lege of foute run zou alles wissen) en OOAPI-bronnen geven verwijderde records niet als `toBeDeleted`
terug. Een latere change kan "retire missing" toevoegen met een veiligheidsgrens (bv. nooit meer dan x% per run).

### D8: Herhalen, time-outs, grenzen

`HttpClient` via `IHttpClientFactory`, time-out 30 s per aanvraag, in totaal 3 pogingen met oplopende wachttijd bij
netwerkfouten, 429 (met `Retry-After`) en 5xx; 4xx (behalve 429) niet herhalen. Het aantal records per run is
begrensd door `Academic:Import:MaxRows` (hergebruik). Per bron draait hooguit één run tegelijk
(`DisableConcurrentExecution` op de bronnaam); een tweede aanvraag meldt "al bezig". Dat slot zit in het geheugen van het proces (een `SyncGate`; de jobserver draait in
hetzelfde proces als de host); bij meerdere instanties moet het een gedeeld slot worden.

### D9: Start op schema en op verzoek

Een `IHostedService` registreert per ingeschakelde bron met `Schedule` een recurring Hangfire-job (zoals
`ImportRetentionSchedule`). `POST /api/v1/ooapi-sources/{name}/syncs` (body `{ "dryRun": false }`) maakt de importjob
(`BeginAsync`, status `queued`), zet de run in de wachtrij en geeft 202 met dat job-id; een onbekende of uitgeschakelde bron geeft 404, een lopende run 409. Het endpoint
vereist `Ooapi.Sync`, niet `Ooapi.Read`: lezen en een externe bron laten bevragen zijn verschillende rechten.
`Ooapi.Sync` komt in `Authorization:RolePermissions` (Development: `doctrina.import`).

### D10: Privacy en logs

Een run logt alleen bronnaam, type, aantallen, statuscodes en job-id; nooit een naam, e-mailadres of token
(geen persoonsgegevens in logs). De regelmelding in het rapport noemt de sleutel (`primaryCode`) en de foutcode, zoals
bij de CSV-import.

## Risks / Trade-offs

- **Een volledige lees per run** duurt bij grote bronnen lang; `PageSize` 250 en de herhalingen beperken dat, delta's
  zijn er niet (geen standaard). Acceptabel voor een nachtelijke sync.
- **Afwijkende SIS'en** (eigen `primaryCode`-conventies, ontbrekende `associations`) worden hier niet opgevangen; ze
  verschijnen als `failed` records in het rapport, zodat de afwijking zichtbaar is.
- **Splitsen van `ImportRunner`** raakt gedeelde code; de bestaande CSV-importtests dekken de regressie.
- **Een fout halverwege** laat een deel opgeslagen: idempotent herhalen is het herstel (D3), net als bij de CSV-import.
- **De naam van een bron is de `Source` van records**: records zijn uniek per `(Source, ExternalId)` en verwijzingen
  worden binnen dezelfde `Source` opgelost. Hernoemen van een bron zou dus bij de volgende run een tweede, dubbele set
  records naast de oude maken. De naam is daarom bewust stabiel en gedocumenteerd; een andere bron met dezelfde
  `primaryCode` raakt de records van deze bron nooit.
