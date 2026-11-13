# Spec-delta

## MODIFIED Requirements

### Requirement: Pagina's en endpoints vereisen permissies
Pagina's en API-endpoints SHALL te beveiligen zijn door een vereiste permissie te noemen. Aanroepers zonder die
permissie SHALL geweigerd worden: ingelogde gebruikers met een pagina "Access denied", API-aanroepers met status 403 en
een ProblemDetails-body.

#### Scenario: Pagina geweigerd zonder permissie
- **GIVEN** een pagina die `Sample.Manage` vereist
- **WHEN** een ingelogde gebruiker zonder `Sample.Manage` haar opent
- **THEN** de pagina "Access denied" wordt getoond met status 403 en de gebruiker blijft ingelogd

#### Scenario: API geweigerd zonder permissie
- **GIVEN** een endpoint dat `Sample.Manage` vereist
- **WHEN** een aanroeper met een geldig token maar zonder `Sample.Manage` het aanroept
- **THEN** de responsestatus is 403 met een ProblemDetails-body waarvan `code` gelijk is aan `Auth.Forbidden`

#### Scenario: Pagina toegestaan met permissie
- **GIVEN** een pagina die `Sample.Manage` vereist
- **WHEN** een ingelogde gebruiker met `Sample.Manage` haar opent
- **THEN** de pagina wordt getoond
