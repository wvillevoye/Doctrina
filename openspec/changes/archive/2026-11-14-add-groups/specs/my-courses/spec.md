# Spec-delta

## ADDED Requirements

### Requirement: "My courses" linkt naar de pagina van elke uitvoering
Elke uitvoering in "My courses" SHALL een link zijn naar haar pagina `/courses/{offering}`.

#### Scenario: Link naar de uitvoering
- **GIVEN** een gekoppelde persoon met een actieve inschrijving in "BIO101-2026-P1"
- **WHEN** de gebruiker "My courses" opent
- **THEN** linkt de rij van "BIO101-2026-P1" naar `/courses/{offering}` van die uitvoering
