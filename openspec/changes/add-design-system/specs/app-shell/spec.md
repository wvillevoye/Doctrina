# Spec-delta

## Purpose

Bepaalt hoe elke pagina van Doctrina is opgebouwd: merk, hoofdmenu, accountmenu en themakeuze in een vaste,
toegankelijke layout die op elk schermformaat werkt, zodat gebruikers zich in alle modules op dezelfde manier oriënteren.

## ADDED Requirements

### Requirement: Elke pagina heeft dezelfde toegankelijke opbouw
Elke pagina SHALL een header met het merk (woordmerk of logo, als link naar de startpagina), het hoofdmenu en het
accountgedeelte hebben, gevolgd door de hoofdinhoud. De pagina SHALL een link "Naar de inhoud" als eerste focusbare
element hebben, SHALL landmarks gebruiken voor header, navigatie en hoofdinhoud, en SHALL de taal Nederlands opgeven.
De paginatitel SHALL de vorm "<pagina> · Doctrina" hebben.

#### Scenario: Opbouw van een pagina
- **WHEN** een bezoeker de startpagina opent
- **THEN** is de eerste focusbare link "Naar de inhoud", zijn er landmarks voor header, hoofdmenu en hoofdinhoud en is de taal `nl`

#### Scenario: Paginatitel
- **WHEN** een ingelogde gebruiker de pagina "Mijn profiel" opent
- **THEN** is de titel van het browservenster "Mijn profiel · Doctrina"

### Requirement: Het accountgedeelte toont wie er is ingelogd
Voor een anonieme bezoeker SHALL het accountgedeelte een knop "Inloggen" tonen die na het inloggen terugkeert naar de
huidige pagina. Voor een ingelogde gebruiker SHALL het de naam tonen, met een menu dat "Mijn profiel" en "Uitloggen"
bevat.

#### Scenario: Anonieme bezoeker
- **WHEN** een anonieme bezoeker een openbare pagina opent
- **THEN** toont het accountgedeelte "Inloggen" met de huidige pagina als terugkeeradres

#### Scenario: Ingelogde gebruiker
- **WHEN** een ingelogde gebruiker een pagina opent
- **THEN** toont het accountgedeelte de naam van de gebruiker en een menu met "Mijn profiel" en "Uitloggen"

### Requirement: Het hoofdmenu werkt op elk schermformaat
Op brede schermen SHALL het hoofdmenu altijd zichtbaar zijn. Op smalle schermen SHALL het hoofdmenu achter een knop
"Menu" zitten die aangeeft of het menu open is, met het toetsenbord te bedienen is en ook werkt zonder scripts. Het
menu-item van de huidige pagina SHALL als huidige pagina gemarkeerd zijn.

#### Scenario: Menuknop op smalle schermen
- **WHEN** de layout wordt weergegeven
- **THEN** is er een knop "Menu" die het hoofdmenu opent en sluit zonder dat daar scripts voor nodig zijn

#### Scenario: Huidige pagina in het menu
- **WHEN** een ingelogde gebruiker "Mijn profiel" opent
- **THEN** is het menu-item "Mijn profiel" gemarkeerd als huidige pagina

### Requirement: De themakeuze staat in de header
De header SHALL een themakeuze bevatten met de opties licht, donker en systeem, waarbij de actieve keuze herkenbaar is
voor zowel zienden als hulptechnologie.

#### Scenario: Actieve themakeuze
- **GIVEN** een bezoeker die "donker" heeft gekozen
- **WHEN** de bezoeker een pagina opent
- **THEN** toont de themakeuze in de header "donker" als actieve keuze

### Requirement: Hostpagina's gebruiken de componentenbibliotheek
De pagina's van de host — start, mijn profiel, geen toegang en de foutpagina — SHALL zijn opgebouwd uit componenten van
de bibliotheek en SHALL NOT eigen kleuren of ruwe Bootstrap-classes gebruiken, zodat ze dezelfde regels volgen als modules.

#### Scenario: Hostpagina's volgen de regels voor modules
- **WHEN** de pagina's en stylesheets van de host worden gecontroleerd op ruwe Bootstrap-classes en letterlijke kleurwaarden
- **THEN** worden er geen gevonden
