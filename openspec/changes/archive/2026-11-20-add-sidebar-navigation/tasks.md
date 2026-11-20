# Taken

## 1. Tokens

- [x] 1.1 Voeg in `src/Doctrina.UI/Styles/_tokens.scss` de `--dc-sidebar-*`-tokens toe aan `dc-light-colors` en `dc-dark-colors` met de waarden uit design D4, bij voorkeur via de UM-paletvariabelen van `add-um-theme`; controleer dat `dotnet build` slaagt
- [x] 1.2 Voeg aan `KleurcontrastVoldoetAanWcag22AaInBeideThemas` de zijbalkparen toe (`--dc-sidebar-text` en `--dc-sidebar-text-muted` op `--dc-sidebar-bg`, `--dc-sidebar-hover` en `--dc-sidebar-active-bg`; `--dc-sidebar-indicator` en `--dc-focus` op `--dc-sidebar-bg` als niet-tekst ≥ 3:1); controleer dat de tests slagen in beide thema's

## 2. Contracten

- [x] 2.1 Voeg `string? Icon = null` toe als laatste parameter van `NavItem` (`src/Doctrina.Core/Modules/NavItem.cs`) en van `DcNavItem` (`src/Doctrina.UI/Components/DcNavItem.cs`), met Engelse XML-doc; controleer dat de hele solution compileert zonder aanpassing van bestaande aanroepen

## 3. Componenten

- [x] 3.1 Laat `DcNavMenu` per item een `<DcIcon aria-hidden="true">` vóór de tekst renderen als `Icon` gezet is, verwijder de "Menu"-knop en het popover-paneel uit `DcNavMenu` (het wordt een `<nav>` met `<ul>`); controleer hoe `DcIconSet.Content` met een onbekende naam omgaat en leg dat vast
- [x] 3.2 Herbouw `DcAppShell.razor`: header (merk, knop "Menu" met `popovertarget`, thema, account), daaronder `.dc-shell__body` met `<aside>` rond `Navigation` (als popover-paneel met het `id`, vgl. design D2) en `<main>`; houd de skip link als eerste focusbare element
- [x] 3.3 Schrijf de stijlen in `DcAppShell.razor.css` en `DcNavMenu.razor.css` (grid ≥ 64rem, sticky zijbalk, lade < 64rem, hover, actief met balk, focus-ring) uitsluitend met `--dc-*`-tokens; controleer dat de architectuurtest `LiteralColors` slaagt
- [x] 3.4 Geef in `src/Doctrina.Host/Components/Layout/NavMenu.razor` "Home" het icoon `house` en "My profile" `user`, en neem `Icon` over van module-items

## 4. Tests

- [x] 4.1 Pas de tests in `AppShellTests` aan: `HetHoofdmenuWerktOpElkSchermformaat.MenuknopOpSmalleSchermen` zoekt de knop nu in de header en het paneel in de zijbalk; controleer dat de test slaagt
- [x] 4.2 Schrijf `HetHoofdmenuWerktOpElkSchermformaat.ZijbalkOpBredeSchermen` (gecompileerde CSS bevat voor `min-width: 64rem` de verborgen knop en `position: sticky`) en `ElkePaginaHeeftDezelfdeToegankelijkeOpbouw.HetHoofdmenuStaatInDeZijbalk` (nav binnen `aside`, niet binnen `header`)
- [x] 4.3 Breid `HetHoofdmenuWerktOpElkSchermformaat.HuidigePaginaInHetMenu` uit zodat naast `aria-current` de stijl een andere aanduiding dan kleur bevat (balk of vet); controleer dat de test slaagt
- [x] 4.4 Schrijf `MenuItemsKunnenEenIcoonHebben.ItemMetIcoon` en `ItemZonderIcoon` (bUnit op `DcNavMenu`)
- [x] 4.5 Schrijf `DeZijbalkHeeftEigenTokensMetVoldoendeContrast` met de scenario's `TekstcontrastInDeZijbalk`, `IndicatorVanHetActieveItemValtOp` en `ZijbalkstijlenBevattenGeenLetterlijkeKleuren` (hergebruik `TokenFile` en de bestaande controle op letterlijke kleuren); controleer dat ze slagen

## 5. Afronding

- [x] 5.1 Werk `docs/ontwikkelen/vormgeving.md` bij (Engels): zijbalk, `--dc-sidebar-*`-tokens, `Icon` op `NavItem` en hoe een module een icoon kiest uit `DcIconSet`
- [ ] 5.2 Open de host in beide thema's op bijvoorbeeld 1280, 800 en 375 px breed: controleer zijbalk, lade, actief item, focusvolgorde met alleen het toetsenbord (Tab, Escape sluit de lade) en dat de stijlgids de menucomponent nog toont
- [x] 5.3 Draai `dotnet test --solution Doctrina.slnx` en `openspec validate add-sidebar-navigation --strict`; controleer dat alles slaagt en dat elk scenario een test heeft

## Workflow follow-up

- Toepassen ná `add-um-theme`.
- Beoordeel de change vóór `/opsx:apply`; archiveren met sync van de deltas naar de hoofdspecs `app-shell` en `theming`.
