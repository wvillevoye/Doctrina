## MODIFIED Requirements

### Requirement: De werkbalk voegt Markdown-syntax in
De Markdown-editor SHALL boven het tekstveld een werkbalk hebben met de keuze "Heading" en de knoppen "Bold", "Italic", "Strikethrough", "Inline code", "Bulleted list", "Numbered list", "Task list", "Quote", "Code block", "Link", "Image", "Table", "Horizontal rule", uitlijning en "Color". Een knop SHALL de syntax om de selectie zetten of een plaatshouder invoegen. Regelknoppen SHALL bij herhaling de syntax weghalen.

#### Scenario: Kop maken van een regel
- **GIVEN** het tekstveld bevat `Welcome` met de cursor in die regel
- **WHEN** de docent bij "Heading" de optie "Heading 2" kiest
- **THEN** bevat het tekstveld `## Welcome`

#### Scenario: Kop vervangt een bestaande kop
- **GIVEN** het tekstveld bevat `# Welcome` met de cursor in die regel
- **WHEN** de docent bij "Heading" de optie "Heading 3" kiest
- **THEN** bevat het tekstveld `### Welcome`
- **AND WHEN** de docent opnieuw "Heading 3" kiest
- **THEN** bevat het tekstveld `Welcome`

#### Scenario: Normale tekst haalt de kop weg
- **GIVEN** het tekstveld bevat `## Welcome` met de cursor in die regel
- **WHEN** de docent bij "Heading" de optie "Normal text" kiest
- **THEN** bevat het tekstveld `Welcome`

#### Scenario: Vet om de selectie
- **GIVEN** het tekstveld bevat `Read this first` met "this" geselecteerd
- **WHEN** de docent "Bold" kiest
- **THEN** bevat het tekstveld `Read **this** first` en is "this" nog geselecteerd

#### Scenario: Plaatshouder zonder selectie
- **GIVEN** een leeg tekstveld
- **WHEN** de docent "Italic" kiest
- **THEN** bevat het tekstveld `*text*` met "text" geselecteerd

#### Scenario: Opsomming over meerdere regels
- **GIVEN** het tekstveld bevat de regels `One` en `Two`, beide geselecteerd
- **WHEN** de docent "Bulleted list" kiest
- **THEN** bevat het tekstveld de regels `- One` en `- Two`

#### Scenario: Link maken
- **GIVEN** het tekstveld bevat `Guide` met die tekst geselecteerd
- **WHEN** de docent "Link" kiest
- **THEN** bevat het tekstveld `[Guide](https://)` met `https://` geselecteerd

#### Scenario: Tekst die al getypt was gaat niet verloren
- **GIVEN** de docent heeft `Hello` getypt en geselecteerd zonder het veld te verlaten
- **WHEN** de docent "Bold" kiest
- **THEN** bevat het tekstveld `**Hello**`

#### Scenario: Doorhalen
- **GIVEN** het tekstveld bevat `old` geselecteerd
- **WHEN** de docent "Strikethrough" kiest
- **THEN** bevat het tekstveld `~~old~~`

#### Scenario: Code in de regel
- **GIVEN** het tekstveld bevat `run dotnet build` met "dotnet build" geselecteerd
- **WHEN** de docent "Inline code" kiest
- **THEN** staat "dotnet build" tussen backticks in het tekstveld

#### Scenario: Genummerde lijst
- **GIVEN** het tekstveld bevat de regels `One` en `Two`, beide geselecteerd
- **WHEN** de docent "Numbered list" kiest
- **THEN** bevat het tekstveld de regels `1. One` en `2. Two`
- **AND WHEN** de docent opnieuw "Numbered list" kiest
- **THEN** bevat het tekstveld de regels `One` en `Two`

#### Scenario: Lijstsoort wisselen
- **GIVEN** het tekstveld bevat de regels `- One` en `- Two`, beide geselecteerd
- **WHEN** de docent "Numbered list" kiest
- **THEN** bevat het tekstveld de regels `1. One` en `2. Two`

#### Scenario: Takenlijst
- **GIVEN** het tekstveld bevat de regels `One` en `Two`, beide geselecteerd
- **WHEN** de docent "Task list" kiest
- **THEN** bevat het tekstveld de regels `- [ ] One` en `- [ ] Two`

#### Scenario: Citaat
- **GIVEN** het tekstveld bevat `Wise words` met de cursor in die regel
- **WHEN** de docent "Quote" kiest
- **THEN** bevat het tekstveld `> Wise words`

#### Scenario: Codeblok met taal
- **GIVEN** het tekstveld bevat `var x = 1;` geselecteerd
- **WHEN** de docent bij "Code block" de taal "C#" kiest
- **THEN** staat `var x = 1;` tussen een openingsregel van drie backticks met `csharp` en een sluitregel van drie backticks

#### Scenario: Afbeelding invoegen
- **GIVEN** het tekstveld bevat `Diagram` met die tekst geselecteerd
- **WHEN** de docent "Image" kiest
- **THEN** bevat het tekstveld `![Diagram](https://)` met `https://` geselecteerd

#### Scenario: Tabel invoegen
- **GIVEN** een leeg tekstveld
- **WHEN** de docent "Table" kiest
- **THEN** bevat het tekstveld een pipetabel met een kopregel, een scheidingsregel en één lege rij met twee kolommen

#### Scenario: Horizontale lijn
- **GIVEN** het tekstveld bevat `One` met de cursor aan het einde
- **WHEN** de docent "Horizontal rule" kiest
- **THEN** staat `---` op een eigen regel in het tekstveld, met een lege regel ervoor

#### Scenario: Geen uploadknop
- **WHEN** de editor wordt weergegeven
- **THEN** bevat de werkbalk geen knop om een bestand te uploaden
