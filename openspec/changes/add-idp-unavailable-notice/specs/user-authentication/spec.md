# Spec-delta

## ADDED Requirements

### Requirement: Een onbereikbare inlogdienst geeft een duidelijke melding
Kan Doctrina de provider bij inloggen niet bereiken (geen verbinding, time-out of netwerkfout), dan SHALL de gebruiker
de pagina "Sign-in unavailable" krijgen in plaats van de algemene foutpagina. Die pagina SHALL status 503 met
`Retry-After` geven, SHALL een tijdelijke storing van de inlogdienst melden zonder technische details, en SHALL opnieuw
laten proberen met terugkeer naar de oorspronkelijke pagina. Andere fouten SHALL de algemene foutpagina blijven geven.

#### Scenario: Inloggen terwijl de inlogdienst onbereikbaar is
- **GIVEN** een provider die niet bereikbaar is
- **WHEN** een anonieme bezoeker op `/profiel?tab=2` inloggen kiest
- **THEN** krijgt de bezoeker de pagina "Sign-in unavailable" met status 503 en `Retry-After`, en gaat "Try again" naar inloggen met `/profiel?tab=2` als terugkeeradres

#### Scenario: Beveiligde pagina openen terwijl de inlogdienst onbereikbaar is
- **GIVEN** een provider die niet bereikbaar is
- **WHEN** een anonieme bezoeker een pagina opent waarvoor inloggen nodig is
- **THEN** krijgt de bezoeker de pagina "Sign-in unavailable" in plaats van de algemene foutpagina

#### Scenario: Terugkeer van de provider mislukt door een netwerkfout
- **GIVEN** een gebruiker die zich bij de provider heeft geauthenticeerd, terwijl het token-endpoint niet bereikbaar is
- **WHEN** de provider de gebruiker terugstuurt naar Doctrina
- **THEN** krijgt de gebruiker de pagina "Sign-in unavailable" en is de gebruiker niet ingelogd

#### Scenario: Andere fouten blijven de foutpagina geven
- **WHEN** een pagina een onverwachte fout geeft die niets met de inlogdienst te maken heeft
- **THEN** krijgt de gebruiker de algemene foutpagina met status 500

### Requirement: Uitloggen beëindigt de sessie ook als de inlogdienst onbereikbaar is
Kan Doctrina de provider bij uitloggen niet bereiken, dan SHALL de Doctrina-sessie toch worden beëindigd en SHALL de
gebruiker een melding krijgen dat de sessie bij de inlogdienst mogelijk nog bestaat.

#### Scenario: Uitloggen terwijl de inlogdienst onbereikbaar is
- **GIVEN** een ingelogde gebruiker en een provider die niet meer bereikbaar is
- **WHEN** de gebruiker kiest voor uitloggen
- **THEN** is de sessiecookie van Doctrina verwijderd en zegt de pagina dat de gebruiker bij Doctrina is uitgelogd, maar mogelijk nog niet bij de inlogdienst

### Requirement: Een onbereikbare inlogdienst wordt gelogd zonder persoonsgegevens
Elke inlog- of uitlogpoging die mislukt omdat de provider onbereikbaar is, SHALL als waarschuwing worden gelogd met het
adres van de provider en de trace-id, en SHALL NOT persoonsgegevens of inloggegevens uit het verzoek bevatten.

#### Scenario: De oorzaak staat in de log zonder persoonsgegevens
- **GIVEN** een provider die niet bereikbaar is
- **WHEN** een inlogpoging daardoor mislukt
- **THEN** bevat de log een waarschuwing met het adres van de provider en de trace-id, en geen naam, e-mailadres, persoonsnummer, autorisatiecode of `state`
