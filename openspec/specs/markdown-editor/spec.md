# markdown-editor Specification

## Purpose
Wie Markdown schrijft in Doctrina (introductie, contentpagina's, startinhoud van sjablonen) een werkbalk geven die de
syntax invoegt, zodat de opmaak niet uit het hoofd geleerd hoeft te worden, en een veilige kleurkeuze die bij het thema past.

## Requirements

### Requirement: De werkbalk voegt Markdown-syntax in
De Markdown-editor SHALL boven het tekstveld een werkbalk hebben met knoppen "Heading 1", "Heading 2", "Heading 3",
"Bold", "Italic", "Bulleted list", "Link", "Align left", "Align center", "Align right" en "Color". Een knop SHALL de syntax om de selectie zetten, of een plaatshouder
op de cursor invoegen als er niets geselecteerd is, en SHALL de nieuwe tekst opnemen in de waarde van het veld.

#### Scenario: Kop maken van een regel
- **GIVEN** het tekstveld bevat `Welcome` met de cursor in die regel
- **WHEN** de docent "Heading 2" kiest
- **THEN** bevat het tekstveld `## Welcome`

#### Scenario: Kop vervangt een bestaande kop
- **GIVEN** het tekstveld bevat `# Welcome` met de cursor in die regel
- **WHEN** de docent "Heading 3" kiest
- **THEN** bevat het tekstveld `### Welcome`
- **AND WHEN** de docent opnieuw "Heading 3" kiest
- **THEN** bevat het tekstveld `Welcome`

#### Scenario: Vet om de selectie
- **GIVEN** het tekstveld bevat `Read this first` met "this" geselecteerd
- **WHEN** de docent "Bold" kiest
- **THEN** bevat het tekstveld `Read **this** first` en is "this" nog geselecteerd

#### Scenario: Plaatshouder zonder selectie
- **GIVEN** een leeg tekstveld
- **WHEN** de docent "Italic" kiest
- **THEN** bevat het tekstveld `*text*` met "text" geselecteerd

#### Scenario: Opsomming over meerdere regels
- **GIVEN** het tekstveld bevat de regels `One` en `Two`, beide geselecteerd
- **WHEN** de docent "Bulleted list" kiest
- **THEN** bevat het tekstveld de regels `- One` en `- Two`

#### Scenario: Link maken
- **GIVEN** het tekstveld bevat `Guide` met die tekst geselecteerd
- **WHEN** de docent "Link" kiest
- **THEN** bevat het tekstveld `[Guide](https://)` met `https://` geselecteerd

#### Scenario: Tekst die al getypt was gaat niet verloren
- **GIVEN** de docent heeft `Hello` getypt en geselecteerd zonder het veld te verlaten
- **WHEN** de docent "Bold" kiest
- **THEN** bevat het tekstveld `**Hello**`

### Requirement: De kleurkeuze gebruikt een vaste set thema-kleuren
De knop "Color" SHALL een keuze tonen uit "UM orange", "UM dark blue", "UM light blue", "Success", "Warning", "Danger",
"Muted", "Purple", "Teal", "Pink", "Brown" en "Olive", en SHALL de selectie omgeven met `{naam}` en `{/naam}` (`accent`, `blue`,
`lightblue`, `success`, `warning`, `danger`, `muted`, `purple`, `teal`, `pink`, `brown`, `olive`; bijvoorbeeld `{accent}tekst{/accent}`). Er SHALL geen manier zijn om een eigen kleurcode in te
voeren.

#### Scenario: Kleur kiezen
- **GIVEN** het tekstveld bevat `Important` geselecteerd
- **WHEN** de docent "Color" en daarna "Danger" kiest
- **THEN** bevat het tekstveld `{danger}Important{/danger}`

#### Scenario: Kleurkeuze zonder eigen kleurcode
- **WHEN** de docent de kleurkeuze opent
- **THEN** bevat de keuze precies twaalf opties en geen veld voor een kleurcode

### Requirement: De uitlijningsknoppen zetten tekst links, gecentreerd of rechts
De knoppen "Align left", "Align center" en "Align right" SHALL de selectie omgeven met `{left}`, `{center}` of `{right}`
en de bijbehorende sluitmarkering.

#### Scenario: Tekst centreren
- **GIVEN** het tekstveld bevat `Welcome` geselecteerd
- **WHEN** de docent "Align center" kiest
- **THEN** bevat het tekstveld `{center}Welcome{/center}`

#### Scenario: Kleur en uitlijning combineren
- **GIVEN** het tekstveld bevat `Hi` geselecteerd
- **WHEN** de docent "Align center" en daarna "Color" en "Danger" kiest
- **THEN** bevat het tekstveld `{center}{danger}Hi{/danger}{/center}`

### Requirement: De werkbalk is toegankelijk en houdt de focus in het tekstveld
De werkbalk SHALL een element met `role="toolbar"` en de naam "Formatting" zijn, elke knop SHALL een toegankelijke naam en
een icoon hebben en met het toetsenbord bereikbaar zijn, en na een knop SHALL de focus in het tekstveld staan zodat
doortypen meteen kan.

#### Scenario: Namen en rol
- **WHEN** de editor wordt weergegeven
- **THEN** heeft de werkbalk de rol "toolbar" met de naam "Formatting" en heeft elke knop een toegankelijke naam en een icoon

#### Scenario: Focus blijft in het veld
- **WHEN** de docent een knop kiest
- **THEN** staat de focus daarna in het tekstveld

### Requirement: Pagina's voor Markdown gebruiken dezelfde editor
De introductie, de contentpagina en de startinhoud van een introductiesjabloon SHALL met deze editor worden bewerkt, met
behoud van de knop "Preview" en de bestaande validatie.

#### Scenario: Editor op de contentpagina
- **WHEN** de docent een pagina in de content bewerkt
- **THEN** staat boven het veld "Text" de werkbalk "Formatting" en werkt "Preview" zoals voorheen

#### Scenario: Editor bij de introductie en sjablonen
- **WHEN** de docent de introductie of de beheerder een sjabloon bewerkt
- **THEN** staat boven het tekstveld de werkbalk "Formatting"

### Requirement: Het voorbeeld is een verschuifbaar venster
"Preview" SHALL de opgemaakte tekst tonen in een niet-modaal venster dat de docent over het scherm kan slepen (muis,
aanraking of pijltjestoetsen op de greep), in grootte kan aanpassen en kan sluiten, terwijl het formulier bruikbaar blijft.

#### Scenario: Voorbeeld openen en sluiten
- **GIVEN** een docent bewerkt een pagina met de tekst `*draft*`
- **WHEN** de docent "Preview" kiest
- **THEN** toont een venster met een greep "Preview" en een knop "Close preview" de tekst "draft" cursief
- **AND WHEN** de docent "Close preview" kiest
- **THEN** is het venster verdwenen

### Requirement: Tekstkleuren hebben voldoende contrast in beide thema's
Elke kleur uit de set SHALL als tekstkleur op `--dc-surface` een contrastverhouding van minstens 4,5:1 hebben in het
lichte en het donkere thema.

#### Scenario: Contrast van de tekstkleuren
- **WHEN** de contrasten van `--dc-md-accent`, `--dc-md-blue`, `--dc-md-lightblue`, `--dc-md-success`, `--dc-md-warning`, `--dc-md-danger`, `--dc-md-muted`, `--dc-md-purple`, `--dc-md-teal`, `--dc-md-pink`, `--dc-md-brown` en `--dc-md-olive` op `--dc-surface` worden gemeten
- **THEN** is elk contrast in beide thema's minstens 4,5:1
