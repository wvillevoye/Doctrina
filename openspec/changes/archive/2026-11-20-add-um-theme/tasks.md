# Taken

## 1. Tokens

- [x] 1.1 Vervang in `src/Doctrina.UI/Styles/_tokens.scss` het paletblok door de UM-paletvariabelen (design D1) en zet de `:root`-tokens `--dc-um-dark-blue`, `--dc-um-orange`, `--dc-um-light-blue`; verwijder `ink`, `sage`, `paper` en `stone-*` (ook uit `:root`); controleer met een zoekactie dat deze namen en de waarden `#1f2a44`, `#b44d32`, `#faf7f2` nergens meer in `src/Doctrina.UI` staan
- [x] 1.2 Koppel de semantische tokens van het lichte thema volgens design D2 in `dc-light-colors`; controleer met `dotnet test --project tests/Doctrina.UI.Tests` dat `TekstcontrastInHetLichteThema` en `FocusIndicatorValtOp` slagen
- [x] 1.3 Koppel de semantische tokens van het donkere thema volgens design D3 in `dc-dark-colors` (controleer en pas `--dc-border-strong` aan tot ≥ 3:1 op achtergrond en vlak); controleer dat `TekstcontrastInHetDonkereThema` en `FocusIndicatorValtOp` slagen
- [x] 1.4 Werk de verwijzingen naar het oude palet bij in `_bootstrap-config.scss` en `--dc-shadow-float` (design D5); controleer met `dotnet build` (warnings = errors) en de bestaande tests in `tests/Doctrina.UI.Tests/Theming`
- [x] 1.5 Controleer de menging van hoverkleuren in `_bootstrap-bridge.scss` tegen `#B33C0D` en `#006089` (design D4); pas het percentage aan of leg vast dat het volstaat; controleer met een test of berekening dat de hover van de primaire knop binnen een kleine marge van `#B33C0D` valt en nog 4,5:1 met witte tekst haalt

## 2. Tests

- [x] 2.1 Schrijf test `DeStandaardvormgevingGebruiktHetUmKleurenpalet.MerkkleurenInHetLichteThema` in `tests/Doctrina.UI.Tests/Theming` (accent `#D2460F`, koppen `#001C3D`, tekst `#333333` via `TokenFile`); controleer dat de test slaagt
- [x] 2.2 Schrijf test `DeStandaardvormgevingGebruiktHetUmKleurenpalet.DonkerblauweAchtergrondInHetDonkereThema` (`--dc-bg` = `#001C3D`); controleer dat de test slaagt
- [x] 2.3 Schrijf test `MerkkleurenDieTeZwakZijnAlsTekstKrijgenEenAfgeleideKleur.LinksHalenHetContrastMetAfgeleideKleuren` (`--dc-link` ≥ 4,5:1 op `--dc-bg`, `--dc-surface` en `--dc-surface-sunken`, in beide thema's); controleer dat de test slaagt
- [x] 2.4 Schrijf test `DeStandaardvormgevingGebruiktHetUmKleurenpalet.OudePaletwaardenZijnVerdwenen` (geen `#1f2a44`, `#b44d32`, `#faf7f2` in de bronnen van `Doctrina.UI`, hoofdletterongevoelig); controleer dat de test slaagt

## 3. Afronding

- [x] 3.1 Werk `docs/ontwikkelen/vormgeving.md` bij (Engels): het UM-palet, de tokenkoppeling in beide thema's en de reden voor de afgeleide tinten; de voorbeelden van white-label-overschrijvingen blijven kloppen met de nieuwe tokennamen; controleer met een zoekactie dat het oude palet er niet meer in staat
- [ ] 3.2 Open de stijlgids (`/style-guide`) in beide thema's op de dev-host en controleer dat knoppen, meldingen, tabel, formulier en dialoog goed leesbaar zijn en dat de focus-ring zichtbaar is
- [x] 3.3 Draai `dotnet test` voor de hele solution en `openspec validate add-um-theme --strict`; controleer dat alles slaagt en dat elk scenario uit de spec-delta een test heeft

## Workflow follow-up

- Beoordeel de change vóór `/opsx:apply`.
- Archiveren met sync van de delta naar de hoofdspec `theming`.
- `add-sidebar-navigation` bouwt hierop voort en kan na deze change starten.
