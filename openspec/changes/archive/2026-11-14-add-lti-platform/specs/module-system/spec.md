## ADDED Requirements

### Requirement: Modules kunnen browser-endpoints leveren
Een module SHALL naast haar `/api/v1`-endpoints endpoints kunnen leveren die met de inlogcookie van de browser werken.
De host SHALL die endpoints van ingeschakelde modules koppelen onder het voorvoegsel `/{modulenaam in kleine letters}`
en SHALL standaard inloggen eisen. Een endpoint SHALL zichzelf expliciet anoniem kunnen maken. Een uitgeschakelde module
SHALL geen browser-endpoints bijdragen.

#### Scenario: Browser-endpoint van een ingeschakelde module is bereikbaar
- **GIVEN** module `Sample` staat in `Modules:Enabled` en levert een browser-endpoint `GET /ping`
- **WHEN** een ingelogde gebruiker `/sample/ping` opent
- **THEN** het endpoint antwoordt met 200

#### Scenario: Browser-endpoint van een uitgeschakelde module geeft 404
- **GIVEN** module `Sample` staat niet in `Modules:Enabled`
- **WHEN** een ingelogde gebruiker `/sample/ping` opent
- **THEN** het antwoord is 404

#### Scenario: Browser-endpoint vereist standaard inloggen
- **GIVEN** module `Sample` levert een browser-endpoint zonder verdere instellingen
- **WHEN** een niet-ingelogde bezoeker `/sample/ping` opent
- **THEN** de bezoeker wordt naar het inloggen gestuurd en het endpoint voert niets uit

#### Scenario: Browser-endpoint kan expliciet anoniem zijn
- **GIVEN** module `Sample` levert een browser-endpoint dat zichzelf anoniem maakt
- **WHEN** een niet-ingelogde bezoeker dat endpoint opent
- **THEN** het endpoint antwoordt zonder in te loggen
