# Taken

## 1. Tool-instellingen (lti-tool-registration)

- [x] 1.1 Voeg `LtiTool.DeepLinkingUrl` en `PublicKeysUrl` toe met een migratie in schema `lti` (`dotnet ef migrations add AddDeepLinkingUrls`), de validatie en foutcodes `Lti.Tool.DeepLinkingUrlRequired`/`PublicKeysUrlRequired` in `ToolService` en `LtiToolSummary.SupportsDeepLinking` in `ILtiToolCatalog` (D1); controleer met LocalDB-tests `DeepLinkingPerTool.ToolMetDeepLinkingOpslaan`, `…DeepLinkingUrlZonderPublicKeysUrlWordtGeweigerd`, `…PublicKeysUrlZonderDeepLinkingUrlWordtGeweigerd`, `…DeepLinkingAdresZonderHttpsWordtGeweigerd` en `…BestaandeToolBlijftWerkenZonderDeepLinking`
- [x] 1.2 Voeg de twee velden toe aan `ManageTools.razor` (labels "Deep linking URL" en "Public keys URL"); controleer met een bUnit-test en `dotnet build`

## 2. Contract in Content

- [x] 2.1 Voeg `IContentDirectory.FindSectionAsync` en `AddToolLinksAsync` toe en `ToolLinkInfo.LaunchUrl` (D2, D4, D5); implementeer ze in `ContentDirectory`/`ContentService` (concept, onderaan de sectie, `Url` = gekozen adres, rechtencontrole en `Content.Section.NotFound`); controleer met LocalDB-tests `DeDocentBevestigtDeSelectie.DocentVoegtDeSelectieToe`, `…StudentZietDeNieuweToolLinkNiet` en `…VerwijderdeSectieVoegtNietsToe`

## 3. Het verzoek starten

- [x] 3.1 Breid de hint van `LaunchService` uit met soort en doel, voeg `StartDeepLinkingAsync` en `GET /lti/deep-linking/{section}/{tool}` toe en laat `IdToken` een `LtiDeepLinkingRequest` bouwen met `deep_linking_settings` en `data` (D2); controleer met tests `EenDeepLinkingSelectieStarten.DocentStartEenSelectie`, `…HetVerzoekIsEenLtiDeepLinkingRequest`, `…StudentMagGeenSelectieStarten`, `…ToolZonderDeepLinkingGeeft404`, `…OnbekendeSectieGeeft404` en `…NietIngelogdeBezoekerMoetEerstInloggen`

## 4. Het antwoord controleren en bewaren

- [x] 4.1 Voeg `DeepLinkSelection` en `DeepLinkNonce` toe aan `LtiDbContext` met een migratie (`AddDeepLinkSelections`), `JwksClient` (D6: timeout, geen redirects, 64 kB, cache) en `DeepLinkingService.ReturnAsync` met de controles in de volgorde van D3; controleer met tests `HetAntwoordVanDeToolControleren.GeldigAntwoordWordtBewaardAlsSelectie`, `…OnjuisteHandtekeningWordtGeweigerd`, `…VerkeerdeIssuerOfAudienceWordtGeweigerd`, `…VerlopenAntwoordWordtGeweigerd`, `…HergebruiktAntwoordWordtGeweigerd`, `…VervalsteOfVerlopenDataWordtGeweigerd`, `…DataVanEenAndereToolWordtGeweigerd`, `…ItemMetEenAnderAdresDanDeToolWordtGeweigerd`, `…AndereItemsoortenWordenGeweigerd` en `…AntwoordZonderItemsGeeftEenLegeSelectie` (de testtool tekent met een eigen sleutelpaar en serveert zijn JWKS)
- [x] 4.2 Voeg `POST /lti/deep-linking/return` toe (anoniem, antiforgery uit voor dit endpoint, 303 naar de bevestigingspagina) en de foutcodes `Lti.DeepLinking.*` met titels in `LtiErrors.resx`/`.en.resx` en `LtiErrors.All` (D7); controleer met een integratietest op het web die een antwoord post en de redirect controleert, en `dotnet build`

## 5. Bevestigen

- [x] 5.1 Voeg de pagina `/lti/deep-linking/selection/{id}` toe (D4): titels als tekst, knop "Add to section", controle op gebruiker en verlooptijd, opruimen van verlopen selecties; controleer met tests `DeDocentBevestigtDeSelectie.DeBevestigingspaginaToontDeTitels`, `…AndereGebruikerMagDeSelectieNietBevestigen` en `…VerlopenSelectieGeeft404` en een bUnit-test van de pagina

## 6. Launch met eigen adres en de beheerpagina

- [x] 6.1 Laat `LaunchService` `LaunchUrl ?? tool.LaunchUrl` gebruiken in de redirect en in `target_link_uri` van de `id_token` (D5); controleer met tests `EenToolLinkMetEigenAdresStartDatAdres.LaunchGebruiktHetGekozenAdres`, `…TokenBevatHetGekozenAdres` en `…GekopieerdeToolLinkBehoudtHetAdres` (de laatste in de Content-tests met `CopyFromAsync`)
- [x] 6.2 Voeg de keuzelijst en de knop "Add from tool" toe aan `ManageContent.razor` (D8), alleen met de module Lti en minstens één tool met deep linking; controleer met een bUnit-test en een integratietest die van selectie tot concept-tool link doorloopt

## 7. Logs, documentatie en afronding

- [x] 7.1 Controleer dat een volledige deep linking-selectie niets persoonlijks logt met `GeenPersoonsgegevensInLogsBijDeepLinking.DeepLinkingLaatGeenPersoonsgegevensAchterInDeLogs` (`CapturingLoggerProvider`, `Trace`)
- [x] 7.2 Werk `docs/doctrina-integratie.md` of `docs/ontwikkelen/starten.md` (Engels: een tool voor deep linking instellen, het verloop, de controles) bij en `docs/Doctrina-voorstel.md` §7 indien nodig; controleer met `dotnet build` zonder waarschuwingen
- [x] 7.3 Draai `dotnet test` voor de hele solution en `openspec validate add-lti-deep-linking --strict`; controleer dat elk scenario uit de spec-delta een test heeft (lijst in de PR-beschrijving)
- [x] 7.4 Handmatig met de dev-stack en de stdlib-testtool: een tool met deep linking registreren, als `docent` "Add from tool" kiezen, bevestigen, controleren dat de tool link als concept staat en bij publiceren naar het gekozen adres launcht; stop de host en de testtool daarna
