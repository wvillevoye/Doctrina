## ADDED Requirements

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
