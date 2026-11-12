# module-system Specification

## Purpose

Doctrina bestaat uit onafhankelijke feature-modules die via configuratie aan of uit gaan, in een voorspelbare volgorde
starten en hun functionaliteit aan de host leveren zonder van elkaars implementatie afhankelijk te zijn.

## Requirements

### Requirement: Modules worden via configuratie aangezet
De host SHALL alleen de modules starten waarvan de naam in de instelling `Modules:Enabled` staat. Een module die daar
niet in staat SHALL niets bijdragen: geen endpoints, pagina's, navigatie, permissies, health checks of migraties.

#### Scenario: Ingeschakelde module wordt gestart
- **GIVEN** module `Sample` staat in `Modules:Enabled`
- **WHEN** de host start
- **THEN** de endpoints, pagina's en navigatie-items van `Sample` zijn beschikbaar

#### Scenario: Uitgeschakelde module draagt niets bij
- **GIVEN** module `Sample` staat niet in `Modules:Enabled`
- **WHEN** de host start
- **THEN** verzoeken aan endpoints van `Sample` geven 404 en de navigatie-items ervan worden niet getoond

#### Scenario: Onbekende modulenaam laat het opstarten mislukken
- **GIVEN** `Modules:Enabled` bevat een naam die bij geen enkele bekende module hoort
- **WHEN** de host start
- **THEN** het opstarten mislukt met een foutmelding die de onbekende module noemt

### Requirement: Module-afhankelijkheden worden bij het opstarten opgelost
Elke module SHALL de modules declareren waarvan zij afhankelijk is. De host SHALL modules in afhankelijkheidsvolgorde
initialiseren en SHALL weigeren te starten als een afhankelijkheid niet is ingeschakeld of als de afhankelijkheden een
cyclus vormen.

#### Scenario: Afhankelijkheden starten eerst
- **GIVEN** module `B` hangt af van module `A` en beide zijn ingeschakeld
- **WHEN** de host start
- **THEN** module `A` wordt vóór module `B` geïnitialiseerd

#### Scenario: Ontbrekende afhankelijkheid laat het opstarten mislukken
- **GIVEN** module `B` hangt af van module `A` en alleen `B` is ingeschakeld
- **WHEN** de host start
- **THEN** het opstarten mislukt met een foutmelding dat `B` module `A` nodig heeft

#### Scenario: Cyclische afhankelijkheid laat het opstarten mislukken
- **GIVEN** module `A` hangt af van `B` en `B` hangt af van `A`, beide ingeschakeld
- **WHEN** de host start
- **THEN** het opstarten mislukt met een foutmelding die de modules in de cyclus noemt

### Requirement: Modules leveren navigatie
De host SHALL het navigatiemenu opbouwen uit de navigatie-items van ingeschakelde modules. Een item dat een permissie
vereist SHALL alleen getoond worden aan gebruikers die die permissie hebben.

#### Scenario: Navigatie-item zichtbaar met permissie
- **GIVEN** een ingeschakelde module levert een navigatie-item dat permissie `Sample.Manage` vereist
- **WHEN** een gebruiker met `Sample.Manage` een willekeurige pagina opent
- **THEN** het navigatiemenu bevat dat item

#### Scenario: Navigatie-item verborgen zonder permissie
- **GIVEN** een ingeschakelde module levert een navigatie-item dat permissie `Sample.Manage` vereist
- **WHEN** een gebruiker zonder `Sample.Manage` een willekeurige pagina opent
- **THEN** het navigatiemenu bevat dat item niet

### Requirement: Modules melden hun gezondheid
Elke ingeschakelde module SHALL health checks kunnen leveren. Het readiness-endpoint SHALL de checks van elke
ingeschakelde module per modulenaam rapporteren en SHALL ongezond melden zodra een ervan faalt.

#### Scenario: Falende modulecheck maakt de host niet gereed
- **GIVEN** een ingeschakelde module waarvan de health check faalt
- **WHEN** een client het readiness-endpoint opvraagt
- **THEN** de responsestatus is 503 en de falende check staat onder de naam van die module

### Requirement: Modulegrenzen worden tijdens de build bewaakt
De geautomatiseerde testsuite SHALL falen als het implementatieproject van een module naar het implementatieproject
van een andere module verwijst. Verwijzingen naar het `Contracts`-project van een andere module en naar gedeelde
kernprojecten SHALL toegestaan zijn.

#### Scenario: Verwijzing naar de implementatie van een andere module laat de tests falen
- **WHEN** het implementatieproject van een module verwijst naar het implementatieproject van een andere module
- **THEN** de architectuurtests falen en noemen beide projecten

#### Scenario: Verwijzing naar een contracts-project is toegestaan
- **WHEN** het implementatieproject van een module verwijst naar het `Contracts`-project van een andere module
- **THEN** de architectuurtests slagen
