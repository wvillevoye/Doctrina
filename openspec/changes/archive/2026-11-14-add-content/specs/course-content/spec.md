# Spec-delta

## Purpose
Docenten lesmateriaal laten delen in een uitvoering, als secties met pagina's, bestanden en links, en deelnemers laten
zien wat gepubliceerd is.

## ADDED Requirements

### Requirement: Docenten delen de content van een uitvoering in secties in
Een uitvoering SHALL secties kunnen hebben, elk met een titel. De pagina `/courses/{offering}/content` SHALL secties
laten aanmaken, hernoemen en verwijderen. Een sectie verwijderen SHALL ook haar items verwijderen, na een bevestiging.

#### Scenario: Docent maakt een sectie
- **GIVEN** een docent met een actieve inschrijving als `lecturer` in "BIO101-2026-P1"
- **WHEN** de docent een sectie "Week 1" aanmaakt
- **THEN** heeft de uitvoering een lege sectie "Week 1"

#### Scenario: Sectie zonder titel
- **WHEN** de docent een sectie zonder titel aanmaakt
- **THEN** wordt de sectie niet opgeslagen en toont de pagina een fout bij het veld "Title"

#### Scenario: Sectie met items verwijderen
- **GIVEN** een sectie "Week 1" met een pagina en een bestand
- **WHEN** de docent "Week 1" verwijdert en de vraag bevestigt
- **THEN** bestaan de sectie en beide items niet meer en is het bestand niet meer te downloaden

### Requirement: Een sectie bevat pagina's, bestanden en links
Een sectie SHALL items kunnen bevatten van de soort pagina, bestand of link, elk met een titel. Een link SHALL een
webadres hebben dat met `http://` of `https://` begint en SHALL in een nieuw tabblad openen. Items SHALL hernoemd en
verwijderd kunnen worden.

#### Scenario: Link toevoegen
- **WHEN** de docent in "Week 1" een link "Course guide" met adres `https://example.test/guide` toevoegt
- **THEN** bevat "Week 1" het item "Course guide" dat naar dat adres verwijst

#### Scenario: Link zonder geldig adres
- **WHEN** de docent een link met adres `javascript:alert(1)` toevoegt
- **THEN** wordt de link niet opgeslagen en toont de pagina "Enter a web address that starts with http:// or https://."

#### Scenario: Item verwijderen
- **GIVEN** een sectie "Week 1" met de link "Course guide"
- **WHEN** de docent "Course guide" verwijdert
- **THEN** bevat "Week 1" dat item niet meer

### Requirement: Pagina's worden in Markdown geschreven en veilig weergegeven
De tekst van een pagina SHALL Markdown zijn en SHALL als opgemaakte tekst worden weergegeven op
`/courses/{offering}/content/{item}`. HTML in de tekst SHALL als gewone tekst worden getoond, en links in de tekst
SHALL alleen werken als ze naar `http://`, `https://` of `mailto:` verwijzen. Tijdens het bewerken SHALL de docent een
voorbeeld van de opgemaakte tekst kunnen zien.

#### Scenario: Opmaak wordt weergegeven
- **GIVEN** een gepubliceerde pagina "Introduction" met de tekst `# Welcome` en `**Read this first**`
- **WHEN** een ingeschreven student de pagina opent
- **THEN** toont de pagina de kop "Welcome" en de vetgedrukte tekst "Read this first"

#### Scenario: HTML en scripts worden niet uitgevoerd
- **GIVEN** een pagina met de tekst `<script>alert(1)</script>` en de link `[x](javascript:alert(1))`
- **WHEN** de pagina wordt weergegeven
- **THEN** bevat de weergave geen `script`-element en geen link naar een `javascript:`-adres

#### Scenario: Voorbeeld tijdens het bewerken
- **WHEN** de docent bij het bewerken van een pagina `*draft*` typt en "Preview" kiest
- **THEN** toont de pagina het voorbeeld met "draft" cursief, zonder dat de pagina is opgeslagen

### Requirement: Bestanden worden geüpload en gedownload
Een docent SHALL een bestand als item kunnen uploaden; het item SHALL de bestandsnaam en de grootte tonen. Wie het item
mag zien SHALL het bestand kunnen downloaden, met de oorspronkelijke bestandsnaam en dezelfde inhoud. Een bestand dat
groter is dan de ingestelde grens (standaard 100 MB) SHALL geweigerd worden.

#### Scenario: Bestand uploaden en downloaden
- **GIVEN** de docent heeft in "Week 1" het bestand `syllabus.pdf` geüpload en gepubliceerd
- **WHEN** een ingeschreven student het item downloadt
- **THEN** krijgt de student een bestand `syllabus.pdf` met dezelfde inhoud

#### Scenario: Bestand te groot
- **GIVEN** een grens van 1 MB
- **WHEN** de docent een bestand van 2 MB uploadt
- **THEN** wordt het item niet aangemaakt en toont de pagina "The file is larger than 1 MB."

### Requirement: Content is pas zichtbaar na publiceren
Nieuwe secties en items SHALL een concept zijn. Een docent SHALL een sectie of item kunnen publiceren en weer verbergen.
Een item SHALL voor deelnemers alleen zichtbaar zijn als het item en zijn sectie beide gepubliceerd zijn. Wie een item
niet mag zien SHALL bij het rechtstreeks openen ervan status 404 krijgen.

#### Scenario: Nieuw item is een concept
- **GIVEN** een gepubliceerde sectie "Week 1"
- **WHEN** de docent er een pagina "Introduction" aan toevoegt
- **THEN** ziet een ingeschreven student "Introduction" niet

#### Scenario: Gepubliceerd item in een verborgen sectie
- **GIVEN** een gepubliceerde pagina "Introduction" in de sectie "Week 1" die een concept is
- **WHEN** een ingeschreven student de uitvoeringspagina opent
- **THEN** ziet de student "Week 1" en "Introduction" niet

#### Scenario: Item verbergen
- **GIVEN** een gepubliceerde pagina "Introduction" in een gepubliceerde sectie
- **WHEN** de docent "Introduction" verbergt
- **THEN** ziet een ingeschreven student het item niet meer

#### Scenario: Student opent een verborgen pagina rechtstreeks
- **GIVEN** een pagina "Introduction" die een concept is
- **WHEN** een ingeschreven student `/courses/{offering}/content/{item}` van die pagina opent
- **THEN** is de status 404

### Requirement: Docenten bepalen de volgorde van secties en items
Secties SHALL in een vaste volgorde staan binnen de uitvoering, en items binnen hun sectie; nieuwe secties en items
SHALL achteraan komen. Een docent SHALL een sectie of item één plaats omhoog of omlaag kunnen verplaatsen. Deelnemers
SHALL de content in dezelfde volgorde zien.

#### Scenario: Item omhoog verplaatsen
- **GIVEN** een sectie met de items "A", "B" en "C" in die volgorde
- **WHEN** de docent bij "C" "Move up" kiest
- **THEN** is de volgorde "A", "C", "B"

#### Scenario: Sectie omlaag verplaatsen
- **GIVEN** de secties "Week 1" en "Week 2" in die volgorde
- **WHEN** de docent bij "Week 1" "Move down" kiest
- **THEN** is de volgorde "Week 2", "Week 1", ook voor studenten

### Requirement: Alleen docenten van de uitvoering en beheerders beheren content
Het contentbeheer van een uitvoering SHALL toegankelijk zijn voor wie een actieve inschrijving als `lecturer` of
`coordinator` in die uitvoering heeft, en voor wie de permissie `Content.Manage` heeft. Ieder ander ingelogd SHALL de
pagina "Access denied" met status 403 krijgen. Wie mag beheren SHALL ook concepten kunnen zien en openen.

#### Scenario: Student mag geen content beheren
- **WHEN** een student met een actieve inschrijving als `student` in "BIO101-2026-P1" `/courses/{offering}/content` opent
- **THEN** krijgt de student de pagina "Access denied" met status 403

#### Scenario: Docent van een andere uitvoering
- **GIVEN** een docent die alleen in "WIS201-2026-P1" lesgeeft
- **WHEN** de docent het contentbeheer van "BIO101-2026-P1" opent
- **THEN** krijgt de docent de pagina "Access denied" met status 403

#### Scenario: Beheerder zonder inschrijving
- **GIVEN** een gebruiker met `Content.Manage` zonder inschrijving in "BIO101-2026-P1"
- **WHEN** de gebruiker het contentbeheer van "BIO101-2026-P1" opent
- **THEN** wordt het contentbeheer getoond

#### Scenario: Docent opent een concept
- **GIVEN** een pagina "Introduction" die een concept is
- **WHEN** een docent van de uitvoering de pagina opent
- **THEN** toont de pagina de tekst met het label "Draft"

### Requirement: Deelnemers zien de gepubliceerde content op de pagina van de uitvoering
De pagina van een uitvoering SHALL een sectie "Content" tonen met de zichtbare secties en hun items. Gepubliceerde
content SHALL zichtbaar zijn voor wie een actieve inschrijving in de uitvoering heeft en voor wie de permissie
`Content.Read` heeft. Wie mag beheren SHALL daar ook de concepten zien, met het label "Draft", en een knop
"Manage content". Ieder ander SHALL bij het openen van een item de pagina "Access denied" met status 403 krijgen.

#### Scenario: Student ziet gepubliceerde content
- **GIVEN** een gepubliceerde sectie "Week 1" met de gepubliceerde items "Introduction" (pagina) en "Course guide" (link)
- **WHEN** een ingeschreven student de uitvoeringspagina opent
- **THEN** toont "Content" de sectie "Week 1" met beide items, zonder de knop "Manage content"

#### Scenario: Uitvoering zonder content
- **WHEN** een ingeschreven student de pagina opent van een uitvoering zonder gepubliceerde content
- **THEN** toont "Content" de tekst "No content yet."

#### Scenario: Docent ziet concepten
- **GIVEN** een sectie "Week 2" die een concept is
- **WHEN** een docent van de uitvoering de uitvoeringspagina opent
- **THEN** toont "Content" de sectie "Week 2" met het label "Draft" en de knop "Manage content"

#### Scenario: Niet ingeschreven student opent een pagina
- **GIVEN** een gepubliceerde pagina "Introduction" in "BIO101-2026-P1"
- **WHEN** een student zonder inschrijving in die uitvoering de pagina opent
- **THEN** krijgt de student de pagina "Access denied" met status 403

#### Scenario: Lezen met Content.Read zonder inschrijving
- **GIVEN** een gebruiker met `Content.Read` zonder inschrijving in "BIO101-2026-P1"
- **WHEN** de gebruiker een gepubliceerde pagina van die uitvoering opent
- **THEN** wordt de pagina getoond
