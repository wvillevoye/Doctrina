# Voorstel

## Why

Het hoofdmenu staat nu als horizontale balk in de header. Naarmate modules menu-items toevoegen (cursussen, groepen,
LTI, beheer) past dat niet meer, en het oogt als een prototype. Een zijbalk is de gangbare plek voor een LMS-hoofdmenu:
schaalt mee met het aantal items, houdt de header vrij voor merk, themakeuze en account, en past bij de UM-uitstraling
uit `add-um-theme`. Een externe bibliotheek (Blazor Bootstrap) is niet nodig: de huidige native aanpak (Popover API,
statische weergave) dekt de zijbalk en blijft zonder scripts werken.

Deze change komt niet uit de roadmap (`docs/Doctrina-voorstel.md` §8). Ze bouwt voort op `add-design-system` en hoort
ná `add-um-theme` toegepast te worden, omdat de zijbalkkleuren uit het UM-palet komen.

## What Changes

- **Zijbalklayout in `DcAppShell`**: de header (merk, themakeuze, account) blijft bovenaan over de volle breedte; het
  hoofdmenu verhuist naar een zijbalk links naast de hoofdinhoud. Brede schermen (≥ 64rem): zijbalk altijd zichtbaar en
  meescrollend (sticky). Smalle schermen: zijbalk als lade (popover) achter de knop "Menu" in de header.
- **`DcNavMenu` wordt de zijbalknavigatie**: verticale lijst, met optioneel icoon per item, duidelijke hover- en
  actieve toestand. De "Menu"-knop verhuist van `DcNavMenu` naar de header van `DcAppShell`.
- **Optioneel icoon op menu-items**: `NavItem` (Core) en `DcNavItem` (UI) krijgen een optionele `Icon` (naam van een
  Lucide-icoon uit `DcIconSet`). Bestaande modules blijven werken zonder icoon; de hostitems "Home" en "My profile" krijgen er wel een.
- **Zijbalktokens**: `--dc-sidebar-bg`, `--dc-sidebar-text`, `--dc-sidebar-text-muted`, `--dc-sidebar-hover`,
  `--dc-sidebar-active-bg` en `--dc-sidebar-indicator` in `_tokens.scss` voor licht en donker; de zijbalk heeft de
  kleur van de paginaachtergrond met een dunne lijn rechts (`--dc-sidebar-border`), met contrast ≥ 4,5:1 voor tekst en ≥ 3:1 voor de actieve indicator.
  `--dc-sidebar-border` komt erbij.
- **Documentatie** (`docs/ontwikkelen/vormgeving.md`): zijbalk, tokens, `Icon` op menu-items.

## Buiten de scope

- UM-palet zelf (`add-um-theme`), logo en lettertypen.
- Inklapbare icoon-rail, groepen of submenu's in de zijbalk, zoeken en broodkruimels: pas als er bij gebruik behoefte aan blijkt.
- Blazor Bootstrap of enige andere UI-bibliotheek, en Bootstrap-JS.
- Een aparte `DcSidebar`-component: `DcNavMenu` blijft de menukomponent en `DcAppShell` bepaalt de plaatsing.

## Capabilities

### New Capabilities
<!-- geen -->

### Modified Capabilities
- `app-shell`: de opbouw van de pagina (header zonder hoofdmenu, zijbalk met hoofdmenu), het gedrag van het hoofdmenu op
  brede en smalle schermen, en optionele iconen bij menu-items.
- `theming`: tokens voor de zijbalk met contrasteisen.

## Impact

- `src/Doctrina.UI/Components/DcAppShell.razor(.css)`, `DcNavMenu.razor(.css)`, `DcNavItem.cs`, `Styles/_tokens.scss`.
- `src/Doctrina.Core/Modules/NavItem.cs` (optioneel `Icon`, achteraan zodat bestaande aanroepen blijven compileren);
  `src/Doctrina.Host/Components/Layout/NavMenu.razor` (icoon doorgeven, hostitems).
- `tests/Doctrina.UI.Tests` (shell- en menutests, contrasttests); de stijlgids in de host.
- `docs/ontwikkelen/vormgeving.md`. Geen wijziging in database, API of Keycloak.
