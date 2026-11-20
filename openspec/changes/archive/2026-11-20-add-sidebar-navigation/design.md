# Design

## Context

Zie `proposal.md`. Observaties in de huidige code:

- `DcAppShell` zet in één header merk, `Navigation` (de `NavMenu` van de host met `DcNavMenu`), themakeuze en account;
  daarna `<main>`. Alles zit in een kolom van `--dc-content-width` (72rem).
- `DcNavMenu` bevat nu de "Menu"-knop (`popovertarget`) en het paneel (`popover`); vanaf 48rem toont CSS het paneel als
  rij. Huidige pagina: `aria-current="page"` met dikke onderstreping (WCAG 1.4.1). Geen scripts nodig (design D9 van
  `add-design-system`).
- `NavItem` (Core, door modules geleverd) heeft `Title`, `Href`, `RequiredPermission`, `Order`. `DcNavItem` (UI) heeft
  `Title`, `Href`, `MatchAll`. `NavMenu.razor` in de host filtert op permissie en zet om naar `DcNavItem`.
- `DcIcon` rendert Lucide-iconen inline uit `DcIconSet` (nu o.a. `house`, `book-open`, `user`, `settings`).
- Tests: `AppShellTests` (bUnit, klasse = requirement), contrasttests lezen `_tokens.scss`.

## Goals / Non-Goals

**Goals:**
- Zijbalk die er verzorgd uitziet, op elk formaat werkt en zonder scripts werkt.
- Bestaande modules hoeven niets te wijzigen.

**Non-Goals:**
- Geen inklapbare rail, submenu's, groepen of zoeken; geen bibliotheek.

## Decisions

### D1: Geen `DcSidebar`-component; `DcAppShell` plaatst `DcNavMenu`

Het menu is al een eigen component; een extra wrapper voor één gebruiker is code zonder reden. `DcAppShell` krijgt een
`<aside class="dc-shell__sidebar">` om de `Navigation`-fragment en een CSS-grid voor de plaatsing. Alternatief: nieuwe
`DcSidebar`. Afgewezen (YAGNI); later splitsen kan als er een tweede gebruik komt.

### D2: De "Menu"-knop verhuist naar de header

De knop moet in de header staan terwijl het paneel in de zijbalk hangt. `popovertarget` werkt over het hele document,
dus `DcAppShell` rendert de knop (alleen zichtbaar < 64rem) en het paneel krijgt het `id` dat de shell aan `DcNavMenu`
meegeeft (`Id`, bestaat al). Zo blijft het native popover-gedrag: openen/sluiten, Escape en `aria-expanded` door de
browser, geen script. Gevolg: de bUnit-tests die de knop in `DcNavMenu` zoeken verhuizen naar `DcAppShell`.

### D3: Layout met CSS-grid

`.dc-shell__body` is een grid: één kolom (< 64rem), twee kolommen `16rem 1fr` (≥ 64rem). De zijbalk is op brede schermen
`position: sticky; top: 0; align-self: start; height: 100dvh` met eigen `overflow-y: auto`, zodat lange menu's scrollen
binnen de zijbalk. De header blijft in de normale flow bovenaan over de volle breedte; de zijbalk sticky onder de
header begint niet hoger dan de bovenrand (`top: 0`). `<main>` houdt `--dc-content-width` maar als maximum binnen de
rechterkolom. Op smalle schermen is de zijbalk het popover-paneel (zoals nu: gedrapeerd over de volle breedte, `inset`
onder de header) en neemt geen plaats in de flow in.

### D4: Zijbalk heeft de kleur van de achtergrond, met een dunne lijn rechts

Op verzoek van de gebruiker (na de eerste oplevering) heeft de zijbalk dezelfde kleur als de paginaachtergrond, met een dunne lijn (`--dc-sidebar-border`, standaard `--dc-border`) langs de rechterrand over de volle hoogte. De zijbalktokens verwijzen standaard naar de paginatokens, zodat ze met het thema meewisselen:

| Token | Verwijst naar |
|---|---|
| `--dc-sidebar-bg` | `--dc-bg` |
| `--dc-sidebar-text` / `--dc-sidebar-text-muted` | `--dc-text` / `--dc-text-muted` |
| `--dc-sidebar-hover` | `--dc-neutral-subtle` |
| `--dc-sidebar-active-bg` | `--dc-surface` |
| `--dc-sidebar-indicator` | `--dc-accent` |
| `--dc-sidebar-border` | `--dc-border` |

De tabel hieronder beschrijft de eerste, donkerblauwe variant en is vervallen:

| Token | Licht | Donker |
|---|---|---|
| `--dc-sidebar-bg` | `#001C3D` | `#001428` |
| `--dc-sidebar-text` | `#E8EDF3` | `#E8EDF3` |
| `--dc-sidebar-text-muted` | `#A9B8C9` | `#A9B8C9` |
| `--dc-sidebar-hover` | `#0A2A52` | `#0A2A52` |
| `--dc-sidebar-active-bg` | `#123F73` | `#123F73` |
| `--dc-sidebar-indicator` | `#F07A4D` | `#F07A4D` |

Gemeten contrast: tekst 14,5/15,8:1 op de achtergrond, 12,2:1 op hover, 10,6:1 op actief; gedempte tekst 8,4/9,2:1,
7,1:1 en 5,2:1; indicator 6,2/6,7:1 op de achtergrond en 3,8:1 op het actieve vlak. Waarden zijn afgeleid van het UM-palet
uit `add-um-theme` (donkerblauw `#001C3D` en zijn hover `#001428`); de tasks verwijzen naar de paletvariabelen van die
change in plaats van de hexwaarden te herhalen, en de tokens zijn de enige plek met letterlijke kleuren.

### D5: Actief item: achtergrond én balk

Actieve pagina (`aria-current="page"`): `--dc-sidebar-active-bg` plus een 4px-balk links in `--dc-sidebar-indicator`
(box-shadow inset), met vetgedrukte tekst. Zo is het niet alleen kleur (WCAG 1.4.1) en blijft de eis uit de huidige
spec behouden. Focus-ring: bestaande `--dc-focus`; op donkerblauw haalt de lichte focuskleur ≥ 3:1 (de test dekt dat
via een nieuw paar `--dc-focus` op `--dc-sidebar-bg`).

### D6: Optioneel icoon, achteraan in de records

`NavItem` krijgt `string? Icon = null` als laatste parameter, `DcNavItem` ook (`MatchAll` blijft zijn plek houden). Dat
breekt geen aanroepen. `DcNavMenu` rendert `<DcIcon Name=... aria-hidden="true" />` vóór de tekst als `Icon` gezet is.
Een onbekende iconnaam: eerst controleren hoe `DcIconSet.Content` zich gedraagt (taak 3.1) en dat gedrag vastleggen in
een test, zodat een typefout in een module de pagina niet breekt. Alternatief: iconen alleen in de host. Afgewezen:
modules leveren hun eigen items en moeten zelf kunnen kiezen.

### D7: Host-items

`NavMenu.razor` geeft "Home" het icoon `house` en "My profile" het icoon `user`; module-items nemen `Icon` over van
`NavItem`.

## Risks / Trade-offs

- [Sticky zijbalk met `100dvh` springt op mobiel bij verschijnen van de adresbalk] → alleen op ≥ 64rem toegepast, waar
  geen adresbalk-wisselend gedrag is; `dvh` valt terug op `vh` in oudere browsers.
- [Native popover heeft geen focus-trap] → de lade is niet-modaal en sluit met Escape en klik buiten; acceptabel voor
  navigatie (geen dialoog). Taak 4.2 controleert het toetsenbordgedrag handmatig.
- [Donkere zijbalk in het lichte thema valt op] → bewuste keuze; tokens maken een lichte variant via white-label mogelijk.
- [Volgorde: de tokens veronderstellen het UM-palet] → toepassen na `add-um-theme`; zo niet, dan moeten de zijbalktokens
  eerst met letterlijke waarden gevuld worden (taak 1.1 noemt beide).

## Open Questions

- Moet er later een inklapbare icoon-rail komen? Nu buiten de scope; pas beslissen als er gebruikservaring is.
