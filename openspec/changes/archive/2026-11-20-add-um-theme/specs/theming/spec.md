## ADDED Requirements

### Requirement: De standaardvormgeving gebruikt het UM-kleurenpalet
Zonder thema-instellingen SHALL Doctrina de kleuren van de UM-huisstijl gebruiken: donkerblauw `#001C3D`, oranje
`#D2460F` en lichtblauw `#007FAD`, met de neutrale tinten uit het huisstijlpalet. Het accent van het lichte thema SHALL
oranje zijn en de koppen donkerblauw; het donkere thema SHALL donkerblauw als achtergrond hebben.

#### Scenario: Merkkleuren in het lichte thema
- **WHEN** de tokenwaarden van het lichte thema worden gelezen
- **THEN** is `--dc-accent` gelijk aan `#D2460F`, `--dc-heading` gelijk aan `#001C3D` en `--dc-text` gelijk aan `#333333`

#### Scenario: Donkerblauwe achtergrond in het donkere thema
- **WHEN** de tokenwaarden van het donkere thema worden gelezen
- **THEN** is `--dc-bg` gelijk aan `#001C3D`

#### Scenario: Oude paletwaarden zijn verdwenen
- **WHEN** de stijlen van Doctrina.UI worden doorzocht op de waarden van het vorige palet (`#1f2a44`, `#b44d32`, `#faf7f2`)
- **THEN** komt geen van die waarden meer voor

### Requirement: Merkkleuren die te zwak zijn als tekst krijgen een afgeleide kleur
Waar een UM-merkkleur als tekst of als focus-indicator te weinig contrast geeft, SHALL het token een afgeleide kleur van
dezelfde tint krijgen die wel aan de eisen voor kleurcontrast voldoet. De merkkleur zelf SHALL dan alleen voor vlakken
en decoratie dienen.

#### Scenario: Links halen het contrast met afgeleide kleuren
- **WHEN** de contrastverhouding van `--dc-link` met achtergrond en vlakken wordt berekend, in beide thema's
- **THEN** is die minstens 4,5:1, ook al is de merkkleur lichtblauw `#007FAD` zelf te zwak als tekst op lichtgrijs
