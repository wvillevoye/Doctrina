## ADDED Requirements

### Requirement: Pagina's kunnen doorhalen en takenlijsten gebruiken
De weergave van Markdown SHALL `~~tekst~~` als doorgehaalde tekst tonen en regels met `- [ ]` of `- [x]` als takenlijst met
uitgeschakelde selectievakjes. Hiervoor SHALL geen HTML uit de tekst worden overgenomen.

#### Scenario: Doorhalen wordt weergegeven
- **GIVEN** een pagina met de tekst `~~old~~`
- **WHEN** de pagina wordt weergegeven
- **THEN** staat "old" in een `del`-element

#### Scenario: Takenlijst wordt weergegeven
- **GIVEN** een pagina met de regels `- [x] Done` en `- [ ] Todo`
- **WHEN** de pagina wordt weergegeven
- **THEN** bevat de weergave twee uitgeschakelde selectievakjes, het eerste aangevinkt

#### Scenario: HTML blijft uit naast de nieuwe opmaak
- **GIVEN** een pagina met de tekst `~~x~~ <script>alert(1)</script>`
- **WHEN** de pagina wordt weergegeven
- **THEN** bevat de weergave geen `script`-element
