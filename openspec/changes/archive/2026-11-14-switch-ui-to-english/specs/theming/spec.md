# Spec-delta

## MODIFIED Requirements

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
