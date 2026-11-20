# Design

## Context

Zie `proposal.md`. Observaties in de huidige code:

- Alle kleuren staan in `Styles/_tokens.scss`: Sass-variabelen voor het palet en twee mixins (`dc-light-colors`,
  `dc-dark-colors`) die de semantische `--dc-*`-tokens zetten. Componentstijlen verwijzen alleen naar tokens
  (architectuurtest `LiteralColors`).
- `_bootstrap-bridge.scss` zet Bootstrap's `--bs-*`-variabelen op `--dc-*` en laat hover- en actieve toestanden uit de
  tokenkleur komen met `color-mix()` en `--dc-shade` (zwart in licht, wit in donker). De hoverkleuren van de UM-huisstijl
  hoeven dus niet als eigen tokens te bestaan, mits de menging er dichtbij komt (zie D4).
- De contrasttests (`KleurcontrastVoldoetAanWcag22AaInBeideThemas`) lezen `_tokens.scss` regel voor regel en toetsen
  tekstparen op 4,5:1 en focus/randen op 3:1. De paletvariabelen `ink`, `sage`, `paper` en `stone-*` komen buiten
  `_tokens.scss` en `_bootstrap-config.scss` niet voor in `src/`.
- Gemeten contrastverhoudingen van de UM-kleuren (berekend voor dit ontwerp):

  | Paar | Verhouding |
  |---|---|
  | oranje `#D2460F` op wit / wit op oranje | 4,54 |
  | oranje op lichtgrijs `#F5F5F5` | 4,17 (te weinig als tekst) |
  | lichtblauw `#007FAD` op wit | 4,53; op lichtgrijs ≈ 4,1 (te weinig als tekst) |
  | lichtblauw hover `#006089` op wit / lichtgrijs | 6,92 / 6,35 |
  | oranje hover `#B33C0D` op wit / lichtgrijs | 5,88 / 5,40 |
  | grijs `#6C757D` op wit / lichtgrijs | 4,69 / 4,30 (te weinig op lichtgrijs) |
  | oranje en lichtblauw op donkerblauw `#001C3D` | 3,75 / 3,76 (te weinig als tekst) |

## Goals / Non-Goals

**Goals:**
- Doctrina ziet er standaard uit in de UM-kleuren, licht en donker, zonder wijziging in componenten.
- Contrast blijft WCAG 2.2 AA; de bestaande tests blijven de bewaker.

**Non-Goals:**
- Geen nieuwe tokens voor hoverkleuren, geen thema-wissel "Doctrina/UM", geen logo of lettertype.

## Decisions

### D1: UM-palet vervangt het oude palet als standaard

Eén standaard is genoeg; een tweede palet is code zonder gebruiker. Wie het oude uiterlijk wil, laadt een eigen
stylesheet (`Theme:StylesheetPath`). De paletvariabelen heten `$um-dark-blue`, `$um-orange`, `$um-light-blue` en de
neutrale tinten `$um-grey-100` (`#F5F5F5`), `$um-grey-600` (`#6C757D`) en `$um-text` (`#333333`); in `:root` komen
`--dc-um-dark-blue`, `--dc-um-orange` en `--dc-um-light-blue` (voor white-label-stylesheets die ernaar willen verwijzen).
Alternatief: palet behouden en UM als tweede thema. Afgewezen: dubbel onderhoud, en niemand vraagt het oude palet.

### D2: Lichte thema

| Token | Waarde | Reden |
|---|---|---|
| `--dc-bg` | `#F5F5F5` | lichtgrijs uit het palet |
| `--dc-surface` | `#FFFFFF` | wit |
| `--dc-surface-sunken` | `#ECEEF0` | afgeleid, iets donkerder dan de achtergrond |
| `--dc-text` | `#333333` | tekstkleur uit het palet (12,6:1 op wit) |
| `--dc-heading` | `#001C3D` | donkerblauw (17:1) |
| `--dc-text-muted` | `#5F676E` | afgeleid van grijs `#6C757D`, dat 4,30:1 op lichtgrijs haalt; deze 5,27:1 |
| `--dc-border` | `#DDE1E5` | afgeleid, decoratief |
| `--dc-border-strong` | `#6C757D` | grijs; ≥ 3:1 op achtergrond en vlak |
| `--dc-accent` / `--dc-accent-contrast` | `#D2460F` / `#FFFFFF` | merkoranje met witte tekst (4,54:1) |
| `--dc-link` | `#006089` | lichtblauw hover; merkblauw haalt 4,5:1 niet op lichtgrijs |
| `--dc-focus` | `#007FAD` | lichtblauw; voor focus volstaat 3:1 |

De semantische kleuren voor succes, waarschuwing, fout en info blijven ongewijzigd; de contrasttests lopen ze al na op de
nieuwe achtergrond.

### D3: Donkere thema

| Token | Waarde | Reden |
|---|---|---|
| `--dc-bg` | `#001C3D` | donkerblauw |
| `--dc-surface` | `#0A2A52` | afgeleid, lichter dan de achtergrond |
| `--dc-surface-sunken` | `#001428` | donkerblauw hover uit het palet |
| `--dc-text` / `--dc-heading` | `#E8EDF3` | afgeleid, 14,5:1 op de achtergrond |
| `--dc-text-muted` | `#A9B8C9` | afgeleid, 8,4:1 |
| `--dc-border` | `#1B3A63` | afgeleid, decoratief |
| `--dc-border-strong` | `#7389A6` | afgeleid; moet ≥ 3:1 halen op achtergrond en vlak (de eerste keuze `#5B7391` haalt 3,49:1 op de achtergrond en waarschijnlijk minder op het vlak; taak 1.3 meet het) |
| `--dc-accent` / `--dc-accent-contrast` | `#F07A4D` / `#001C3D` | lichtere oranje (6,2:1 op de achtergrond, 5,2:1 op het vlak); donkerblauwe tekst erop 6,2:1 |
| `--dc-link`, `--dc-focus` | `#4DB8E3` | lichtere blauw (7,5:1) |

Merkoranje `#D2460F` haalt op donkerblauw maar 3,75:1 en is dus alleen als vlak geschikt, niet als tekst.

### D4: Hoverkleuren via de bestaande menging

De UM-hovers zijn donkerder varianten (`#B33C0D` ≈ oranje met 15% zwart, `#006089` ≈ blauw met ±25% zwart). De brug mengt
nu al met `--dc-shade`. De tasks controleren het mengpercentage in `_bootstrap-bridge.scss` en passen dat aan als de
uitkomst merkbaar afwijkt; er komen geen losse hovertokens. Alternatief: een `--dc-accent-hover`-token per kleur.
Afgewezen: meer tokens voor een verschil dat de menging al levert.

### D5: Opruimen van het oude palet

`_bootstrap-config.scss` verwijst naar `$dc-ink`, `$dc-accent` en dergelijke; die verwijzingen gaan naar de nieuwe
variabelen. `--dc-shadow-float` gebruikt donkerblauw in plaats van inkt. Geen oude paletvariabelen laten staan "voor
het geval dat": een test (scenario "Oude paletwaarden zijn verdwenen") bewaakt dat.

## Risks / Trade-offs

- [Merkkleuren worden niet 1-op-1 als tekst gebruikt] → de afgeleide tinten staan met hun contrast in deze tabel; de
  contrasttests bewaken ze.
- [Merkvertegenwoordigers verwachten letterlijk `#007FAD` voor links] → alleen vlakken en decoratie gebruiken de merkwaarde
  (D2, D3); leg dat uit bij oplevering, of kies voor knoppen met een lichtblauw vlak en witte tekst (4,53:1).
- [De brug kleurt Bootstrap-componenten anders dan verwacht] → visuele controle van de stijlgids in beide thema's (taak 3.2).
- [Open: UM-lettertype] → buiten scope; apart besluit.

## Open Questions

<!-- geen: scope en kleurwaarden zijn bevestigd; lettertype en logo zijn expliciet buiten de scope -->
