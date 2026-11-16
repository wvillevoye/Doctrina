# Proposal

## Why

Met LTI core en Deep Linking kan een docent een tool starten en content in de tool kiezen, maar een cijfer dat de tool
geeft (een quiz, een oefening) blijft in de tool. Studenten en docenten zien het niet in Doctrina. LTI Assignment and
Grade Services (AGS) laat een tool scores terugsturen. Dit is rij 8b van de fasering (§8 van
`docs/Doctrina-voorstel.md`; §6.5 noemt AGS "cijfers terug naar LMS"). AGS vraagt ook iets wat er nog niet is: een
beveiligde server-naar-server-aanroep van de tool naar Doctrina. Die voorziening (OAuth-token met `client_assertion`)
bouwen we hier één keer, zodat NRPS (8c) hem hergebruikt.

## What Changes

- Doctrina krijgt een **tokenendpoint** (`POST /lti/token`, OAuth 2.0 client credentials met een door de tool
  ondertekende `client_assertion`, LTI Advantage Security Framework) dat korte access tokens met scopes uitgeeft.
- Een tool met **cijferservices** aan kan via `/lti/ags/...` **line items** beheren, **scores** posten en **results**
  lezen (scopes `lineitem`, `lineitem.readonly`, `score`, `result.readonly`).
- De launch (`id_token`) bevat voor zo'n tool de AGS-claim met de adressen van de line items en, als er al een line item
  bij de tool link hoort, dat line item.
- Doctrina **bewaart** line items en scores (schema `lti`, migratie) en **toont** ze: een student ziet de eigen scores
  van een offering, een docent een overzicht per tool link.
- De toolregistratie krijgt een instelling "Grade services"; een `Public keys URL` is dan ook zonder deep linking URL
  geldig (de tool tekent nu ook een token).
- Documentatie (Engels) en de bestaande testtool uitbreiden (buiten de repo) voor de handmatige controle.

**Buiten de scope:**
- Een algemene cijferlijst, eindcijfers of berekeningen over tools heen; scores van een tool blijven bij die tool link.
- Koppelen van scores aan `Association`-resultaten van Academic.
- Terugschrijven van Doctrina naar de tool; synchronisatie van cijfers met het SIS.
- NRPS (8c) en andere services; alleen de tokeninfrastructuur wordt gedeeld.
- Per-scope instellingen per tool: "Grade services" geeft alle vier de AGS-scopes.
- Dynamische client-registratie van tools.

## Capabilities

### New Capabilities
- `lti-service-auth`: een tool haalt met `client_assertion` een access token met scopes op voor Doctrina's LTI-services
  (generiek, ook voor latere services).
- `lti-grade-services`: AGS: line items, scores en results van tools, bewaard en getoond in Doctrina.

### Modified Capabilities
- `lti-tool-registration`: instelling "Grade services" per tool; `Public keys URL` is ook geldig zonder deep linking URL
  als cijferservices aan staan.
- `lti-launch`: de `id_token` bevat de AGS-claim voor tools met cijferservices.

## Impact

- Code: module `Lti` (`Doctrina.Modules.Lti`: entiteiten `LineItem` en `Score`, services, endpoints `/lti/token` en
  `/lti/ags/*`, pagina's) en `Doctrina.Modules.Lti.Contracts`; kleine toevoegingen in `Content.Contracts` (bestaat er in
  een offering een tool link van deze tool, en welk item) en `Academic.Contracts` (is deze persoon lid van de
  uitvoering).
- Data: nieuwe tabellen in schema `lti` (migratie `AddGradeServices`); een kolom `GradeServices` op `LtiTool`.
- Geen nieuwe packages (`Microsoft.IdentityModel.*` is al aanwezig).
- Documentatie: `docs/ontwikkelen/starten.md` (Engels), `docs/Doctrina-voorstel.md` §6.5/§8.
