# Taken

## 1. Voorbereiding

- [x] 1.1 Lees de OpenAPI van OOAPI v5 (release 5.0) opnieuw voor de leesrichting en leg vast: de verplichte velden per resource, de paden voor `courses/{id}/offerings` en `offerings/{id}/associations`, de filters, de foutvorm en de pagineringsvelden; pas D3/D5 en de spec aan waar ze afwijken en voer `openspec validate add-ooapi-consumer --strict` uit
- [x] 1.2 Controleer welke lengte `Source` toestaat en hoe `Api:Sources` bronnen toekent, zodat een bronnaam als `Source` past; leg de grens vast in D4

## 2. Naad in Academic

- [x] 2.1 Voeg `IAcademicRecordImport` en de records van D2 toe aan `Academic.Contracts`; splits in `ImportRunner` de verwerking van één rij van het lezen van het bestand en implementeer het contract daarmee (jobrapportage zonder upload, dry-run met transactie); controleer met LocalDB-tests `EenSyncIsEenImportjobMetRapportEnDryRun.RecordsViaHetContractGevenEenRapport`, `…DryRunSlaatNietsOp`, `…OnbekendTypeIsEenMislukteRegel` en `…EenJobIsQueuedTotDeRunStart`, en met de bestaande CSV-importtests als regressie

## 3. Consument in Ooapi

- [x] 3.1 Voeg de opties `Ooapi:Sources:<naam>` (D4) toe met validatie bij het starten (https buiten Development, geldige cron, unieke naam) en een `HttpClient` per bron met tokenverkrijging via client credentials (D8: time-out, herhalen, `Retry-After`); controleer met tests `DeVerbindingMetDeBronIsBeveiligd.TokenWordtHergebruikt`, `…TweedeAanvraagBijEenVerlopenToken` en `…AlleenHttpsBuitenDevelopment`
- [x] 3.2 Implementeer de leesvolgorde en paginering van D3 en de mapping van D5 (`primaryCode` → sleutel, verwijzingstabel, taalkeuze, enums, `associationType`, `state`) met een stub-OOAPI-bron (een `HttpMessageHandler` in de test); controleer met tests `EenOoapiBronWordtIngelezenInAfhankelijkheidsvolgorde.AlleTypenInVolgorde` en `…MeerderePaginas`, `…BovenliggendeOrganisatieKomtLaterInDeLijst`, `…AfgelopenUitvoeringenWordenOokGelezen`, `BovenliggendeRecordsWordenEerstIngevoerd.BovenliggendeOrganisatieKomtLaterInDeLijst`, `GelezenRecordsGaanViaDeGedeeldeValidatieEnUpsert.NieuweRecordsZijnAangemaakt`, en `DeSleutelVanEenRecordKomtUitDeBron.RecordZonderPrimaryCode`, `…AssociationMetHetIdVanDeBron`, en `DeVeldenWordenVertaaldVolgensDeOoapiStandaard.OnbekendeEnumwaarde`, `…AssociationVanEenAnderType` en `…NietActieveAssociation`
- [x] 3.3 Voer de gelezen records in via `IAcademicRecordImport` met `Source` gelijk aan de bronnaam; controleer met tests `EenSyncIsIdempotentEnRaaktAndermansRecordsNiet.TweedeRunGeeftUnchanged`, `…RecordVanEenAndereBronBlijftOngemoeid` en `…RecordNietMeerInDeBron`
- [x] 3.4 Beheer fouten: HTTP-fouten en time-outs na de herhalingen laten de run `failed` eindigen met een melding zonder geheimen of persoonsgegevens (D3, D8, D10); controleer met tests `EenFoutInDeBronStoptDeRunMetEenDuidelijkeMelding.ServerfoutNaDriePogingen`, `…Autorisatiefout` en `…TeVeelRecords`

## 4. Schema en endpoint

- [x] 4.1 Registreer per bron met `Schedule` een recurring Hangfire-job (D9) met `DisableConcurrentExecution` per bron; controleer met tests `EenSyncDraaitOpSchemaEnOpVerzoek.BronMetSchemaIsGeregistreerd`, `…BronZonderSchema` en `…UitgeschakeldeBron`
- [x] 4.2 Voeg `POST /api/v1/ooapi-sources/{name}/syncs` toe achter `Ooapi.Sync` (202 met job-id, 404, 409) en voeg `Ooapi.Sync` toe aan de permissies en aan `Authorization:RolePermissions` in Development; controleer met tests `EenSyncDraaitOpSchemaEnOpVerzoek.SyncStartenOpVerzoek`, `…ZonderToken`, `…TokenMetAlleenOoapiRead`, `…OnbekendeBron` en `…LopendeRun`

## 5. Logs, documentatie en afronding

- [x] 5.1 Controleer dat een sync niets persoonlijks of geheims logt met `EenSyncLogtNietsPersoonlijksOfGeheims.LogsBijEenRunMetPersonen` (`CapturingLoggerProvider`, `Trace`, ook geen token) (D10)
- [x] 5.2 Beschrijf in het Engels hoe een bron wordt ingesteld (opties, `appsettings.Secrets.json`, schema, rapport, dry-run, het endpoint) in `docs/doctrina-integratie.md`; voeg rij 6a toe aan §8 en werk §6.7 van `docs/Doctrina-voorstel.md` bij; vermeld de consument in `src/Modules/README.md`; controleer met `dotnet build` zonder waarschuwingen
- [x] 5.3 Draai `dotnet test` voor de hele solution en `openspec validate add-ooapi-consumer --strict`; controleer dat elk scenario een test heeft
- [x] 5.4 Handmatig met de dev-stack: laat Doctrina zichzelf als bron gebruiken (`/ooapi/v5` met `doctrina-sis-sync`), start een dry-run en een echte sync via het endpoint, bekijk het rapport op de importpagina en controleer de 401/403/409-antwoorden; stop de host daarna
