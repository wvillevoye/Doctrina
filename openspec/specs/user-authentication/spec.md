# user-authentication Specification

## Purpose

Laat mensen inloggen bij Doctrina via elke standaardconforme OpenID Connect-provider (Keycloak, Microsoft Entra ID,
SURFconext), uitsluitend gekozen via configuratie, en authenticeert systemen en gebruikers die de API aanroepen met
bearer-tokens.

## Requirements

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

### Requirement: De identiteit gebruikt een provider-neutraal claimcontract
De ingelogde identiteit SHALL claimnamen precies zo gebruiken als de provider ze uitgeeft, zonder hernoemen bij
binnenkomst. De weergavenaam SHALL uit de claim `name` komen en de rollen uit de claim `roles`, die meerdere waarden kan
bevatten.

#### Scenario: Rollen en naam uit het token
- **GIVEN** een ID-token met `name` "Sanne Student" en `roles` ["student"]
- **WHEN** de gebruiker inlogt
- **THEN** de weergavenaam van de gebruiker is "Sanne Student" en de gebruiker heeft rol `student`

#### Scenario: Meerdere rollen
- **GIVEN** een ID-token met `roles` ["admin", "employee"]
- **WHEN** de gebruiker inlogt
- **THEN** de gebruiker heeft zowel rol `admin` als rol `employee`

### Requirement: Het inlogscherm van de provider kan worden overgeslagen
Als `Authentication:Oidc:IdpHint` is ingesteld, SHALL het inlogverzoek die waarde als identity-provider-hint meesturen,
zodat een makelende provider (zoals Keycloak die Entra ID makelt) de gebruiker direct doorstuurt. Als de waarde leeg
is, SHALL NOT er een hint worden meegestuurd.

#### Scenario: Hint wordt meegestuurd als die is ingesteld
- **GIVEN** `Authentication:Oidc:IdpHint` is "entra"
- **WHEN** een gebruiker begint met inloggen
- **THEN** het autorisatieverzoek bevat `kc_idp_hint=entra`

#### Scenario: Geen hint als die niet is ingesteld
- **GIVEN** `Authentication:Oidc:IdpHint` is leeg
- **WHEN** een gebruiker begint met inloggen
- **THEN** het autorisatieverzoek bevat geen parameter `kc_idp_hint`

### Requirement: Gebruikers loggen uit bij Doctrina en bij de provider
Uitloggen SHALL de Doctrina-sessie beëindigen en SHALL de gebruiker doorsturen naar het end-session-endpoint van de
provider, waarna de gebruiker terugkomt op de startpagina.

#### Scenario: Uitloggen beëindigt beide sessies
- **GIVEN** een ingelogde gebruiker
- **WHEN** de gebruiker kiest voor "Sign out"
- **THEN** de sessiecookie van Doctrina is verwijderd, de gebruiker wordt naar het end-session-endpoint van de provider gestuurd en komt anoniem terug op de startpagina

### Requirement: De API authenticeert bearer-tokens
Verzoeken aan `/api/v1` SHALL geauthenticeerd worden met een bearer-access-token dat is uitgegeven door de ingestelde
authority en de ingestelde API-audience bevat. API-verzoeken SHALL NOT met de sessiecookie geauthenticeerd worden en
SHALL NOT naar een inlogpagina worden doorgestuurd.

#### Scenario: Geldig token van een systeem
- **GIVEN** een access token, verkregen met client credentials, met audience `doctrina-api`
- **WHEN** het als bearer-token naar een `/api/v1`-endpoint wordt gestuurd dat authenticatie vereist
- **THEN** het verzoek is geauthenticeerd als die client

#### Scenario: Ontbrekend token
- **WHEN** een `/api/v1`-endpoint dat authenticatie vereist zonder token wordt aangeroepen
- **THEN** de responsestatus is 401 met een ProblemDetails-body en zonder doorsturen

#### Scenario: Token voor een andere audience
- **GIVEN** een geldig access token waarvan de audience `doctrina-api` niet bevat
- **WHEN** het naar een `/api/v1`-endpoint wordt gestuurd
- **THEN** de responsestatus is 401

#### Scenario: Sessiecookie wordt niet door de API geaccepteerd
- **GIVEN** een gebruiker die is ingelogd in de webapplicatie
- **WHEN** de browser een `/api/v1`-endpoint aanroept met alleen de sessiecookie
- **THEN** de responsestatus is 401

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
