## ADDED Requirements

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
