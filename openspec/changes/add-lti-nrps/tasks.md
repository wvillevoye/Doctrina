# Taken

`add-lti-ags` is gebouwd en gearchiveerd; de aanpassing van "Deep linking per tool" (D1) staat als MODIFIED in de delta.
De testhulpen `LaunchScenario.CreateAsync`, `LtiTools.ToolAsync`, `ToolInput` en `GradeScenario` uit 8b krijgen een optie
`membershipServices` waar nodig.

## 1. Toolinstelling (lti-tool-registration)

- [ ] 1.1 Voeg `LtiTool.MembershipServices` toe met migratie `AddMembershipServices`, de validatie in `ToolService.Check` (public keys URL verplicht bij ledenservices; de regel "public keys URL zonder deep linking URL" geldt alleen als ook `GradeServices` en `MembershipServices` uit staan), `LtiToolSummary.SupportsMembershipServices` en het veld "Membership services" in `ManageTools.razor`; controleer met tests `LedenservicesPerTool.ToolMetLedenservicesOpslaan`, `…LedenservicesZonderPublicKeysUrlWordtGeweigerd` en `…BestaandeToolHeeftGeenLedenservices`, de aangepaste `DeepLinkingPerTool.PublicKeysUrlZonderDeepLinkingUrlWordtGeweigerd` en een bUnit-test

## 2. Leescontract in Academic

- [ ] 2.1 Voeg `OfferingMemberDetail` en `IAcademicDirectory.MemberPageAsync` toe aan `Academic.Contracts` (D4) en implementeer ze met één query per pagina, gesorteerd op persoon-id, met rolfilter en een paginasleutel; controleer met LocalDB-tests in de Academic-tests (pagina's zonder dubbelen, persoon met twee rollen eenmaal, verwijderde inschrijving/persoon ontbreekt)

## 3. Endpoint

- [ ] 3.1 Maak de rolafbeelding van `IdToken` een gedeelde methode, verplaats de hulpen van `AgsEndpoints` (D2) naar een gedeelde plek en voeg `MembershipService` en `GET /lti/nrps/{context}/memberships` toe met de tokencontrole en toegangscontrole van 8b, de `membershipcontainer`-vorm, `role`, `limit`, `rlid`, de `Link`-header en de privacyregels (D2–D6), plus foutcodes `Lti.Nrps.*` met titels in de `.resx`-bestanden; controleer met tests `DeDeelnemersVanEenUitvoeringOphalen.DeelnemersVanEenUitvoering`, `…UserIdIsHetzelfdeAlsSubInDeLaunch`, `…PersoonMetMeerRollenKomtEenmaalVoor`, `…UitvoeringZonderToolLinkVanDezeTool`, `…VerwijderdeInschrijvingOfPersoon`, `RollenVolgenDeLisRollenVanDeLaunch.RollenVanEenStudentEnEenDocent` en `…FilterenOpRol`
- [ ] 3.2 Test de privacy- en paginatieregels; controleer met tests `NaamEnEmailAlleenAlsDeToolDaarvoorIsIngesteld.ZonderInstellingGeenNamen` en `…MetInstellingNaamEnEmail`, `DeLijstIsGepagineerdEnTeBeperkenTotEenResourceLink.Paginering`, `…TeGroteLimit` en `…OnbekendeResourceLink`
- [ ] 3.3 Voeg de NRPS-scope toe aan `LtiScopes` en laat `LtiScopes.AllowedFor` hem alleen geven aan tools met `MembershipServices`; controleer met tests `AlleenMetDeScopeEnMetLedenservices.ZonderToken`, `…TokenZonderNrpsScope` en `…ToolZonderLedenservices`

## 4. Launch

- [ ] 4.1 Laat `IdToken.Payload` de NRPS-claim opnemen voor tools met ledenservices (D8); controleer met tests `DeIdTokenVermeldtDeLedenservices.LaunchVanEenToolMetLedenservices` en `…ToolZonderLedenservicesKrijgtGeenNrpsClaim`

## 5. Logs, documentatie en afronding

- [ ] 5.1 Controleer dat een NRPS-aanroep met namen en e-mail niets persoonlijks logt met `GeenPersoonsgegevensInLogsBijNrps.NrpsLaatGeenPersoonsgegevensAchterInDeLogs` (`CapturingLoggerProvider`, `Trace`)
- [ ] 5.2 Beschrijf in het Engels in `docs/ontwikkelen/starten.md` hoe je ledenservices instelt, de scope, wat een tool krijgt en de privacyregels, en werk `docs/Doctrina-voorstel.md` §6.5/§8 bij; controleer met `dotnet build` zonder waarschuwingen
- [ ] 5.3 Draai `dotnet test` voor de hele solution en `openspec validate add-lti-nrps --strict`; controleer dat elk scenario een test heeft
- [ ] 5.4 Handmatig met de dev-stack en de testtool (uitgebreid met NRPS): een tool met ledenservices registreren, een token halen, de deelnemers van "Cell biology" ophalen (met en zonder "Send name"/"Send email") en de paginering controleren; stop de host en de testtool daarna
