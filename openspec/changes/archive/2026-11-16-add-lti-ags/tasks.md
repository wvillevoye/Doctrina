# Taken

## 1. Toolinstelling (lti-tool-registration)

- [x] 1.1 Voeg `LtiTool.GradeServices` toe met migratie, de aangepaste validatie (public keys URL verplicht bij cijferservices en toegestaan zonder deep linking URL), `LtiToolSummary.SupportsGradeServices` en het veld "Grade services" met de token-URL in `ManageTools.razor` (D5); controleer met tests `CijferservicesPerTool.ToolMetCijferservicesOpslaan`, `…CijferservicesZonderPublicKeysUrlWordtGeweigerd`, `…BestaandeToolHeeftGeenCijferservices` en `…PlatformgegevensTonenDeTokenUrl`, `DeepLinkingPerTool.PublicKeysUrlZonderDeepLinkingUrlWordtGeweigerd` (zonder cijferservices) en een bUnit-test

## 2. Tokenendpoint (lti-service-auth)

- [x] 2.1 Maak van `DeepLinkNonce` een `JwtNonce` met `Purpose` (migratie `AddGradeServices`, bestaande rijen krijgen `deeplink`) en pas `DeepLinkingService` aan; controleer met de bestaande deep linking-tests en `dotnet build`
- [x] 2.2 Voeg `ServiceAuthService` en `POST /lti/token` toe met de controles in de volgorde van D1, scopebeperking en een access token als JWT (D2), plus de foutcodes `Lti.ServiceAuth.*`; controleer met tests `EenToolHaaltEenAccessTokenOpMetEenClientAssertion.GeldigeAssertionGeeftEenToken`, `…OnjuisteHandtekeningWordtGeweigerd`, `…VerkeerdeAudienceWordtGeweigerd`, `…VerlopenAssertionWordtGeweigerd`, `…HergebruikteAssertionWordtGeweigerd`, `…OnbekendeOfUitgeschakeldeTool`, `ScopesWordenBeperktTotWatDeToolMag.ScopeDieNietIsToegestaan` en `…DeelsToegestaneScopes` (de testtool tekent met een eigen sleutelpaar)
- [x] 2.3 Voeg een token-validatiehulp voor service-endpoints toe (401/403 volgens de scope); controleer met tests `EenAccessTokenGeldtKortEnVoorEenTool.ZonderToken`, `…VerlopenToken` en `…OnvoldoendeScope`

## 3. Contracten

- [x] 3.1 Voeg `IContentDirectory.ToolLinksOfToolAsync` toe in `Content.Contracts` (D4) en implementeer hem in Content; controleer met LocalDB-tests in de Content-tests (tool links van een tool in een offering, leeg voor een andere tool)

## 4. Line items, scores en results (lti-grade-services)

- [x] 4.1 Voeg `LineItem` en `Score` toe aan `LtiDbContext` (D5) en `GradeService` met de validatie van D6 (`Lti.Ags.*`, titels in de `.resx`-bestanden en `LtiErrors.All`); controleer met `dotnet build`
- [x] 4.2 Voeg de line item-endpoints toe onder `/lti/ags/{context}/lineitems` (aanmaken, lijst met filters en `Link`-paginering, ophalen, wijzigen, verwijderen incl. bijbehorende scores); controleer met tests `LineItemsVanEenToolBeheren.LineItemAanmaken`, `…LineItemMetEenResourceLinkVanEenAndereTool`, `…LijstFilterenOpResourceLink`, `…UitvoeringZonderToolLinkVanDezeTool`, `…LineItemVanEenAndereTool`, `…AlleenLezen` en `ScoresVerdwijnenMetHunLineItem.LineItemVerwijderen`
- [x] 4.3 Voeg `POST {lineitem}/scores` toe (mediatype, `timestamp`-regel, lidmaatschapscontrole); controleer met tests `ScoresVanStudentenPosten.ScorePosten`, `…OudereScoreWordtGeweigerd`, `…NieuwereScoreVervangtDeOude`, `…GebruikerIsGeenLidVanDeUitvoering` en `…OngeldigeScore`
- [x] 4.4 Voeg `GET {lineitem}/results` toe met paginering en filter `user_id`; controleer met tests `ResultsLezen.ResultsVanEenLineItem` en `…ResultsVoorEenGebruiker`

## 5. Launch en tonen

- [x] 5.1 Laat `IdToken.Payload` de AGS-claim opnemen voor tools met cijferservices (D8); controleer met tests `DeIdTokenVermeldtDeCijferservices.LaunchVanEenToolMetCijferservices`, `…LaunchMetEenBestaandLineItem` en `…ToolZonderCijferservicesKrijgtGeenAgsClaim`
- [x] 5.2 Voeg de sectie "Scores" van de student toe aan de offering-pagina en de docentenpagina `/lti/ags/results/{itemId}` met de link "Scores" in `ManageContent.razor` (D7); controleer met tests `EenStudentZietDeEigenScores.StudentZietDeEigenScore`, `…StudentZietGeenScoresVanAnderen`, `EenDocentZietDeScoresPerToolLink.DocentZietHetOverzicht`, `…StudentMagHetOverzichtNietOpenen` en bUnit-tests van beide
- [x] 5.3 Controleer dat een volledige AGS-stroom niets persoonlijks of geheims logt met `GeenGeheimenOfPersoonsgegevensInLogsBijServiceAuth.…` en `GeenPersoonsgegevensInLogsBijAgs.AgsLaatGeenPersoonsgegevensAchterInDeLogs` (`CapturingLoggerProvider`, `Trace`)

## 6. Documentatie en afronding

- [x] 6.1 Beschrijf in het Engels in `docs/ontwikkelen/starten.md` hoe je cijferservices instelt, het tokenendpoint, de scopes en wat studenten en docenten zien, en werk `docs/Doctrina-voorstel.md` §6.5/§8 bij; controleer met `dotnet build` zonder waarschuwingen
- [x] 6.2 Draai `dotnet test` voor de hele solution en `openspec validate add-lti-ags --strict`; controleer dat elk scenario een test heeft
- [x] 6.3 Handmatig met de dev-stack en de testtool (uitgebreid met AGS): een tool met cijferservices registreren, als `docent` een tool link starten, als de tool een line item maken en een score posten, als `student` de score zien en als `docent` het overzicht; stop de host en de testtool daarna
