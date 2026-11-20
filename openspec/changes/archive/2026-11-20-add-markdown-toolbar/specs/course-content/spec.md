## MODIFIED Requirements

### Requirement: Pagina's worden in Markdown geschreven en veilig weergegeven
De tekst van een pagina SHALL Markdown zijn en SHALL als opgemaakte tekst worden weergegeven op
`/courses/{offering}/content/{item}`. HTML in de tekst SHALL als gewone tekst worden getoond, en links in de tekst
SHALL alleen werken als ze naar `http://`, `https://` of `mailto:` verwijzen. Tijdens het bewerken SHALL de docent een
voorbeeld van de opgemaakte tekst kunnen zien. Tekst tussen `{naam}` en `{/naam}`, met `naam` een van `accent`,
`blue`, `lightblue`, `success`, `warning`, `danger`, `muted`, `purple`, `teal`, `pink`, `brown` of `olive`, SHALL met de bijbehorende thema-kleur worden weergegeven,
en met `naam` `left`, `center` of `right` SHALL de tekst op die manier worden uitgelijnd, in beide gevallen zonder
`style`-attribuut en zonder andere waarden toe te laten; elke andere of niet gesloten markering SHALL gewone tekst blijven.

#### Scenario: Opmaak wordt weergegeven
- **GIVEN** een gepubliceerde pagina "Introduction" met de tekst `# Welcome` en `**Read this first**`
- **WHEN** een ingeschreven student de pagina opent
- **THEN** toont de pagina de kop "Welcome" en de vetgedrukte tekst "Read this first"

#### Scenario: HTML en scripts worden niet uitgevoerd
- **GIVEN** een pagina met de tekst `<script>alert(1)</script>` en de link `[x](javascript:alert(1))`
- **WHEN** de pagina wordt weergegeven
- **THEN** bevat de weergave geen `script`-element en geen link naar een `javascript:`-adres

#### Scenario: Voorbeeld tijdens het bewerken
- **WHEN** de docent bij het bewerken van een pagina `*draft*` typt en "Preview" kiest
- **THEN** toont de pagina het voorbeeld met "draft" cursief, zonder dat de pagina is opgeslagen

#### Scenario: Kleur wordt weergegeven
- **GIVEN** een pagina met de tekst `{danger}Deadline{/danger} is Friday`
- **WHEN** de pagina wordt weergegeven
- **THEN** staat "Deadline" in een element met de klasse `md-color--danger` en bevat de weergave geen `style`-attribuut

#### Scenario: Uitlijning wordt weergegeven
- **GIVEN** een pagina met de tekst `{center}Welcome{/center}`
- **WHEN** de pagina wordt weergegeven
- **THEN** staat "Welcome" in een element met de klasse `md-align--center` en bevat de weergave geen `style`-attribuut

#### Scenario: Kleur om opgemaakte tekst
- **GIVEN** een pagina met de tekst `{accent}**Read** this{/accent}`
- **WHEN** de pagina wordt weergegeven
- **THEN** staat de vetgedrukte tekst "Read" binnen het element met de klasse `md-color--accent`

#### Scenario: Onbekende of ongesloten kleur blijft tekst
- **GIVEN** een pagina met de tekst `{pink}x{/pink}` en `{danger}open`
- **WHEN** de pagina wordt weergegeven
- **THEN** staan beide teksten ongewijzigd als gewone tekst op de pagina en bevat de weergave geen element met een `md-color`-klasse

#### Scenario: Kleur in code blijft tekst
- **GIVEN** een pagina met de tekst `` `{danger}x{/danger}` ``
- **WHEN** de pagina wordt weergegeven
- **THEN** toont de pagina de code `{danger}x{/danger}` letterlijk
