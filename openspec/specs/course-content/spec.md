# course-content Specification

# Spec-delta

## Purpose
Docenten lesmateriaal laten delen in een uitvoering, als secties met pagina's, bestanden en links, en deelnemers laten
zien wat gepubliceerd is.

## Requirements

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

### Requirement: Content kopiëren naar een lege uitvoering
Een gebruiker die de content van een uitvoering mag beheren SHALL de actieve secties en items van een andere uitvoering
van dezelfde cursus als concept, in dezelfde volgorde en met eigen bestandskopieën, naar deze uitvoering kunnen kopiëren.
De bron SHALL ongewijzigd blijven; items die niet kunnen worden gekopieerd SHALL worden overgeslagen en geteld.

#### Scenario: Docent kopieert de content van een eerdere uitvoering
- **GIVEN** een docent van twee uitvoeringen van dezelfde cursus; de eerste heeft sectie "Week 1" met een pagina, een link en een bestand, de tweede heeft geen content
- **WHEN** de docent op de beheerpagina van de tweede uitvoering "Copy content" kiest met de eerste uitvoering als bron
- **THEN** heeft de tweede uitvoering sectie "Week 1" met dezelfde pagina, link en bestand in dezelfde volgorde, allemaal als concept
- **AND** staat in de melding hoeveel secties en items zijn gekopieerd

#### Scenario: Gekopieerde content is een concept en onzichtbaar voor studenten
- **GIVEN** een gepubliceerde sectie met een gepubliceerd item in de bron
- **WHEN** de content naar een lege uitvoering wordt gekopieerd
- **THEN** zijn de nieuwe sectie en het nieuwe item concept en ziet een student van de nieuwe uitvoering niets

#### Scenario: Bestanden worden gekopieerd en zijn los van de bron
- **GIVEN** een bestand in de bron
- **WHEN** de content wordt gekopieerd en daarna het bestand in de bron wordt verwijderd
- **THEN** is het gekopieerde bestand nog te downloaden met dezelfde naam, hetzelfde type en dezelfde grootte

#### Scenario: De bron blijft ongewijzigd
- **GIVEN** een bron met secties en items, deels als concept en deels gepubliceerd
- **WHEN** de content wordt gekopieerd
- **THEN** zijn de secties, items, volgorde en publicatiestatus van de bron ongewijzigd

#### Scenario: Verwijderde items gaan niet mee
- **GIVEN** een bron met een verwijderde sectie en een verwijderd item
- **WHEN** de content wordt gekopieerd
- **THEN** bevat de kopie die niet

#### Scenario: Tool link behoudt de tool
- **GIVEN** een tool link naar een bestaande tool "Quiz tool" in de bron
- **WHEN** de content wordt gekopieerd
- **THEN** wijst de gekopieerde tool link naar dezelfde tool "Quiz tool"

#### Scenario: Tool link naar een verwijderde tool wordt overgeslagen
- **GIVEN** een tool link in de bron naar een tool die intussen is verwijderd, en een gewone pagina
- **WHEN** de content wordt gekopieerd
- **THEN** staat de pagina in de kopie maar de tool link niet
- **AND** telt de melding één overgeslagen item

#### Scenario: Ontbrekend bestand wordt overgeslagen
- **GIVEN** een bestand-item in de bron waarvan het bestand niet meer op schijf staat, en een gewone pagina
- **WHEN** de content wordt gekopieerd
- **THEN** staat de pagina in de kopie maar het bestand-item niet
- **AND** telt de melding één overgeslagen item

### Requirement: Voorwaarden voor het kopiëren
Kopiëren SHALL alleen lukken als de gebruiker de content van de nieuwe uitvoering en van de bron mag beheren, de bron een
andere uitvoering van dezelfde cursus is, en de nieuwe uitvoering geen actieve secties heeft. Anders SHALL niets
worden gekopieerd. Het kopiëren SHALL alles of niets zijn: een mislukte kopie laat geen secties, items of bestanden achter.

#### Scenario: Uitvoering met content wordt niet overschreven
- **GIVEN** een uitvoering met al een actieve sectie
- **WHEN** een gebruiker content naar deze uitvoering wil kopiëren
- **THEN** wordt niets gekopieerd en geeft de service de foutcode `Content.Copy.TargetNotEmpty`

#### Scenario: Een uitvoering met alleen verwijderde secties telt als leeg
- **GIVEN** een uitvoering waarvan alle secties zijn verwijderd
- **WHEN** een gebruiker er content naartoe kopieert
- **THEN** slaagt het kopiëren

#### Scenario: Bron van een andere cursus wordt geweigerd
- **GIVEN** twee uitvoeringen van verschillende cursussen
- **WHEN** een gebruiker content van de ene naar de andere wil kopiëren
- **THEN** wordt niets gekopieerd en geeft de service de foutcode `Content.Copy.InvalidSource`

#### Scenario: Zonder beheerrecht op de bron wordt niets gekopieerd
- **GIVEN** een docent van de nieuwe uitvoering die geen docent van de bron is en geen `Content.Manage` heeft
- **WHEN** de docent de bron als kopieerbron kiest
- **THEN** wordt niets gekopieerd en geeft de service de foutcode `Content.Access.Forbidden`

#### Scenario: Zonder beheerrecht op de nieuwe uitvoering wordt niets gekopieerd
- **GIVEN** een student van de nieuwe uitvoering
- **WHEN** de student content wil kopiëren
- **THEN** wordt niets gekopieerd en geeft de service de foutcode `Content.Access.Forbidden`

#### Scenario: Kopiëren is alles of niets
- **GIVEN** een bron waarvan het opslaan van de kopie mislukt na het kopiëren van bestanden
- **WHEN** de content wordt gekopieerd
- **THEN** bevat de nieuwe uitvoering geen secties of items en blijven er geen gekopieerde bestanden op schijf

### Requirement: Kopiëren vanaf de beheerpagina
De beheerpagina van de content SHALL de kaart "Copy content" tonen met de andere uitvoeringen van dezelfde cursus als
bron, alleen als de uitvoering geen actieve secties heeft en er andere uitvoeringen zijn. Academic SHALL die andere
uitvoeringen via zijn contract leveren.

#### Scenario: De kaart verschijnt alleen als kopiëren kan
- **GIVEN** een docent van een uitvoering zonder secties, met andere uitvoeringen van dezelfde cursus
- **WHEN** de docent de beheerpagina opent
- **THEN** staat er een kaart "Copy content" met de andere uitvoeringen in de keuzelijst
- **AND** is de kaart er niet als de uitvoering al een sectie heeft, of als de cursus geen andere uitvoeringen heeft

#### Scenario: Academic geeft de andere uitvoeringen van dezelfde cursus
- **GIVEN** een cursus met drie actieve uitvoeringen, een verwijderde uitvoering, en een uitvoering van een andere cursus
- **WHEN** de andere uitvoeringen van een van de drie worden opgevraagd
- **THEN** zijn dat de twee overige actieve uitvoeringen van dezelfde cursus, de nieuwste periode eerst
