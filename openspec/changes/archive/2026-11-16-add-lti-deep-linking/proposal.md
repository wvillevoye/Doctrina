# Voorstel

## Why

Nu maakt een docent een tool link met de hand: een tool kiezen en een titel typen. De docent kan dus niet zeggen
"deze quiz uit de tool". Deze change is rij 8a van de roadmap (§8 van `docs/Doctrina-voorstel.md`,
`add-lti-deep-linking`; §7): de docent kiest de content **in de tool zelf** (LTI Deep Linking 2.0) en Doctrina voegt
die als tool link aan de sectie toe. Rij 8 (`add-lti-platform`) is af en alles wat nodig is (launch, sleutels, tools,
tool links) bestaat.

Keuze van de gebruiker (2026-11-14): `add-lti-deep-linking` is de volgende change, boven NRPS, AGS en OOAPI.

## What Changes

- **Per tool deep linking instellen**: een tool krijgt twee optionele adressen: de `Deep linking URL` (waar Doctrina het
  verzoek naartoe stuurt) en de `Public keys URL` (JWKS van de tool, om zijn antwoord te controleren). Het ene kan niet
  zonder het andere.
- **Docent start een selectie**: op de beheerpagina van de content staat bij een sectie "Add from tool" voor elke tool
  met deep linking. Doctrina doet een login initiation en stuurt een `LtiDeepLinkingRequest` (`id_token`) naar de tool.
- **De tool antwoordt** met een ondertekende JWT (`LtiDeepLinkingResponse`) op `POST /lti/deep-linking/return`.
  Doctrina controleert handtekening, `iss`, `aud`, `exp`, `nonce` en de `data` die het zelf meegaf, en bewaart de
  gekozen items als **wachtende selectie** (15 minuten).
- **Docent bevestigt**: Doctrina stuurt de docent naar een bevestigingspagina met de gekozen titels. Eén klik voegt ze
  als **concept** toe aan de sectie. Zo ziet de docent wat er komt en is geen sessie nodig op de cross-site POST.
- **Tool link met eigen launch-URL**: een tool link uit deep linking bewaart het adres dat de tool koos; de launch
  gebruikt dat als `target_link_uri`. Alleen `ltiResourceLink`-items, met een adres op dezelfde herkomst als de launch-URL.
- Wijzigingen in Content: `IContentDirectory.AddToolLinksAsync` en `ToolLinkInfo.LaunchUrl`. Een kopie
  (`add-course-templates`) neemt het adres al mee.
- Documentatie: `docs/doctrina-integratie.md` of `docs/ontwikkelen/starten.md` (een tool voor deep linking instellen).

## Buiten de scope

- Andere contentsoorten uit de spec (`link`, `html`, `file`, `image`) en `custom`-parameters, `lineItem`, `available`/`submission`-tijden.
- **AGS** (cijfers, rij 8b) en **NRPS** (deelnemers, rij 8c).
- Een tool die zelf Deep Linking start of items later wijzigt (content item update); de docent verwijdert en kiest opnieuw.
- Automatisch toevoegen zonder bevestiging (`auto_create`), en meerdere secties tegelijk.
- Dynamic registration (LTI Dynamic Registration) van tools.

## Capabilities

### New Capabilities
- `lti-deep-linking`: een deep linking-selectie starten, het antwoord van de tool controleren, de selectie bevestigen
  en een tool link starten met het gekozen adres.

### Modified Capabilities
- `lti-tool-registration`: een nieuwe eis "Deep linking per tool" (de twee adressen).

## Impact

- Module Lti: `LtiTool.DeepLinkingUrl`/`PublicKeysUrl` (migratie in schema `lti`), tabel `DeepLinkSelection` en
  `DeepLinkNonce`, `DeepLinkingService`, endpoints onder `/lti/deep-linking`, een bevestigingspagina, velden op
  `ManageTools.razor`, foutcodes `Lti.DeepLinking.*` (resx nl/en).
- Module Content: contract `IContentDirectory.AddToolLinksAsync` en `LaunchUrl` in `ToolLinkInfo`, knop "Add from
  tool" op `ManageContent.razor`. Geen nieuwe tabellen of migraties (de kolom `Url` bestaat al).
- Tests: Lti-tests (LocalDB, een testtool met eigen sleutelpaar), Content-tests, een integratietest op het web.
- Geen nieuwe packages: `Microsoft.IdentityModel.JsonWebTokens` is al aanwezig.
- Beveiliging: Doctrina haalt een JWKS op van een door de beheerder ingevoerd adres (design D6).
