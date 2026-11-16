# ooapi-consumer Specification

## Purpose
Doctrina vult zichzelf uit een extern OOAPI v5-bronsysteem (SIS), periodiek of op verzoek, via dezelfde validatie en
upsert als de CSV-import en de REST-API, met een rapport per record.

## Requirements

### Requirement: Een OOAPI-bron wordt ingelezen in afhankelijkheidsvolgorde
Een sync van een geconfigureerde bron (`Ooapi:Sources:<naam>`) SHALL in deze volgorde lezen, elk met de paginering van de
standaard tot `hasNextPage` onwaar is: `organizations`, `academic-sessions`, `courses`, de `offerings` van elke cursus,
`persons` en de `associations` van elke offering. De offerings van een cursus SHALL met `since=1900-01-01` worden
opgevraagd, omdat de standaard anders alleen toekomstige offerings toont.

#### Scenario: Alle typen in volgorde
- **GIVEN** een bron met een organisatie, een periode, een cursus met één offering, een persoon en één association
- **WHEN** een sync draait
- **THEN** zijn de aanvragen aan de bron in de volgorde organizations, academic-sessions, courses, offerings van de cursus, persons, associations van de offering en bestaan alle vijf records in Doctrina

#### Scenario: Afgelopen uitvoeringen worden ook gelezen
- **GIVEN** een bron die zonder `since` alleen toekomstige offerings teruggeeft en met `since=1900-01-01` ook een afgelopen offering
- **WHEN** een sync draait
- **THEN** vraagt Doctrina de offerings van de cursus op met `since=1900-01-01` en bestaat ook de afgelopen uitvoering in Doctrina

#### Scenario: Meerdere pagina's
- **GIVEN** een bron met 25 cursussen en `PageSize` 10
- **WHEN** een sync draait
- **THEN** vraagt Doctrina `pageNumber` 1, 2 en 3 op en bestaan alle 25 cursussen

### Requirement: Bovenliggende records worden eerst ingevoerd
Binnen `organizations` en `academic-sessions` SHALL een bovenliggend record vóór zijn onderliggende records worden
ingevoerd, ook als de bron ze in een andere volgorde levert.

#### Scenario: Bovenliggende organisatie komt later in de lijst
- **GIVEN** een bron die organisatie "FSE" met `parent` "UM" vóór organisatie "UM" aanlevert
- **WHEN** een sync draait
- **THEN** bestaan beide organisaties en is "FSE" gekoppeld aan "UM"

### Requirement: De verbinding met de bron is beveiligd
Een sync SHALL met OAuth 2.0 client credentials een token halen en dit hergebruiken tot kort voor het verlopen. Het
token SHALL nooit worden gelogd. Buiten Development SHALL een `BaseUrl` of `TokenUrl` zonder `https` de bron weigeren
bij het starten.

#### Scenario: Token wordt hergebruikt
- **WHEN** een sync meerdere aanvragen doet binnen de geldigheid van het token
- **THEN** is er één tokenaanvraag gedaan

#### Scenario: Tweede aanvraag bij een verlopen token
- **GIVEN** een token dat tijdens de run verloopt
- **WHEN** de sync een nieuwe pagina opvraagt
- **THEN** wordt een nieuw token gehaald en gaat de sync door

#### Scenario: Alleen https buiten Development
- **GIVEN** een bron met een `BaseUrl` die met `http://` begint, in de omgeving Production
- **WHEN** de applicatie start
- **THEN** wordt de bron geweigerd met een melding die de bronnaam noemt

### Requirement: Gelezen records gaan via de gedeelde validatie en upsert
Elk gelezen record SHALL naar de veldnamen van de CSV-import worden vertaald en via `IAcademicRecordImport` door
dezelfde validator en upsert-service gaan als de CSV-import en de REST-API, met `Source` gelijk aan de bronnaam.

#### Scenario: Nieuwe records zijn aangemaakt
- **GIVEN** een bron met één cursus met `primaryCode` "BIO101" en naam "Cell biology"
- **WHEN** een sync draait
- **THEN** bestaat een cursus met `ExternalId` "BIO101", naam "Cell biology" en `Source` gelijk aan de bronnaam, en staat de regel als "created" in het rapport

### Requirement: De sleutel van een record komt uit de bron
De sleutel van een record SHALL `primaryCode.code` zijn, nooit het OOAPI-id van de bron; een record zonder
`primaryCode` SHALL een `failed` regel opleveren. Alleen een association, die in de standaard geen `primaryCode` heeft,
SHALL het `associationId` van de bron als sleutel gebruiken.

#### Scenario: Record zonder primaryCode
- **GIVEN** een bron met een persoon zonder `primaryCode`
- **WHEN** een sync draait
- **THEN** is er geen persoon aangemaakt en toont het rapport voor die regel "failed" met de code `Academic.Person.Invalid`

#### Scenario: Association met het id van de bron
- **GIVEN** een bron met een association met `associationId` "a-1" voor een bestaande persoon en offering
- **WHEN** een sync draait
- **THEN** bestaat een inschrijving met `ExternalId` "a-1"

### Requirement: De velden worden vertaald volgens de OOAPI-standaard
Verwijzingen SHALL via het OOAPI-id van de bron naar het `primaryCode` van het verwezen record worden vertaald. Teksten
SHALL in `Ooapi:Language` worden gelezen, anders in de eerste beschikbare taal. Een onbekende enumwaarde SHALL een
`failed` regel met een validatiecode opleveren. Alleen associations van het type `courseOfferingAssociation` met
`state` "associated" SHALL worden ingelezen; andere SHALL worden overgeslagen.

#### Scenario: Onbekende enumwaarde
- **GIVEN** een bron met een organisatie met `organizationType` "faculty-of-wizardry"
- **WHEN** een sync draait
- **THEN** toont het rapport die organisatie als "failed" met de code `Academic.Organization.Invalid` en gaan de andere records door

#### Scenario: Association van een ander type
- **GIVEN** een bron met een association van `associationType` "programAssociation"
- **WHEN** een sync draait
- **THEN** is er geen inschrijving aangemaakt en meldt de job een waarschuwing over het overgeslagen type

#### Scenario: Niet-actieve association
- **GIVEN** een bron met een association met `state` "pending"
- **WHEN** een sync draait
- **THEN** is er geen inschrijving aangemaakt voor die association

### Requirement: Een sync is een importjob met rapport en dry-run
Een sync SHALL een importjob zijn (aangemaakt als `queued` voordat de run start): dezelfde statussen, aantallen per resultaat (`created`, `updated`, `unchanged`,
`failed`), regels per record (met het type als bestandsnaam) en CSV-download als bij de CSV-import, zichtbaar op de
bestaande importpagina's. Een dry-run SHALL dezelfde verwerking uitvoeren en daarna niets opslaan.

#### Scenario: Records via het contract geven een rapport
- **WHEN** een module drie geldige personen aanlevert via `IAcademicRecordImport` (begin en run)
- **THEN** bestaat een importjob met drie regels "created", met de naam van de bron als `Source`

#### Scenario: Dry-run slaat niets op
- **GIVEN** een bron met een nieuwe cursus
- **WHEN** een sync als dry-run draait
- **THEN** toont het rapport de cursus als "created" en bestaat de cursus daarna niet in Doctrina

#### Scenario: Onbekend type is een mislukte regel
- **WHEN** een module een record van het type "klassen" aanlevert via `IAcademicRecordImport`
- **THEN** toont het rapport die regel als "failed" en gaan de andere records door

### Requirement: Een sync is idempotent en raakt andermans records niet
Dezelfde bron twee keer inlezen SHALL geen dubbele records opleveren; de tweede run SHALL elk ongewijzigd record als
"unchanged" tonen. Een record dat bij een andere bron hoort SHALL niet worden overschreven. Records die de bron niet
meer teruggeeft SHALL in Doctrina blijven bestaan.

#### Scenario: Tweede run geeft unchanged
- **GIVEN** een bron die al eens is ingelezen
- **WHEN** een sync opnieuw draait zonder wijzigingen in de bron
- **THEN** is het aantal records gelijk gebleven en toont het rapport elke regel als "unchanged"

#### Scenario: Record van een andere bron blijft ongemoeid
- **GIVEN** een cursus "BIO101" met `Source` "SIS" en een bron "ooapi-sis2" die een cursus met `primaryCode` "BIO101" en een andere naam levert
- **WHEN** de sync draait
- **THEN** is de cursus van "SIS" ongewijzigd en bestaat ernaast een eigen cursus "BIO101" met `Source` "ooapi-sis2"

#### Scenario: Record niet meer in de bron
- **GIVEN** een eerder ingelezen cursus die de bron niet meer teruggeeft
- **WHEN** een sync draait
- **THEN** bestaat de cursus nog steeds en is haar status ongewijzigd

### Requirement: Een fout in de bron stopt de run met een duidelijke melding
Een netwerkfout, time-out, 429 of 5xx SHALL worden herhaald tot in totaal drie pogingen, met oplopende wachttijd (bij
429 met `Retry-After`); een andere 4xx SHALL niet worden herhaald. Blijft een aanvraag mislukken, dan SHALL de job `failed`
eindigen met een melding die het type en de statuscode noemt maar geen token, geheim of persoonsgegevens. Wat al was
opgeslagen SHALL blijven staan. Het aantal records per run SHALL begrensd zijn door `Academic:Import:MaxRows`.

#### Scenario: Serverfout na drie pogingen
- **GIVEN** een bron die op `courses` steeds 503 geeft
- **WHEN** een sync draait
- **THEN** eindigt de job als `failed` met een melding die "courses" en "503" noemt, en blijven de al opgeslagen organisaties staan

#### Scenario: Autorisatiefout
- **GIVEN** een bron die het token weigert met 401
- **WHEN** een sync draait
- **THEN** eindigt de job als `failed` zonder herhaling en zonder het token in de melding

#### Scenario: Te veel records
- **GIVEN** `Academic:Import:MaxRows` is 5 en een bron met 6 cursussen
- **WHEN** een sync draait
- **THEN** eindigt de job als `failed` met een melding over het maximum

### Requirement: Een sync draait op schema en op verzoek
Per ingeschakelde bron met een `Schedule` SHALL een recurring achtergrondjob zijn geregistreerd; een bron zonder
`Schedule` of met `Enabled` onwaar SHALL er geen hebben. Per bron SHALL hooguit één run tegelijk draaien.
`POST /api/v1/ooapi-sources/{name}/syncs` SHALL met `Ooapi.Sync` een run in de wachtrij zetten en 202 met het job-id
geven; een onbekende of uitgeschakelde bron SHALL 404 geven en een lopende run 409. Zonder token SHALL de status 401 zijn
en met een token zonder `Ooapi.Sync` 403.

#### Scenario: Bron met schema is geregistreerd
- **GIVEN** een bron met `Schedule` "0 3 * * *"
- **WHEN** de applicatie start
- **THEN** bestaat een recurring job voor die bron met dat schema

#### Scenario: Bron zonder schema
- **GIVEN** een bron zonder `Schedule`
- **WHEN** de applicatie start
- **THEN** bestaat er geen recurring job voor die bron

#### Scenario: Uitgeschakelde bron
- **GIVEN** een bron met `Enabled` onwaar en een `Schedule`
- **WHEN** de applicatie start
- **THEN** bestaat er geen recurring job voor die bron

#### Scenario: Sync starten op verzoek
- **GIVEN** een systeem met `Ooapi.Sync` en een ingeschakelde bron
- **WHEN** het systeem `POST /api/v1/ooapi-sources/{name}/syncs` aanroept
- **THEN** is de status 202 met een job-id en staat de job op de importpagina

#### Scenario: Zonder token
- **WHEN** `POST /api/v1/ooapi-sources/{name}/syncs` zonder token wordt aangeroepen
- **THEN** is de status 401

#### Scenario: Token met alleen Ooapi.Read
- **WHEN** een systeem met alleen `Ooapi.Read` de sync probeert te starten
- **THEN** is de status 403 en is er geen job aangemaakt

#### Scenario: Onbekende bron
- **WHEN** een systeem met `Ooapi.Sync` een sync start voor een bron die niet bestaat
- **THEN** is de status 404

#### Scenario: Lopende run
- **GIVEN** een run die nog bezig is voor een bron
- **WHEN** een systeem een tweede run voor dezelfde bron start
- **THEN** is de status 409 en is er geen tweede job aangemaakt

### Requirement: Een sync logt niets persoonlijks of geheims
Tijdens een sync SHALL de log alleen bronnaam, type, aantallen, statuscodes en job-id bevatten; nooit namen,
e-mailadressen, tokens of het clientgeheim.

#### Scenario: Logs bij een run met personen
- **GIVEN** een bron met personen en `Trace`-logging
- **WHEN** een sync draait
- **THEN** bevat geen logregel een naam, een e-mailadres, een token of het clientgeheim
