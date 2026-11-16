# Taken

## 1. Voorbereiding

- [ ] 1.1 Haal de OpenAPI van OOAPI v5 uit `github.com/open-education-api/specification` en leg vast: paden, veldnamen, enum-waarden (rollen, `offeringType`), standaard- en maximale `pageSize`, filters per resource en het foutformaat; pas D5/D6 en de specs aan waar ze afwijken (`/opsx:update`) en voer `openspec validate add-ooapi-interop --strict` uit
- [ ] 1.2 Controleer of de architectuurtests een module zonder DbContext/schema toelaten (D1); pas anders de test aan, met een test die zo'n module toestaat

## 2. Host en modulesysteem

- [ ] 2.1 Voeg `IModule.MapOoapiEndpoints` (default, leeg) toe in `Doctrina.Core` en laat de host een groep `/ooapi/v5` met de `ApiCaller`-policy maken en per ingeschakelde module aanroepen (D3); controleer met tests `ModuleEndpointsStaanGeversioneerdOnderApiV1.StandaardApiOnderEigenPrefix` en `…ZonderTokenGeeft401` en `dotnet build`

## 3. Leescontract in Academic

- [ ] 3.1 Voeg `IAcademicExchangeReader` toe aan `Academic.Contracts` (D4): per type een gepagineerde lijst (alleen actief, stabiele volgorde, filter op `ExternalId`) en een opvraag op id; implementeer het in `Doctrina.Modules.Academic` met de bestaande queries; controleer met LocalDB-tests `AlleenActieveRecordsWordenGetoond.VerwijderdRecordIsOnzichtbaar` en `LijstenZijnGepagineerdVolgensDeStandaard.TweedePaginaVanPersonen`

## 4. Module Ooapi

- [ ] 4.1 Maak `Doctrina.Modules.Interop.OOAPI` (+ `Doctrina.Modules.Interop.OOAPI.Tests`) met `OoapiModule` (`DependsOn Academic`, permissie `Ooapi.Read`), registratie in `KnownModules.cs`, `Modules:Enabled` en `Authorization:RolePermissions` in Development (D1, D7); controleer met tests `HetLezenVanOoapiVraagtDePermissieOoapiRead.ZonderToken`, `…TokenZonderPermissie` en `…SessiecookieVolstaatNiet` en `DeModuleStaatUitAlsZeNietIsIngeschakeld.ModuleUitgeschakeld`
- [ ] 4.2 Implementeer de paginatiewrapper, `primaryCode` en de taalinstelling `Ooapi:Language` (D5, D6) en de endpoints voor `courses` en `organizations`; controleer met tests `DoctrinaBiedtOoapiResourcesAan.CursusOpvragenOpId`, `…OnbekendId`, `…SchrijvenWordtNietOndersteund`, `DeSleutelVanHetBronsysteemStaatAlsCode.FilterenOpCode` en `LijstenZijnGepagineerdVolgensDeStandaard.TeGrotePagina`
- [ ] 4.3 Voeg `academic-sessions`, `offerings` (met `offeringType` "course", cursus- en periodeverwijzing) en `persons` toe; controleer met tests `EenUitvoeringIsEenOoapiOffering.UitvoeringMetCursusEnPeriode` en een test van de persoonsvelden
- [ ] 4.4 Voeg `associations` toe met de rolmapping van D5; controleer met tests `EenInschrijvingIsEenOoapiAssociation.DocentInEenUitvoering` en `…RolZonderEquivalent`

## 5. Logs, documentatie en afronding

- [ ] 5.1 Controleer dat een reeks OOAPI-aanroepen niets persoonlijks logt met `GeenPersoonsgegevensInLogsBijOoapi.OoapiLaatGeenPersoonsgegevensAchterInDeLogs` (`CapturingLoggerProvider`, `Trace`) (D8)
- [ ] 5.2 Beschrijf in het Engels hoe een OOAPI-consument koppelt (client, scope `doctrina.persons.read`, `/ooapi/v5`, paginering, taalinstelling) in `docs/doctrina-integratie.md`, werk `docs/Doctrina-voorstel.md` §6.7 en §8 bij (alleen-lezen, schrijven blijft `/api/v1`) en vermeld de module in `src/Modules/README.md`; controleer met `dotnet build` zonder waarschuwingen
- [ ] 5.3 Draai `dotnet test` voor de hele solution en `openspec validate add-ooapi-interop --strict`; controleer dat elk scenario een test heeft
- [ ] 5.4 Handmatig met de dev-stack: een token voor `doctrina-sis-sync` halen, `/ooapi/v5/courses` en `/ooapi/v5/persons` opvragen en de paginering en de 401/403-antwoorden controleren; stop de host daarna
