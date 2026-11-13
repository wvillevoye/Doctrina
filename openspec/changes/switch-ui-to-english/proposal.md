# Voorstel

## Why

Volgens de taalafspraak van 2026-11-12 (`AGENTS.md`) is de gebruikersinterface van Doctrina Engels. De bestaande UI is
nog Nederlands: teksten in de host, de layout en de `Dc*`-componenten, `lang="nl"` en Nederlandse paden zoals
`/inloggen` en `/profiel`. Sinds `add-idp-unavailable-notice` staat er al een Engelse pagina tussen. Elke nieuwe module
(te beginnen met `add-academic-core`) zou die mengvorm overnemen. Omzetten kan nu nog zonder kosten: Doctrina draait niet
in productie, dus er zijn geen bladwijzers, koppelingen of gebruikers die op de oude paden rekenen.

Deze change komt niet uit de roadmap (`docs/Doctrina-voorstel.md` §8). Ze volgt uit de taalafspraak en bouwt voort op
`add-design-system` en `add-idp-unavailable-notice`.

## What Changes

- **Teksten Engels**: alle zichtbare teksten worden Engels, ook verborgen teksten voor hulptechnologie. Dat geldt voor
  de hostpagina's (start, profiel, geen toegang, foutpagina, stijlgids) en de layout. In de `Dc*`-componenten gaat het
  om skiplink, menuknop, accountmenu, themakeuze, "(verplicht)", "(bezig)", sluitknop van de dialoog en het label van
  de ernst van een melding. De teksten staan direct in de componenten; er komt geen vertaallaag.
- **Taal van de pagina**: `<html lang="en">`.
- **BREAKING (paden)**: de routes worden Engels, zonder doorverwijzing vanaf de oude paden:

  | Oud | Nieuw |
  |---|---|
  | `/inloggen` | `/sign-in` |
  | `/uitloggen` | `/sign-out` |
  | `/profiel` | `/profile` |
  | `/geen-toegang` | `/access-denied` |
  | `/fout` | `/error` |
  | `/stijlgids` | `/style-guide` |
  | `/inloggen-onbereikbaar` (met `uitgelogd=1`) | `/sign-in-unavailable` (met `signedOut=1`) |
  | `/thema` | `/theme` |

  De OIDC-callbacks (`/signin-oidc`, `/signout-callback-oidc`) zijn al Engels. De redirect-URI's in Keycloak veranderen
  dus niet.
- **Fouttitels in de UI**: een fout uit een `Result` die in de UI wordt getoond, heeft altijd de Engelse titel, ook als
  de browser om Nederlands vraagt. De API houdt haar gedrag: `Accept-Language` bepaalt de taal, met Nederlands als
  standaard.
- **Documentatie** met paden of schermteksten wordt bijgewerkt (`docs/ontwikkelen/*`, `docs/doctrina-integratie.md`,
  `dev-stack/README.md`, scripts die paden gebruiken).

## Buiten de scope

- Een Nederlandse vertaling of een taalkeuze in de UI. Als die nodig is, komt er een eigen change met resource-bestanden.
- De taal van de API (`Accept-Language`, Nederlands als standaard): blijft zoals in `api-conventions`.
- Opmaak van datums en getallen. Die volgt de cultuur van het verzoek, net als nu.
- Codecommentaar en bestaande Nederlandse documentatie: dat Engels maken hoort bij herschrijven om een andere reden
  (taalafspraak). Alleen de genoemde paden en teksten in die documenten worden bijgewerkt.
- Testnamen en OpenSpec-artefacten blijven Nederlands.
- Foutmeldingen van exceptions voor ontwikkelaars (bv. `DcButton` zonder naam). Die zijn geen UI.
- Voorbeeldteksten in de scenario's van `ui-components` ("Verwijderen", "E-mailadres", "Cursussen"). Dat zijn
  voorbeelden van inhoud die een module aanlevert, geen teksten van Doctrina.

## Capabilities

### New Capabilities
<!-- geen -->

### Modified Capabilities
- `app-shell`: taal `en`, skiplink "Skip to content", accountmenu "Sign in"/"My profile"/"Sign out", themakeuze
  "Dark"; nieuwe requirement dat de UI Engels is, met Engelse paden en Engelse fouttitels.
- `theming`: de keuzes in de scenario's heten "Dark" en "System".
- `user-authentication`: paden `/profile` en `/sign-in`, knop "Sign out", terugkeeradres `/profile?tab=2` in de melding
  bij een onbereikbare inlogdienst.
- `access-control`: de weigerpagina heet "Access denied".

`app-shell` en `theming` komen uit `add-design-system`, de requirement over de onbereikbare inlogdienst uit
`add-idp-unavailable-notice`. Die twee changes moeten dus eerst gearchiveerd zijn voordat deze change gearchiveerd kan
worden.

## Impact

- `src/Doctrina.Host`: pagina's, layout, `App.razor`, de route van het thema-endpoint en de registratie van de
  fouttitels voor de UI.
- `src/Doctrina.Infrastructure/Authentication`: `SignInPath`, `SignOutPath`, `UnavailablePath`, de queryparameter
  voor uitloggen en het pad `AccessDeniedPath`.
- `src/Doctrina.UI`: teksten in de componenten en de standaardwaarden voor paden (`ProfileHref`, `/theme`).
- `tests/*`: de verwachtingen in bUnit-, integratie- en architectuurtests op teksten en paden, en de samplepagina's in
  `Doctrina.TestKit` (`/sample/fout`, `/sample/beheer`).
- Documentatie zoals hierboven. Geen wijziging in database, API-contract of Keycloak-configuratie.
