# Spec-delta

## Purpose
Docenten studenten van een uitvoering laten indelen in groepen, handmatig of willekeurig, zodat studenten en latere
modules weten wie bij wie hoort.

## ADDED Requirements

### Requirement: Docenten beheren GroupSets en groepen in hun uitvoering
Een uitvoering SHALL GroupSets kunnen hebben, elk met een naam, een keuze of de set zichtbaar is voor studenten en een
optionele maximale groepsgrootte, en met groepen die elk een naam hebben. De pagina `/courses/{offering}/groups` SHALL
GroupSets en groepen laten aanmaken, hernoemen en verwijderen.

#### Scenario: Docent maakt een GroupSet met groepen
- **GIVEN** een docent met een actieve inschrijving als `lecturer` in "BIO101-2026-P1"
- **WHEN** de docent een GroupSet "Project teams" met de groepen "Team A" en "Team B" aanmaakt
- **THEN** heeft de uitvoering een GroupSet "Project teams" met twee lege groepen

#### Scenario: Groep met leden verwijderen
- **GIVEN** een groep "Team A" met twee leden
- **WHEN** de docent "Team A" verwijdert
- **THEN** bestaat de groep niet meer en zijn beide studenten niet meer ingedeeld in die GroupSet

### Requirement: Alleen docenten van de uitvoering en beheerders beheren groepen
Het groepsbeheer van een uitvoering SHALL toegankelijk zijn voor wie een actieve inschrijving als `lecturer` of
`coordinator` in die uitvoering heeft, en voor wie de permissie `Groups.Manage` heeft. Ieder ander ingelogd SHALL de
pagina "Access denied" met status 403 krijgen.

#### Scenario: Student mag geen groepen beheren
- **WHEN** een student met een actieve inschrijving als `student` in "BIO101-2026-P1" `/courses/{offering}/groups` opent
- **THEN** krijgt de student de pagina "Access denied" met status 403

#### Scenario: Docent van een andere uitvoering
- **GIVEN** een docent die alleen in "WIS201-2026-P1" lesgeeft
- **WHEN** de docent het groepsbeheer van "BIO101-2026-P1" opent
- **THEN** krijgt de docent de pagina "Access denied" met status 403

#### Scenario: Beheerder zonder inschrijving
- **GIVEN** een gebruiker met `Groups.Manage` zonder inschrijving in "BIO101-2026-P1"
- **WHEN** de gebruiker het groepsbeheer van "BIO101-2026-P1" opent
- **THEN** wordt het groepsbeheer getoond

### Requirement: Studenten worden handmatig ingedeeld
Een docent SHALL een student met een actieve inschrijving als `student` in de uitvoering aan een groep kunnen toevoegen
en eruit kunnen halen. Een student SHALL per GroupSet in hoogstens één groep zitten; toevoegen aan een andere groep in
dezelfde set SHALL de student verplaatsen. Een groep die vol is volgens de maximale groepsgrootte SHALL geen nieuw lid
accepteren. Personen zonder actieve studentinschrijving in de uitvoering SHALL NOT lid kunnen worden.

#### Scenario: Student toevoegen
- **GIVEN** een GroupSet met de groepen "Team A" en "Team B" en een student "I6000001" die nog niet is ingedeeld
- **WHEN** de docent "I6000001" aan "Team A" toevoegt
- **THEN** is "I6000001" lid van "Team A"

#### Scenario: Student verplaatsen
- **GIVEN** student "I6000001" is lid van "Team A"
- **WHEN** de docent "I6000001" aan "Team B" in dezelfde GroupSet toevoegt
- **THEN** is "I6000001" lid van "Team B" en niet meer van "Team A"

#### Scenario: Groep is vol
- **GIVEN** een GroupSet met maximale groepsgrootte 2 en een groep "Team A" met twee leden
- **WHEN** de docent een derde student aan "Team A" toevoegt
- **THEN** wordt dat geweigerd met de melding dat de groep vol is, en heeft "Team A" nog twee leden

#### Scenario: Persoon zonder studentinschrijving
- **GIVEN** een persoon die in de uitvoering alleen als `lecturer` is ingeschreven
- **WHEN** de docent die persoon aan een groep wil toevoegen
- **THEN** wordt dat geweigerd en is de persoon geen lid

### Requirement: Studenten worden willekeurig verdeeld
Een docent SHALL de studenten van de uitvoering die in een GroupSet nog niet zijn ingedeeld, in willekeurige volgorde
kunnen verdelen: over een gegeven aantal nieuwe groepen, of in nieuwe groepen van een gegeven grootte. De groepen
SHALL zo gelijk mogelijk van grootte zijn (verschil hoogstens één) en de namen "Group 1", "Group 2", … krijgen,
doorgenummerd na bestaande groepen met die naamvorm. Al ingedeelde studenten SHALL blijven waar ze zijn.

#### Scenario: Verdelen over drie groepen
- **GIVEN** een lege GroupSet en tien studenten in de uitvoering
- **WHEN** de docent de studenten over 3 groepen verdeelt
- **THEN** zijn er drie groepen met 4, 3 en 3 leden en zit elke student in precies één groep

#### Scenario: Groepen van vier
- **GIVEN** een lege GroupSet en tien studenten in de uitvoering
- **WHEN** de docent de studenten in groepen van 4 verdeelt
- **THEN** zijn er drie groepen met 4, 3 en 3 leden

#### Scenario: Al ingedeelde studenten blijven
- **GIVEN** een GroupSet met groep "Group 1" waarin twee van de tien studenten zitten
- **WHEN** de docent de overige studenten over 2 groepen verdeelt
- **THEN** zitten de twee studenten nog in "Group 1" en zijn de acht anderen verdeeld over "Group 2" en "Group 3" met elk vier leden

### Requirement: Wijzigingen in groepslidmaatschap worden als event gepubliceerd
Elke toevoeging, verwijdering of verplaatsing van een lid SHALL na het opslaan een event `GroupMembershipChanged`
publiceren, met de uitvoering, de GroupSet, de groep, de persoon en of het lid is toegevoegd of verwijderd.

#### Scenario: Verplaatsen geeft twee events
- **GIVEN** student "I6000001" is lid van "Team A"
- **WHEN** de docent "I6000001" naar "Team B" verplaatst
- **THEN** worden een event "removed" voor "Team A" en een event "added" voor "Team B" gepubliceerd

### Requirement: Studenten zien hun groepen op de pagina van de uitvoering
De pagina van een uitvoering SHALL voor een student een sectie "My groups" tonen met de groep per GroupSet die zichtbaar
is voor studenten, en de namen van de leden van die groep. GroupSets die niet zichtbaar zijn voor studenten SHALL NOT
getoond worden. Voor wie de groepen mag beheren SHALL de sectie een link naar het groepsbeheer bevatten.

#### Scenario: Zichtbare en verborgen GroupSet
- **GIVEN** student "I6000001" zit in "Team A" van de zichtbare GroupSet "Project teams" en in "Lab 2" van de verborgen GroupSet "Lab groups"
- **WHEN** de student de pagina van de uitvoering opent
- **THEN** toont "My groups" "Project teams: Team A" met de namen van de leden, en niets over "Lab groups"

#### Scenario: Docent ziet de link naar het groepsbeheer
- **WHEN** een docent van de uitvoering de pagina van de uitvoering opent
- **THEN** bevat de sectie een link "Manage groups" naar `/courses/{offering}/groups`
