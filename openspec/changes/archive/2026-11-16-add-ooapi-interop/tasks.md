# Taken

## 1. Voorbereiding

- [x] 1.1 Lees de OpenAPI van OOAPI v5 (release 5.0 van `github.com/open-education-api/specification`) en leg vast: paden, veldnamen, enum-waarden, `pageSize`, filters en foutformaat; pas D5/D6 en de specs aan waar ze afwijken en voer `openspec validate add-ooapi-interop --strict` uit
- [x] 1.2 Controleer of de architectuurtests een module zonder DbContext/schema toelaten (D1); pas anders de test aan, met een test die zo'n module toestaat

## 2. Host en modulesysteem

- [x] 2.1 Voeg `IModule.MapOoapiEndpoints` (default, leeg) toe in `Doctrina.Core` en laat de host een groep `/ooapi/v5` met de `ApiCaller`-policy maken en per ingeschakelde module aanroepen (D3); controleer met tests `ModuleEndpointsStaanGeversioneerdOnderApiV1.StandaardApiOnderEigenPrefix` en `…ZonderTokenGeeft401` en `dotnet build`

## 3. Leescontract in Academic

- [x] 3.1 Voeg `IAcademicExchangeReader` toe aan `Academic.Contracts` (D4): de lijsten en opvragen op id van D6 (alleen actief, stabiele volgorde, filter op `ExternalId` en op rol, personen met hun rollen); implementeer het in `Doctrina.Modules.Academic` met de bestaande queries; controleer met LocalDB-tests `AlleenActieveRecordsWordenGetoond.VerwijderdRecordIsOnzichtbaar` en `LijstenZijnGepagineerdVolgensDeStandaard.TweedePaginaVanPersonen`

## 4. Module Ooapi

- [x] 4.1 Maak `Doctrina.Modules.Interop.OOAPI` (de tests staan in `Doctrina.Host.IntegrationTests/Ooapi`, want de module heeft geen eigen logica buiten HTTP) met `OoapiModule` (`DependsOn Academic`, permissie `Ooapi.Read`), registratie in `KnownModules.cs`, `Modules:Enabled` en `Authorization:RolePermissions` in Development (D1, D7); controleer met tests `HetLezenVanOoapiVraagtDePermissieOoapiRead.ZonderToken`, `…TokenZonderPermissie` en `…SessiecookieVolstaatNiet` en `DeModuleStaatUitAlsZeNietIsIngeschakeld.ModuleUitgeschakeld`
- [x] 4.2 Implementeer de paginatiewrapper, `primaryCode`, de instellingen `Ooapi:*` (D5, D9), `GET /` en de endpoints voor `courses` en `organizations`; controleer met tests `DoctrinaBiedtOoapiResourcesAan.CursusOpvragenOpId`, `…OnbekendId`, `…SchrijvenWordtNietOndersteund`, `…OrganisatieMetType`, `DeSleutelVanHetBronsysteemStaatAlsCode.FilterenOpCode`, `LijstenZijnGepagineerdVolgensDeStandaard.StandaardPaginagrootte` en `…TeGroteOfOngeldigePagina`, `GetGeeftDeServicegegevens.Servicegegevens` en `VerplichteOoapiVeldenZonderBronWordenAfgeleidOfIngesteld.CursusMetIngesteldeTaalEnNiveau`
- [x] 4.3 Voeg `academic-sessions`, `offerings` (op id en de lijsten van cursus, periode en organisatie, met `offeringType` "course", cursus- en periodeverwijzing en de datums) en `persons` (met afgeleide `affiliations` en `activeEnrollment`) toe; controleer met tests `EenUitvoeringIsEenOoapiOffering.UitvoeringMetCursusEnPeriode`, `…UitvoeringenVanEenCursus` en `…UitvoeringZonderEigenDatums` en `VerplichteOoapiVeldenZonderBronWordenAfgeleidOfIngesteld.PersoonMetEenInschrijvingAlsStudent` en `…PersoonZonderInschrijving`
- [x] 4.4 Voeg `associations` toe (op id en de lijsten van uitvoering, met `role`-filter, en persoon) met de rolmapping van D5; controleer met tests `EenInschrijvingIsEenOoapiAssociation.DocentInEenUitvoering`, `…TeachingAssistantEnCoordinator` en `…FilterenOpRol`

## 5. Logs, documentatie en afronding

- [x] 5.1 Controleer dat een reeks OOAPI-aanroepen niets persoonlijks logt met `GeenPersoonsgegevensInLogsBijOoapi.LogsBijHetOpvragenVanPersonen` (`CapturingLoggerProvider`, `Trace`) (D8)
- [x] 5.2 Beschrijf in het Engels hoe een OOAPI-consument koppelt (client, scope `doctrina.persons.read`, `/ooapi/v5`, paginering, taalinstelling) in `docs/doctrina-integratie.md`, werk `docs/Doctrina-voorstel.md` §6.7 en §8 bij (alleen-lezen, schrijven blijft `/api/v1`) en vermeld de module in `src/Modules/README.md`; controleer met `dotnet build` zonder waarschuwingen
- [x] 5.3 Draai `dotnet test` voor de hele solution en `openspec validate add-ooapi-interop --strict`; controleer dat elk scenario een test heeft
- [x] 5.4 Handmatig met de dev-stack: een token voor `doctrina-sis-sync` halen, `/ooapi/v5/courses` en `/ooapi/v5/persons` opvragen en de paginering en de 401/403-antwoorden controleren; stop de host daarna
