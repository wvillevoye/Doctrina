# Spec-delta

## Purpose
Het onderwijskernmodel van Doctrina volgens OOAPI v5: organisaties, perioden, cursussen, uitvoeringen, personen en
inschrijvingen, met één validatie en één upsert per type voor elke invoerroute.

## ADDED Requirements

### Requirement: Het kernmodel volgt OOAPI v5
De module Academic SHALL de typen `Organization`, `AcademicSession`, `Course`, `CourseOffering`, `Person` en
`Association` bevatten, met naamgeving van velden volgens OOAPI v5. Elk type SHALL uitwisselbaar zijn (`Id`,
`ExternalId`, `Source`, `Status`, `DateLastModified`) en SHALL in het schema `academic` staan.

#### Scenario: Uitvoering met cursus, periode en inschrijving
- **GIVEN** een cursus "BIO101", een periode "2026-P1" en een persoon "I6000001"
- **WHEN** een uitvoering van "BIO101" in "2026-P1" wordt opgeslagen en de persoon daarin als `student` wordt ingeschreven
- **THEN** zijn uitvoering en inschrijving op te vragen, met verwijzingen naar de cursus, de periode en de persoon

### Requirement: Invoer wordt gevalideerd met stabiele foutcodes
Elke invoer van een type SHALL door één validator voor dat type gaan. Ongeldige invoer SHALL een `Result` met
`ErrorType.Validation`, een foutcode met prefix `Academic.` en de fouten per veld opleveren; er SHALL dan niets worden
opgeslagen.

#### Scenario: Verplicht veld ontbreekt
- **WHEN** een persoon zonder achternaam wordt aangeboden
- **THEN** is het resultaat een validatiefout met code `Academic.Person.Invalid` en een fout bij het veld `surname`, en is er geen persoon opgeslagen

#### Scenario: Einddatum voor begindatum
- **WHEN** een periode met einddatum 2026-09-01 en begindatum 2026-11-01 wordt aangeboden
- **THEN** is het resultaat een validatiefout met een fout bij het veld `endDate`

### Requirement: Opslaan is een upsert op ExternalId en Source
Opslaan SHALL een record met dezelfde `ExternalId` en `Source` bijwerken en anders een nieuw record aanmaken. Het
resultaat SHALL aangeven of het record is aangemaakt, bijgewerkt of ongewijzigd. Een ongewijzigd record SHALL zijn
`DateLastModified` houden.

#### Scenario: Nieuw record
- **WHEN** een cursus met `ExternalId` "BIO101" en `Source` "SIS" voor het eerst wordt opgeslagen
- **THEN** is het resultaat "aangemaakt" en bestaat er één cursus "BIO101"

#### Scenario: Bestaand record bijwerken
- **GIVEN** een cursus "BIO101" uit "SIS" met naam "Celbiologie"
- **WHEN** dezelfde cursus met naam "Cell biology" wordt opgeslagen
- **THEN** is het resultaat "bijgewerkt", heeft de cursus de nieuwe naam en bestaat er nog steeds één cursus "BIO101"

#### Scenario: Ongewijzigd record
- **GIVEN** een cursus "BIO101" uit "SIS", laatst gewijzigd op 2026-11-12T10:00:00Z
- **WHEN** om 2026-11-13T09:00:00Z exact dezelfde gegevens worden opgeslagen
- **THEN** is het resultaat "ongewijzigd" en blijft `DateLastModified` 2026-11-12T10:00:00Z

### Requirement: Verwijzingen lopen via ExternalId binnen dezelfde bron
Bij invoer SHALL een verwijzing naar een ander record (bv. de cursus van een uitvoering, de persoon van een inschrijving)
de `ExternalId` van dat record zijn, opgezocht binnen dezelfde `Source`. Een verwijzing naar een onbekend record SHALL
een validatiefout met code `Academic.<Type>.ReferenceNotFound` geven.

#### Scenario: Inschrijving voor een onbekende persoon
- **GIVEN** een uitvoering "BIO101-2026-P1" uit "SIS" en geen persoon "I6999999"
- **WHEN** een inschrijving van "I6999999" in "BIO101-2026-P1" uit "SIS" wordt aangeboden
- **THEN** is het resultaat een validatiefout met code `Academic.Association.ReferenceNotFound` bij het veld `personId`

### Requirement: Inschrijvingen hebben een rol
Een `Association` SHALL precies één rol hebben uit `student`, `lecturer`, `teaching-assistant` en `coordinator`. Een
persoon SHALL per uitvoering hoogstens één inschrijving per rol hebben.

#### Scenario: Onbekende rol
- **WHEN** een inschrijving met rol "teacher" wordt aangeboden
- **THEN** is het resultaat een validatiefout bij het veld `role`

### Requirement: Status "te verwijderen" verbergt een record zonder het te wissen
Een record met status `ToBeDeleted` SHALL blijven bestaan en SHALL weer `Active` kunnen worden via een upsert. Waar
Doctrina actieve gegevens toont (zoals "My courses"), SHALL een record met status `ToBeDeleted` niet meetellen.

#### Scenario: Inschrijving op te verwijderen
- **GIVEN** een actieve inschrijving van "I6000001" als `student` in "BIO101-2026-P1"
- **WHEN** dezelfde inschrijving met status `ToBeDeleted` wordt opgeslagen
- **THEN** bestaat het record nog met status `ToBeDeleted` en telt het niet mee als actieve inschrijving
