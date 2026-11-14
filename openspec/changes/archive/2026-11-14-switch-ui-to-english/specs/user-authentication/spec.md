# Spec-delta

## MODIFIED Requirements

### Requirement: Gebruikers loggen in via OpenID Connect
De webapplicatie SHALL gebruikers laten inloggen met de OpenID Connect authorization code flow met PKCE tegen de
provider die in `Authentication:Oidc` is ingesteld (authority, client-id, client secret). Wisselen van provider SHALL
alleen configuratiewijzigingen vereisen.

#### Scenario: Anonieme gebruiker wordt naar de provider gestuurd
- **WHEN** een anonieme gebruiker een pagina opent waarvoor inloggen nodig is
- **THEN** de gebruiker wordt doorgestuurd naar het authorization-endpoint van de ingestelde provider met een PKCE-code challenge

#### Scenario: Na succesvol inloggen terug naar de gevraagde pagina
- **GIVEN** een gebruiker die op `/profile` begon en zich bij de provider heeft geauthenticeerd
- **WHEN** de provider terugstuurt met een authorization code
- **THEN** de gebruiker is ingelogd en komt uit op `/profile`

#### Scenario: Pagina's zonder inlogeis blijven openbaar
- **WHEN** een anonieme gebruiker de startpagina opent
- **THEN** de pagina wordt getoond zonder doorsturen naar de provider

### Requirement: Gebruikers loggen uit bij Doctrina en bij de provider
Uitloggen SHALL de Doctrina-sessie beëindigen en SHALL de gebruiker doorsturen naar het end-session-endpoint van de
provider, waarna de gebruiker terugkomt op de startpagina.

#### Scenario: Uitloggen beëindigt beide sessies
- **GIVEN** een ingelogde gebruiker
- **WHEN** de gebruiker kiest voor "Sign out"
- **THEN** de sessiecookie van Doctrina is verwijderd, de gebruiker wordt naar het end-session-endpoint van de provider gestuurd en komt anoniem terug op de startpagina

### Requirement: De ingelogde identiteit is zichtbaar voor de gebruiker
De applicatie SHALL ingelogde gebruikers een pagina `/profile` bieden met hun weergavenaam, persoonsnummer (uit de
claim `personId`), de issuer van de identity provider, rollen en effectieve permissies, en SHALL NOT tokens tonen.

#### Scenario: Profielpagina toont de identiteit
- **GIVEN** een gebruiker die is ingelogd met rollen ["employee"] en `personId` "P7002809"
- **WHEN** de gebruiker `/profile` opent
- **THEN** de pagina toont de weergavenaam, persoonsnummer "P7002809", de issuer, de rol `employee` en de daarvan afgeleide permissies, en geen tokenwaarden

#### Scenario: Profielpagina zonder persoonsnummer
- **GIVEN** een ingelogde gebruiker wiens token geen claim `personId` bevat
- **WHEN** de gebruiker `/profile` opent
- **THEN** de pagina toont "–" als persoonsnummer

### Requirement: Een onbereikbare inlogdienst geeft een duidelijke melding
Kan Doctrina de provider bij inloggen niet bereiken (geen verbinding, time-out of netwerkfout), dan SHALL de gebruiker
de pagina "Sign-in unavailable" krijgen in plaats van de algemene foutpagina. Die pagina SHALL status 503 met
`Retry-After` geven, SHALL een tijdelijke storing van de inlogdienst melden zonder technische details, en SHALL opnieuw
laten proberen met terugkeer naar de oorspronkelijke pagina. Andere fouten SHALL de algemene foutpagina blijven geven.

#### Scenario: Inloggen terwijl de inlogdienst onbereikbaar is
- **GIVEN** een provider die niet bereikbaar is
- **WHEN** een anonieme bezoeker op `/profile?tab=2` inloggen kiest
- **THEN** krijgt de bezoeker de pagina "Sign-in unavailable" met status 503 en `Retry-After`, en gaat "Try again" naar inloggen met `/profile?tab=2` als terugkeeradres

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
