# Spec-delta

## ADDED Requirements

### Requirement: De gebruikersinterface is Engels
Alle zichtbare teksten van Doctrina, en de teksten die alleen hulptechnologie voorleest, SHALL Engels zijn. De paden
van de pagina's en endpoints van de host SHALL Engels zijn. Een fout uit een `Result` die in de UI wordt getoond, SHALL
de Engelse titel krijgen, ongeacht de taal die de browser vraagt.

#### Scenario: Engelse paden
- **WHEN** een ingelogde gebruiker `/profile` opent
- **THEN** wordt de profielpagina getoond, en `/profiel` geeft status 404

#### Scenario: Fouttitel in de UI is Engels bij een Nederlandse browser
- **GIVEN** een foutcode met een Nederlandse en een Engelse vertaling
- **WHEN** een pagina die fout toont aan een browser die `Accept-Language: nl` stuurt
- **THEN** toont de melding de Engelse vertaling als titel

## MODIFIED Requirements

### Requirement: Elke pagina heeft dezelfde toegankelijke opbouw
Elke pagina SHALL een header met het merk (woordmerk of logo, als link naar de startpagina), het hoofdmenu en het
accountgedeelte hebben, gevolgd door de hoofdinhoud. De pagina SHALL een link "Skip to content" als eerste focusbare
element hebben, SHALL landmarks gebruiken voor header, navigatie en hoofdinhoud, en SHALL de taal Engels opgeven.
De paginatitel SHALL de vorm "<pagina> · Doctrina" hebben.

#### Scenario: Opbouw van een pagina
- **WHEN** een bezoeker de startpagina opent
- **THEN** is de eerste focusbare link "Skip to content", zijn er landmarks voor header, hoofdmenu en hoofdinhoud en is de taal `en`

#### Scenario: Paginatitel
- **WHEN** een ingelogde gebruiker de pagina "My profile" opent
- **THEN** is de titel van het browservenster "My profile · Doctrina"

### Requirement: Het accountgedeelte toont wie er is ingelogd
Voor een anonieme bezoeker SHALL het accountgedeelte een knop "Sign in" tonen die na het inloggen terugkeert naar de
huidige pagina. Voor een ingelogde gebruiker SHALL het de naam tonen, met een menu dat "My profile" en "Sign out"
bevat.

#### Scenario: Anonieme bezoeker
- **WHEN** een anonieme bezoeker een openbare pagina opent
- **THEN** toont het accountgedeelte "Sign in" met de huidige pagina als terugkeeradres

#### Scenario: Ingelogde gebruiker
- **WHEN** een ingelogde gebruiker een pagina opent
- **THEN** toont het accountgedeelte de naam van de gebruiker en een menu met "My profile" en "Sign out"

### Requirement: Het hoofdmenu werkt op elk schermformaat
Op brede schermen SHALL het hoofdmenu altijd zichtbaar zijn. Op smalle schermen SHALL het hoofdmenu achter een knop
"Menu" zitten die aangeeft of het menu open is, met het toetsenbord te bedienen is en ook werkt zonder scripts. Het
menu-item van de huidige pagina SHALL als huidige pagina gemarkeerd zijn.

#### Scenario: Menuknop op smalle schermen
- **WHEN** de layout wordt weergegeven
- **THEN** is er een knop "Menu" die het hoofdmenu opent en sluit zonder dat daar scripts voor nodig zijn

#### Scenario: Huidige pagina in het menu
- **WHEN** een ingelogde gebruiker "My profile" opent
- **THEN** is het menu-item "My profile" gemarkeerd als huidige pagina

### Requirement: De themakeuze staat in de header
De header SHALL een themakeuze bevatten met de opties licht, donker en systeem, waarbij de actieve keuze herkenbaar is
voor zowel zienden als hulptechnologie.

#### Scenario: Actieve themakeuze
- **GIVEN** een bezoeker die "Dark" heeft gekozen
- **WHEN** de bezoeker een pagina opent
- **THEN** toont de themakeuze in de header "Dark" als actieve keuze
