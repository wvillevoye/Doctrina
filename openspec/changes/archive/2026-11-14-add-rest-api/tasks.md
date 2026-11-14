# Taken

## 1. Representatie en bron

- [x] 1.1 Voeg aan `AcademicType` `Query(db)` en `ToFields(entity)` toe voor alle zes typen (design D2); controleer met een LocalDB-test per type dat een record na `ToFields` → upsert `Unchanged` is, en dat verwijzingen als ExternalId en datums als `yyyy-MM-dd` verschijnen
- [x] 1.2 Maak de omzetting JSON-object → velden (D3) en `ApiSource.For(user)` (D5); controleer met unit-tests: strings, getallen, booleans, `null`, array als waarde (validatiefout), onbekende velden genegeerd, en bron uit `Api:Sources:{azp}` of anders `SIS`

## 2. Endpoints van Academic

- [x] 2.1 Voeg permissie `Academic.Write` toe, de rolkoppeling van de Keycloak-scopes in `appsettings.Development.json` (D8) en een testhelper in `Doctrina.Host.IntegrationTests` voor een API-client met rollen en `azp` op de Academic-host; controleer met test `LezenEnSchrijvenVragenApartePermissies.AlleenLeesrechten`
- [x] 2.2 Maak `GET /{type}` en `GET /{type}/{id}` (D2, D6) met foutcodes `Academic.<Type>.NotFound` en `Academic.Api.InvalidQuery` en hun titels; controleer met integratietests `RecordsGebruikenDeVeldnamenVanDeImport.PersoonOpvragenOpId`, `ElkAcademicTypeHeeftRestEndpointsOnderApiV1.OnbekendId`, `LijstenZijnGepagineerdEnOndersteunenDeltas.DeltaSindsEenMoment` (met `FakeTimeProvider`) en `…Paginering`, plus filters op `source` en `status`
- [x] 2.3 Maak `POST`, `PUT by-external-id` en `PATCH` (D4, D5) met `Academic.<Type>.DuplicateExternalId`, `Academic.Api.KeyMismatch` en `Academic.Record.OtherSource`; controleer met integratietests `AanmakenGeeftEenConflictBijEenBestaandeSleutel.AanmakenVanEenNieuwePersoon` en `…AanmakenVanEenBestaandeSleutel`, `UpsertOpDeSleutelVanHetBronsysteem.UpsertVanEenNieuwePersoon`, `…UpsertVanEenBestaandePersoon` en `…OngeldigeInvoer`, `GedeeltelijkBijwerkenBehoudtDeOverigeVelden.GedeeltelijkBijwerken`, `SystemenSchrijvenMetHunEigenBron.BronUitDeConfiguratie` en `…RecordVanEenAndereBron`, plus een sleutel in de body die afwijkt (400) en een verwijzing naar een onbekend record (400 met `ReferenceNotFound`)
- [x] 2.4 Maak `DELETE` (D6); controleer met integratietest `VerwijderenIsZacht.InschrijvingVerwijderen` en dat de verwijdering in een delta verschijnt
- [x] 2.5 Maak `POST /{type}/batch` en de rate limiting (D7; policy in `Doctrina.Infrastructure`, `Api:BatchesPerMinute`); controleer met integratietests `EenBatchVerwerktTot500RecordsMetEenResultaatPerRecord.BatchMetEenFoutRecord`, `…TeGroteBatch` en `BatchesZijnPerClientBegrensd.TeVeelBatches`, en dat een andere client dan nog wel een batch mag sturen
- [x] 2.6 Zet in de OpenAPI-beschrijving van elk schrijvend endpoint de velden van het type, met de verplichte gemarkeerd (Risks); controleer met een test dat `/openapi/v1.json` (Development) `PUT /api/v1/persons/by-external-id/{externalId}` bevat met "surname (required)" in de beschrijving

## 3. Groups-API

- [x] 3.1 Voeg permissie `Groups.Read` toe en maak `GET /groups?offeringId=` en `GET /groups/{id}` (D9) met `Groups.Api.OfferingRequired` en titels; controleer met integratietests `GroepenZijnViaDeApiTeLezen.GroepenVanEenUitvoering`, `…VerwijderdeGroep` en `…ZonderGroupsRead`, plus 400 zonder `offeringId`

## 4. Documentatie en afronding

- [x] 4.1 Beschrijf in `docs/ontwikkelen/starten.md` (§ "De API proberen", nieuwe tekst in het Engels) een token voor `doctrina-sis-sync` en een PUT, GET en batch met PowerShell, en in `docs/doctrina-integratie.md` de scopes → permissies en de bron per client; controleer dat de voorbeelden werken tegen de lokale host
- [x] 4.2 Draai `dotnet test` voor de hele solution en `openspec validate add-rest-api --strict`; controleer dat alles slaagt en dat elk scenario uit de spec-delta een test heeft
- [x] 4.3 Handmatig met Keycloak en de lokale host: als `doctrina-sis-sync` een persoon upserten, een delta ophalen, een batch sturen en de groepen van "Cell biology (2026-P1)" lezen; daarna in de beheerschermen controleren dat de persoon er staat met bron `SIS`

## Vervolg in de workflow

- Beoordeel de change vóór `/opsx:apply`.
- Bouwen op `feature/add-rest-api` (voortbouwend op `feature/add-groups`).
- Daarna `add-bulk-import` (rest van §6.6).
