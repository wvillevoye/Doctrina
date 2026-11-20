## MODIFIED Requirements

### Requirement: Elke pagina heeft dezelfde toegankelijke opbouw
Elke pagina SHALL een header met het merk (woordmerk of logo, als link naar de startpagina), de themakeuze en het
accountgedeelte hebben, een zijbalk met het hoofdmenu, en de hoofdinhoud. De pagina SHALL een link "Skip to content" als
eerste focusbare element hebben, SHALL landmarks gebruiken voor header, navigatie en hoofdinhoud, en SHALL de taal
Engels opgeven. De paginatitel SHALL de vorm "<pagina> · Doctrina" hebben.

#### Scenario: Opbouw van een pagina
- **WHEN** een bezoeker de startpagina opent
- **THEN** is de eerste focusbare link "Skip to content", zijn er landmarks voor header, hoofdmenu en hoofdinhoud en is de taal `en`

#### Scenario: Paginatitel
- **WHEN** een ingelogde gebruiker de pagina "My profile" opent
- **THEN** is de titel van het browservenster "My profile · Doctrina"

#### Scenario: Het hoofdmenu staat in de zijbalk
- **WHEN** de layout wordt weergegeven
- **THEN** staat het hoofdmenu in een zijbalk naast de hoofdinhoud en niet in de header

### Requirement: Het hoofdmenu werkt op elk schermformaat
Op brede schermen SHALL de zijbalk met het hoofdmenu altijd zichtbaar zijn en bij scrollen in beeld blijven. Op smalle
schermen SHALL de zijbalk achter een knop "Menu" in de header zitten die aangeeft of het menu open is, met het
toetsenbord te bedienen is en ook werkt zonder scripts. Het menu-item van de huidige pagina SHALL als huidige pagina
gemarkeerd zijn, niet alleen met kleur.

#### Scenario: Menuknop op smalle schermen
- **WHEN** de layout wordt weergegeven
- **THEN** is er in de header een knop "Menu" die de zijbalk opent en sluit zonder dat daar scripts voor nodig zijn

#### Scenario: Zijbalk op brede schermen
- **WHEN** de stijlen voor een breed scherm (vanaf 64rem) worden toegepast
- **THEN** is de zijbalk zichtbaar zonder de knop "Menu" en blijft hij bij scrollen in beeld

#### Scenario: Huidige pagina in het menu
- **WHEN** een ingelogde gebruiker "My profile" opent
- **THEN** is het menu-item "My profile" gemarkeerd als huidige pagina, met een andere aanduiding dan alleen kleur

## ADDED Requirements

### Requirement: Menu-items kunnen een icoon hebben
Een menu-item SHALL optioneel een icoon kunnen hebben, aangeduid met de naam van een icoon uit de eigen iconenset. Het
icoon SHALL decoratief zijn (verborgen voor hulptechnologie) en de tekst SHALL altijd zichtbaar blijven. Een item
zonder icoon SHALL zonder fout worden weergegeven.

#### Scenario: Item met icoon
- **GIVEN** een menu-item "Home" met het icoon "house"
- **WHEN** het menu wordt weergegeven
- **THEN** toont het item de tekst "Home" met een icoon dat `aria-hidden` is

#### Scenario: Item zonder icoon
- **GIVEN** een menu-item zonder icoon
- **WHEN** het menu wordt weergegeven
- **THEN** toont het item alleen de tekst
