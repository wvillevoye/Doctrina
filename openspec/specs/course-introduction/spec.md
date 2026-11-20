# course-introduction Specification

## Purpose
Docenten laten een introductie schrijven die bovenaan de cursuspagina staat, in een door beheerders beheerde, vaste
opmaak, zodat elke cursus er herkenbaar en passend bij het thema uitziet.

## Requirements

### Requirement: De introductie staat boven de content van een uitvoering
De pagina `/courses/{offering}` SHALL een sectie "Introduction" tonen boven de sectie "Content", als er een
gepubliceerde introductie is. Deelnemers SHALL een concept niet zien en SHALL geen lege sectie zien; wie de content mag
beheren SHALL de sectie altijd zien: het concept met een badge "Draft", of anders een knop "Add introduction". Het
cursusmenu SHALL een item "Introduction" bevatten als de sectie getoond wordt.

#### Scenario: Introductie boven de content
- **GIVEN** een gepubliceerde introductie met de tekst `# Welcome` in "BIO101-2026-P1" en een sectie "Week 1"
- **WHEN** een ingeschreven student de cursuspagina opent
- **THEN** staat de sectie "Introduction" met de kop "Welcome" vóór de sectie "Content"

#### Scenario: Geen introductie voor deelnemers
- **GIVEN** een uitvoering zonder introductie
- **WHEN** een ingeschreven student de cursuspagina opent
- **THEN** bevat de pagina geen sectie "Introduction" en geen menu-item "Introduction"

#### Scenario: Concept alleen voor beheerders
- **GIVEN** een introductie die niet is gepubliceerd
- **WHEN** een student de pagina opent
- **THEN** staat de introductie niet op de pagina
- **AND WHEN** de docent de pagina opent
- **THEN** staat de introductie er met de badge "Draft"

#### Scenario: Docent voegt een introductie toe
- **GIVEN** een uitvoering zonder introductie
- **WHEN** de docent de cursuspagina opent
- **THEN** staat er een sectie "Introduction" met de knop "Add introduction" naar `/courses/{offering}/introduction`

### Requirement: Docenten bewerken de introductie in Markdown
Wie de content van de uitvoering mag beheren SHALL de introductie bewerken op `/courses/{offering}/introduction`: de
tekst in Markdown, met een voorbeeld, en kiezen of de introductie gepubliceerd is. De tekst SHALL hoogstens 50.000 tekens
bevatten. Anderen SHALL de pagina niet kunnen openen.

#### Scenario: Docent schrijft en publiceert
- **GIVEN** een docent met een actieve inschrijving als `lecturer` in "BIO101-2026-P1"
- **WHEN** de docent de tekst `# Welcome` invult, "Published" aanzet en opslaat
- **THEN** toont de cursuspagina de gepubliceerde introductie met de kop "Welcome"

#### Scenario: Voorbeeld tijdens het bewerken
- **WHEN** de docent bij het bewerken `*draft*` typt en "Preview" kiest
- **THEN** toont de pagina het voorbeeld met "draft" cursief, zonder dat de introductie is opgeslagen

#### Scenario: Te lange tekst
- **WHEN** de docent een tekst van 50.001 tekens opslaat
- **THEN** wordt de introductie niet opgeslagen en toont de pagina een fout bij het veld "Text"

#### Scenario: Student bewerkt niet
- **WHEN** een ingeschreven student `/courses/{offering}/introduction` opent
- **THEN** wordt de student naar de pagina "Access denied" gestuurd

### Requirement: De introductie wordt veilig weergegeven
De tekst SHALL met dezelfde regels als pagina's in de content worden weergegeven: HTML in de tekst SHALL als gewone
tekst worden getoond en links en afbeeldingen SHALL alleen werken met een `http://`- of `https://`-adres (links ook
`mailto:`).

#### Scenario: Scripts worden niet uitgevoerd
- **GIVEN** een gepubliceerde introductie met de tekst `<script>alert(1)</script>` en de link `[x](javascript:alert(1))`
- **WHEN** de cursuspagina wordt weergegeven
- **THEN** bevat de introductie geen `script`-element en geen link naar een `javascript:`-adres

### Requirement: Beheerders beheren introductiesjablonen
Wie `Content.Manage` heeft SHALL op `/content/templates` sjablonen kunnen aanmaken, wijzigen en verwijderen. Een sjabloon
SHALL een naam (verplicht, hoogstens 100 tekens), een opmaak (een van `plain`, `banner` of `objectives`) en een
startinhoud in Markdown hebben. Het hoofdmenu SHALL voor deze beheerders het item "Intro templates" tonen. Er SHALL
altijd een sjabloon "Default" met opmaak `plain` bestaan dat niet verwijderd kan worden. Anderen SHALL de pagina niet
kunnen openen.

#### Scenario: Beheerder maakt een sjabloon
- **WHEN** een beheerder een sjabloon "Course with banner" met opmaak `banner` en startinhoud `# Welcome` opslaat
- **THEN** staat "Course with banner" in de lijst met sjablonen

#### Scenario: Sjabloon zonder naam
- **WHEN** de beheerder een sjabloon zonder naam opslaat
- **THEN** wordt het sjabloon niet opgeslagen en toont de pagina een fout bij het veld "Name"

#### Scenario: Standaardsjabloon blijft bestaan
- **WHEN** de beheerder het sjabloon "Default" probeert te verwijderen
- **THEN** blijft het sjabloon bestaan en toont de pagina "The default template cannot be deleted."

#### Scenario: Docent beheert geen sjablonen
- **GIVEN** een docent zonder `Content.Manage`
- **WHEN** de docent `/content/templates` opent
- **THEN** wordt de docent naar de pagina "Access denied" gestuurd en toont het hoofdmenu geen "Intro templates"

### Requirement: Een uitvoering kiest een sjabloon dat de opmaak bepaalt
De docent SHALL bij het bewerken een sjabloon kiezen; zonder keuze geldt "Default". De sectie SHALL de opmaak van het
gekozen sjabloon tonen, uitsluitend met `--dc-*`-tokens, zodat uitvoeringen met hetzelfde sjabloon er gelijk uitzien.
Is het gekozen sjabloon verwijderd, dan SHALL de opmaak van "Default" gelden.

#### Scenario: Opmaak van het sjabloon
- **GIVEN** twee uitvoeringen met het sjabloon "Course with banner"
- **WHEN** een student beide cursuspagina's opent
- **THEN** hebben beide introducties dezelfde opmaak `banner` en bevatten de stijlen van die opmaak geen letterlijke kleuren

#### Scenario: Verwijderd sjabloon
- **GIVEN** een uitvoering met het sjabloon "Course with banner" dat daarna door een beheerder wordt verwijderd
- **WHEN** een student de cursuspagina opent
- **THEN** toont de sectie de introductietekst met de opmaak `plain`

### Requirement: De startinhoud van een sjabloon vult een lege introductie
Kiest de docent een sjabloon terwijl de tekst leeg is, dan SHALL de startinhoud van het sjabloon in de tekst komen.
Wisselen van sjabloon SHALL een bestaande tekst niet wijzigen.

#### Scenario: Startinhoud bij een lege introductie
- **GIVEN** een sjabloon "Course with banner" met startinhoud `# Welcome` en een uitvoering zonder introductietekst
- **WHEN** de docent dat sjabloon kiest
- **THEN** staat `# Welcome` in het tekstveld

#### Scenario: Sjabloon wisselen behoudt de tekst
- **GIVEN** een introductie met de tekst `Hello` en het sjabloon "Default"
- **WHEN** de docent het sjabloon "Course with banner" kiest en opslaat
- **THEN** is de tekst nog `Hello` en heeft de sectie de opmaak `banner`
