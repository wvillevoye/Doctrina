# Spec-delta

## Purpose

Biedt modules een gedeelde, toegankelijke set Doctrina-componenten, zodat alle schermen er hetzelfde uitzien en
werken, en voorkomt dat modules hun eigen vormgeving maken buiten het design system om.

## ADDED Requirements

### Requirement: Knoppen en links hebben altijd een toegankelijke naam
Elke knop en elke link uit de componentenbibliotheek SHALL een toegankelijke naam hebben. Een knop die alleen een
icoon toont SHALL een tekstlabel voor hulptechnologie hebben; decoratieve iconen SHALL verborgen zijn voor
hulptechnologie. Een knop die bezig is SHALL dat aangeven en SHALL NOT opnieuw te activeren zijn.

#### Scenario: Knop met alleen een icoon heeft een label
- **GIVEN** een knop met alleen een icoon en het label "Verwijderen"
- **WHEN** de knop wordt weergegeven
- **THEN** heeft de knop de toegankelijke naam "Verwijderen" en is het icoon verborgen voor hulptechnologie

#### Scenario: Knop zonder naam wordt geweigerd
- **WHEN** een knop zonder tekst en zonder label wordt weergegeven
- **THEN** meldt het component een fout aan de ontwikkelaar in plaats van een naamloze knop te tonen

#### Scenario: Bezige knop
- **GIVEN** een knop in de toestand "bezig"
- **WHEN** de knop wordt weergegeven
- **THEN** is de knop uitgeschakeld en meldt hij aan hulptechnologie dat hij bezig is

### Requirement: Formuliervelden zijn gekoppeld aan label en foutmelding
Elk formulierveld SHALL een zichtbaar label hebben dat aan het invoerelement gekoppeld is. Een verplicht veld SHALL als
verplicht gemarkeerd zijn. Een veld met een validatiefout SHALL als ongeldig gemarkeerd zijn en SHALL de foutmelding
aan het invoerelement koppelen, zodat hulptechnologie die voorleest.

#### Scenario: Label hoort bij het invoerveld
- **WHEN** een tekstveld met label "E-mailadres" wordt weergegeven
- **THEN** is het label programmatisch aan het invoerelement gekoppeld

#### Scenario: Verplicht veld
- **WHEN** een verplicht tekstveld wordt weergegeven
- **THEN** is het invoerelement als verplicht gemarkeerd en toont het label dat het veld verplicht is

#### Scenario: Veld met een validatiefout
- **GIVEN** een formulier waarin het veld "E-mailadres" de fout "Ongeldig e-mailadres." heeft
- **WHEN** het formulier wordt weergegeven
- **THEN** is het invoerelement als ongeldig gemarkeerd en verwijst het naar de zichtbare foutmelding "Ongeldig e-mailadres."

### Requirement: Fouten uit een Result worden eenduidig getoond
Een component SHALL een verwachte fout uit een `Result` kunnen tonen: de vertaalde titel bij de foutcode als melding,
en bij een validatiefout de meldingen per veld bij de bijbehorende formuliervelden.

#### Scenario: Fout wordt als melding getoond
- **GIVEN** een fout met code `Sample.Item.NotFound` waarvoor een Nederlandse titel bestaat
- **WHEN** die fout in het component wordt getoond
- **THEN** ziet de gebruiker de Nederlandse titel in een melding die hulptechnologie aankondigt

#### Scenario: Validatiefouten verschijnen bij de velden
- **GIVEN** een validatiefout met meldingen voor de velden `email` en `givenName`
- **WHEN** die fout in een formulier met die velden wordt getoond
- **THEN** staat elke melding bij het bijbehorende veld

### Requirement: Dialogen houden de focus vast
Een dialoog SHALL bij openen de focus naar zich toe nemen en SHALL de rest van de pagina onbereikbaar maken zolang hij
open is. De dialoog SHALL met de Escape-toets en met een sluitknop te sluiten zijn, en SHALL bij sluiten de focus
teruggeven aan het element dat hem opende. Een dialoog SHALL een titel hebben die als zijn naam dient.

#### Scenario: Dialoog openen en met Escape sluiten
- **GIVEN** een knop die een dialoog met titel "Bevestigen" opent
- **WHEN** de gebruiker de dialoog opent en daarna Escape drukt
- **THEN** krijgt de dialoog bij openen de focus en de naam "Bevestigen", en keert de focus na sluiten terug naar de knop

### Requirement: Tabellen zijn semantisch en hebben een lege toestand
Een tabel SHALL een bijschrift of label hebben en kolomkoppen die als kop van hun kolom gemarkeerd zijn. Een tabel
zonder rijen SHALL een melding tonen dat er niets is om te tonen.

#### Scenario: Kolomkoppen en bijschrift
- **WHEN** een tabel met bijschrift "Cursussen" en kolommen "Code" en "Naam" wordt weergegeven
- **THEN** heeft de tabel het bijschrift "Cursussen" en zijn "Code" en "Naam" gemarkeerd als kolomkoppen

#### Scenario: Lege tabel
- **WHEN** een tabel zonder rijen wordt weergegeven met de tekst "Nog geen cursussen."
- **THEN** toont de tabel "Nog geen cursussen." in plaats van een lege tabel

### Requirement: De stijlgids toont elk component
Doctrina SHALL een stijlgids-pagina bieden die elk component uit de bibliotheek in zijn varianten toont, in het lichte en
het donkere thema. De stijlgids SHALL beschikbaar zijn in de omgeving Development en daarbuiten alleen als
`Ui:StyleGuide:Enabled` true is.

#### Scenario: Elk component staat in de stijlgids
- **WHEN** de stijlgids in Development wordt geopend
- **THEN** bevat de pagina een sectie voor elk component uit de bibliotheek

#### Scenario: Stijlgids standaard verborgen in productie
- **GIVEN** de omgeving Production zonder `Ui:StyleGuide:Enabled`
- **WHEN** iemand de stijlgids opent
- **THEN** is de responsestatus 404

### Requirement: Modules geven alleen vorm via de componentenbibliotheek
De geautomatiseerde testsuite SHALL falen als de pagina's, componenten of stylesheets van een module ruwe
Bootstrap-classes of letterlijke kleurwaarden bevatten. Verwijzingen naar `--dc-`-tokens en componenten uit de
bibliotheek SHALL toegestaan zijn.

#### Scenario: Letterlijke kleur in een module laat de tests falen
- **WHEN** een stylesheet van een module een letterlijke kleurwaarde zoals `#c2573a` bevat
- **THEN** falen de architectuurtests en noemen ze het bestand en de regel

#### Scenario: Token gebruiken is toegestaan
- **WHEN** een stylesheet van een module een kleur als `var(--dc-accent)` gebruikt
- **THEN** slagen de architectuurtests
