# Design

## Context

Zie `proposal.md` voor de aanleiding. De huidige stand:

- Routes staan op een paar vaste plekken: `AuthenticationEndpoints` (`SignInPath`, `SignOutPath`, `UnavailablePath`),
  `IdentityProviderUnavailableMiddleware.SignedOutParameter` (`uitgelogd`), `AccessDeniedPath` in
  `AuthenticationServiceCollectionExtensions`, `DcTheme.EndpointPath`, de `@page`-richtlijnen van de hostpagina's, de
  `ExceptionHandlingPath` in `Program.cs` en de standaardwaarde `ProfileHref` in `DcAccountMenu` en `DcAppShell`.
- Fouttitels komen uit `.resx`-bestanden (neutraal = Nederlands, `.en` = Engels) via `ErrorTitles`. De API gebruikt
  `ErrorTitles.For(code, fallback, culture)` met de cultuur van het verzoek. `DcResult` gebruikt de abstractie
  `IErrorTitles` (design D8 van `add-design-system`), die nu naar dezelfde cultuur van het verzoek kijkt.
- `UseDoctrinaRequestLocalization` zet de cultuur op basis van `Accept-Language` (nl standaard). Dat bepaalt ook de
  opmaak van datums en getallen op pagina's; dat blijft zo (buiten de scope).

## Goals / Non-Goals

**Goals:**
- Elke route en elke tekst op één plek wijzigen, zodat er na de change geen Nederlands pad of Nederlandse UI-tekst
  meer in `src/` staat.
- De UI krijgt Engelse fouttitels zonder dat de API van gedrag verandert.

**Non-Goals:**
- Geen vertaallaag, resource-bestanden of `IStringLocalizer` voor UI-teksten.
- Geen doorverwijzingen van oude naar nieuwe paden.
- Geen hernoeming van code-identifiers buiten de paginacomponenten (D4); testnamen en testklassen blijven Nederlands,
  net als OpenSpec-artefacten.

## Decisions

### D1: Teksten direct in de componenten, geen vertaallaag

De teksten worden in de `.razor`-bestanden zelf vervangen door Engels. Er is maar één UI-taal; een resource-laag voor
één taal is code die niets doet. Komt er ooit een tweede taal, dan voegt die change de resources toe (zie proposal,
buiten de scope).

Alternatief: `IStringLocalizer` met alleen `.resx` in het Engels. Afgewezen: extra indirectie zonder gebruiker.

### D2: `IErrorTitles` levert altijd Engels; `ErrorTitles.For` voor de API blijft taalafhankelijk

De expliciete implementatie `IErrorTitles.For` in `ErrorTitles` roept `For(code, fallback, CultureInfo.GetCultureInfo("en"))`
aan in plaats van de cultuur van het verzoek. `IErrorTitles` bestaat alleen voor de UI (`DcResult`); de API
(`DoctrinaProblemDetails`) gebruikt de concrete `ErrorTitles` en blijft dus `Accept-Language` volgen. De documentatie
van `IErrorTitles` wordt aangepast: "in de taal van de UI (Engels)".

Alternatief: een aparte `IErrorTitles`-implementatie in de host die `ErrorTitles` met vaste cultuur omwikkelt.
Afgewezen: een extra klasse en registratie voor één regel verschil; het bestaande onderscheid tussen interface (UI) en
concrete klasse (API) volstaat. De architectuurtest `ComponentenKrijgenDezelfdeTitelsViaIErrorTitles` (zelfde
instantie) blijft geldig.

### D3: Paden blijven constanten op hun huidige plek

Alleen de waarden wijzigen (`SignInPath = "/sign-in"`, `SignOutPath = "/sign-out"`, `UnavailablePath =
"/sign-in-unavailable"`, `SignedOutParameter = "signedOut"`, `AccessDeniedPath = "/access-denied"`,
`DcTheme.EndpointPath = "/theme"`, `ExceptionHandlingPath = "/error"`, `ProfileHref = "/profile"`). Tests verwijzen
waar mogelijk naar de constanten; letterlijke paden in tests worden Engels. Zo staat elk pad één keer in de code.

### D4: Paginacomponenten krijgen Engelse namen

De componenten van de hostpagina's worden hernoemd naar hun route: `Profiel` → `Profile`, `GeenToegang` →
`AccessDenied`, `Fout` → `Error`, `Stijlgids` → `StyleGuide` (map en bestand), `NietToegestaan` → `NotAuthorized`.
Bestandsnaam en route lopen anders uiteen, wat bij zoeken verwarrend is. Overige identifiers blijven staan.

De samplepagina's in `Doctrina.TestKit` krijgen Engelse routes (`/sample/fout` → `/sample/error`, `/sample/beheer` →
`/sample/manage`), zodat er geen Nederlands pad in de host te openen is.

### D5: De 404 op oude paden is gewoon "geen route"

`/profiel` geeft 404 omdat er geen route meer is; er komt geen aparte afhandeling. De test voor het scenario "Engelse
paden" controleert dat `/profile` werkt en `/profiel` 404 geeft.

## Risks / Trade-offs

- [Een Nederlandse tekst of pad blijft achter] → bij afronden een zoekactie over `src/` en `tests/Doctrina.TestKit` op
  de oude paden en op veelvoorkomende Nederlandse woorden in `.razor`-bestanden (taak in `tasks.md`).
- [Ontwikkelaars met oude bladwijzers (`/stijlgids`) krijgen 404] → geaccepteerd; Doctrina draait niet in productie.
  De documentatie noemt de nieuwe paden.
- [Datums op pagina's in Nederlandse opmaak bij Engelse tekst] → geaccepteerd en buiten de scope; volgt de cultuur van
  het verzoek.

## Migration Plan

Geen data of configuratie: de OIDC-callbacks en Keycloak-redirect-URI's veranderen niet. Archiveren kan pas na
`add-design-system` en `add-idp-unavailable-notice` (zie proposal). Terugdraaien = de commit terugdraaien.
