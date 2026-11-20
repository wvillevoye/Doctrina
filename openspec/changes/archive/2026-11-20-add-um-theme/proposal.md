# Voorstel

## Why

De standaardvormgeving van Doctrina gebruikt een eigen palet (inkt, terracotta, perkament) dat niet bij Maastricht
University past. De UM-huisstijlkleuren zijn nu beschikbaar (`https://localhost:5001/brand/colors.json`: donkerblauw
`#001C3D`, oranje `#D2460F`, lichtblauw `#007FAD`, neutrale tinten en hoverkleuren). Doctrina moet er uit de doos als UM
uitzien, zonder dat elke module of pagina verandert: alle stijlen lopen al via `--dc-*`-tokens.

Deze change komt niet uit de roadmap (`docs/Doctrina-voorstel.md` §8). Ze bouwt voort op `add-design-system` en vervangt
het palet uit §7.0 van het voorstel als standaard. De sidebar (`add-sidebar-navigation`) gebruikt daarna deze tokens.

## What Changes

- **Nieuw standaardpalet**: `Styles/_tokens.scss` krijgt de UM-kleuren als paletvariabelen en als
  `--dc-um-*`-tokens. Het oude palet (`ink`, `sage`, `paper`, `stone-*`, terracotta accent) vervalt.
- **Semantische tokens opnieuw gekoppeld**, licht en donker:
  - licht: achtergrond lichtgrijs, vlakken wit, tekst `#333333`, koppen donkerblauw, accent oranje met witte tekst,
    links en focus in blauw;
  - donker: achtergrond donkerblauw, lichter blauw voor vlakken, lichtere varianten van oranje (accent) en lichtblauw
    (link, focus) zodat ze als tekst leesbaar zijn.
- **Contrast blijft WCAG 2.2 AA**: waar een merkkleur als tekst of focus te weinig contrast geeft, krijgt het token een
  donkerdere (licht thema) of lichtere (donker thema) afgeleide kleur. Elke afwijking staat in `design.md` met de
  gemeten verhouding.
- **Documentatie**: `docs/ontwikkelen/vormgeving.md` toont het UM-palet en de tokenkoppeling.
- Geen wijziging in componenten, markup of paden; alleen tokenwaarden, de paletvariabelen en de verwijzingen daarnaar
  (`_bootstrap-config.scss`, schaduw).

## Buiten de scope

- De sidebar en andere layoutwijzigingen: `add-sidebar-navigation`.
- Het UM-logo en lettertypen (Inter en Fraunces blijven; een UM-lettertype is een eigen besluit).
- Een tweede thema of themawissel tussen "Doctrina" en "UM". Het UM-palet wordt de standaard; een installatie kan nog
  steeds eigen tokens laden (`Theme:StylesheetPath`).
- De semantische kleuren voor succes, waarschuwing, fout en info: die staan niet in de UM-palet en blijven zoals ze zijn.

## Capabilities

### New Capabilities
<!-- geen -->

### Modified Capabilities
- `theming`: twee nieuwe requirements: de standaardvormgeving gebruikt het UM-kleurenpalet, en merkkleuren die te zwak zijn als tekst krijgen een afgeleide kleur.

## Impact

- `src/Doctrina.UI/Styles/_tokens.scss` (palet en beide thema-mixins), `_bootstrap-config.scss` (verwijzingen naar het
  oude palet), de schaduwtoken.
- `tests/Doctrina.UI.Tests/Theming`: een test voor de UM-koppeling; de bestaande contrasttests blijven gelden en moeten
  slagen.
- `docs/ontwikkelen/vormgeving.md`. Geen wijziging in database, API, Keycloak of modules.
