# domain-events Specification

## Purpose

Laat een module reageren op iets dat in een andere module gebeurde (bijvoorbeeld een gewijzigd lidmaatschap) zonder
dat een van beide naar de implementatie van de ander verwijst, met events die in de contracts van de publicerende
module staan.

## Requirements

### Requirement: Events bereiken alle handlers van ingeschakelde modules
Als een module een event publiceert, SHALL het systeem het in-process afleveren aan elke handler voor dat eventtype die
bij een ingeschakelde module hoort. Handlers van uitgeschakelde modules SHALL NOT aangeroepen worden.

#### Scenario: Event bereikt handlers in andere modules
- **GIVEN** ingeschakelde modules `A` en `B`, en `B` handelt event `SampleCreated` af dat in de contracts van `A` staat
- **WHEN** `A` publiceert `SampleCreated`
- **THEN** de handler van `B` ontvangt het event precies één keer

#### Scenario: Handler van een uitgeschakelde module wordt niet aangeroepen
- **GIVEN** module `B` handelt `SampleCreated` af, maar `B` is niet ingeschakeld
- **WHEN** `A` publiceert `SampleCreated`
- **THEN** de handler van `B` wordt niet aangeroepen en het publiceren slaagt

#### Scenario: Event zonder handlers
- **WHEN** een module een event publiceert dat geen enkele ingeschakelde module afhandelt
- **THEN** het publiceren slaagt zonder fout

### Requirement: Een falende handler raakt de publicerende module en andere handlers niet
Als een eventhandler faalt, SHALL het systeem de fout loggen met het eventtype en de naam van de handler, SHALL het
event aan de overige handlers blijven afleveren en SHALL NOT het de fout aan de publicerende module melden.

#### Scenario: Eén handler faalt, de andere draaien gewoon
- **GIVEN** twee handlers voor `SampleCreated`, waarvan de eerste een exception gooit
- **WHEN** `SampleCreated` wordt gepubliceerd
- **THEN** de tweede handler ontvangt het event nog steeds, de publicerende module rondt succesvol af en er wordt een fout gelogd die de falende handler noemt

### Requirement: Events worden pas na het opslaan gepubliceerd
Events die tijdens het wijzigen van gegevens ontstaan, SHALL pas worden afgeleverd nadat die wijziging in de database
is vastgelegd. Als het opslaan mislukt, SHALL NOT de events worden afgeleverd.

#### Scenario: Geen event als het opslaan mislukt
- **GIVEN** een module laat `SampleCreated` ontstaan bij het aanmaken van een entiteit
- **WHEN** het opslaan van de entiteit mislukt
- **THEN** geen enkele handler ontvangt `SampleCreated`
