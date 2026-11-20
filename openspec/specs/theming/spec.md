# theming Specification

## Purpose
Geeft Doctrina één herkenbare, toegankelijke vormgeving die uit één set design tokens komt, die licht en donker kan
weergeven naar keuze van de gebruiker, en die een instelling met eigen kleuren en logo kan aanpassen zonder code te
wijzigen.

## Requirements

### Requirement: Design tokens zijn de enige bron van de vormgeving
Kleuren, typografie, spacing, radius, schaduw en beweging SHALL als design tokens (CSS-variabelen met prefix `--dc-`)
gedefinieerd zijn. De stijlen van componenten en van de app-shell SHALL hun kleuren uit tokens halen en SHALL NOT
letterlijke kleurwaarden bevatten. Een gewijzigde tokenwaarde SHALL overal doorwerken zonder andere stijlen aan te passen.

#### Scenario: Gewijzigd accenttoken werkt door in knoppen
- **GIVEN** een stylesheet die alleen `--dc-accent` een andere waarde geeft
- **WHEN** een pagina met een primaire knop wordt weergegeven met die stylesheet
- **THEN** de achtergrondkleur van de knop is de nieuwe accentkleur

#### Scenario: Componentstijlen bevatten geen letterlijke kleuren
- **WHEN** de stijlen van de componenten en de app-shell worden gecontroleerd
- **THEN** komen kleuren alleen voor als verwijzing naar een `--dc-`-token, behalve in de definitie van de tokens zelf

### Requirement: Licht en donker thema naar keuze van de gebruiker
Doctrina SHALL een licht en een donker thema hebben. Zonder keuze van de gebruiker SHALL het thema de systeeminstelling
van het apparaat volgen. De gebruiker SHALL licht, donker of systeem kunnen kiezen; die keuze SHALL bewaard blijven
over sessies heen en SHALL al in de eerste weergave van een pagina toegepast zijn, zonder eerst het andere thema te tonen.

#### Scenario: Zonder keuze volgt het thema het systeem
- **GIVEN** een bezoeker die nog geen thema heeft gekozen
- **WHEN** de bezoeker een pagina opent
- **THEN** is de pagina ingesteld om het systeemthema te volgen

#### Scenario: Gekozen donker thema wordt onthouden
- **GIVEN** een bezoeker die "Dark" kiest
- **WHEN** de bezoeker daarna een willekeurige pagina opent
- **THEN** wordt de pagina direct in het donkere thema weergegeven

#### Scenario: Terug naar de systeeminstelling
- **GIVEN** een bezoeker die eerder "Dark" koos
- **WHEN** de bezoeker "System" kiest
- **THEN** volgt elke volgende pagina weer de systeeminstelling

#### Scenario: Themakeuze werkt zonder scripts
- **GIVEN** een browser waarin scripts niet (of nog niet) draaien
- **WHEN** de bezoeker een thema kiest
- **THEN** wordt de keuze opgeslagen en keert de bezoeker terug naar dezelfde pagina in het gekozen thema

### Requirement: Kleurcontrast voldoet aan WCAG 2.2 AA in beide thema's
In het lichte en het donkere thema SHALL gewone tekst een contrastverhouding van minstens 4,5:1 met haar achtergrond
hebben, en SHALL focus-indicatoren en grenzen van bedieningselementen minstens 3:1 halen.

#### Scenario: Tekstcontrast in het lichte thema
- **WHEN** de contrastverhouding van de tokenparen voor tekst en achtergrond in het lichte thema wordt berekend
- **THEN** is elke verhouding minstens 4,5:1

#### Scenario: Tekstcontrast in het donkere thema
- **WHEN** de contrastverhouding van de tokenparen voor tekst en achtergrond in het donkere thema wordt berekend
- **THEN** is elke verhouding minstens 4,5:1

#### Scenario: Focus-indicator valt op
- **WHEN** de contrastverhouding van de focuskleur met de achtergrond wordt berekend, in beide thema's
- **THEN** is die minstens 3:1

### Requirement: Een instelling kan het thema aanpassen zonder code
Een installatie SHALL via configuratie een eigen stylesheet kunnen laden die tokenwaarden overschrijft en die na de
standaardstijlen wordt toegepast, en SHALL een eigen logo kunnen tonen in plaats van het woordmerk. Zonder deze
instellingen SHALL de standaardvormgeving van Doctrina gelden.

#### Scenario: Eigen thema-stylesheet wordt na de standaardstijlen geladen
- **GIVEN** de instelling `Theme:StylesheetPath` verwijst naar een stylesheet van de instelling
- **WHEN** een pagina wordt weergegeven
- **THEN** wordt die stylesheet geladen ná de standaardstijlen van Doctrina

#### Scenario: Eigen logo vervangt het woordmerk
- **GIVEN** de instellingen `Theme:LogoPath` en `Theme:LogoAlt` zijn gezet
- **WHEN** een pagina wordt weergegeven
- **THEN** toont de header dat logo met die alternatieve tekst als link naar de startpagina

#### Scenario: Zonder eigen thema de standaardvormgeving
- **GIVEN** geen thema-instellingen
- **WHEN** een pagina wordt weergegeven
- **THEN** wordt geen extra stylesheet geladen en toont de header het woordmerk "Doctrina"

### Requirement: Lettertypen en iconen worden zelf gehost
Lettertypen, iconen en stijlen SHALL door Doctrina zelf worden aangeboden. Een pagina SHALL NOT voor haar vormgeving
bronnen van andere domeinen laden, zodat er geen persoonsgegevens (zoals IP-adressen) naar derden gaan.

#### Scenario: Geen externe bronnen voor de vormgeving
- **WHEN** een pagina en haar stylesheets worden opgehaald
- **THEN** verwijzen ze voor lettertypen, iconen en stijlen alleen naar Doctrina zelf

### Requirement: Beweging respecteert de voorkeur van de gebruiker
Animaties en overgangen SHALL kort zijn en SHALL uitgeschakeld worden als de gebruiker in het systeem heeft aangegeven
minder beweging te willen.

#### Scenario: Minder beweging gevraagd
- **GIVEN** een systeem met de voorkeur "minder beweging"
- **WHEN** een pagina met animerende componenten wordt weergegeven
- **THEN** duren animaties en overgangen niet langer dan nodig om de eindtoestand te tonen

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

### Requirement: De zijbalk heeft eigen tokens met voldoende contrast
De zijbalk SHALL zijn kleuren uit eigen `--dc-sidebar-*`-tokens halen, voor het lichte en het donkere thema. Tekst in
de zijbalk SHALL een contrastverhouding van minstens 4,5:1 met de zijbalkachtergrond hebben, en de indicator van het
actieve item SHALL minstens 3:1 halen.

#### Scenario: Tekstcontrast in de zijbalk
- **WHEN** de contrastverhouding van de zijbalktekst en de gedempte zijbalktekst met de zijbalkachtergrond, de hover- en de actieve achtergrond wordt berekend, in beide thema's
- **THEN** is elke verhouding minstens 4,5:1

#### Scenario: Indicator van het actieve item valt op
- **WHEN** de contrastverhouding van `--dc-sidebar-indicator` met de zijbalkachtergrond en de actieve achtergrond wordt berekend, in beide thema's
- **THEN** is die minstens 3:1

#### Scenario: Zijbalkstijlen bevatten geen letterlijke kleuren
- **WHEN** de stijlen van de zijbalk worden gecontroleerd
- **THEN** komen kleuren alleen voor als verwijzing naar een `--dc-`-token
