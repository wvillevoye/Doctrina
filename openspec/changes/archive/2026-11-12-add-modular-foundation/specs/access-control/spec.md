# Spec-delta

## Purpose

Bepaalt wat ingelogde gebruikers en aanroepende systemen mogen, met permissies die modules declareren en een
instelbare koppeling van rollen uit de identity provider aan die permissies, op dezelfde manier afgedwongen in pagina's
en API-endpoints.

## ADDED Requirements

### Requirement: Modules declareren permissies
Elke module SHALL de permissies declareren die zij gebruikt, elk met een unieke naam met de modulenaam als prefix
(bijvoorbeeld `Import.Execute`) en een omschrijving. De host SHALL de permissies van alle ingeschakelde modules kennen.

#### Scenario: Gedeclareerde permissies zijn bekend
- **GIVEN** ingeschakelde module `Sample` declareert `Sample.Read` en `Sample.Manage`
- **WHEN** de host start
- **THEN** beide permissies staan met hun omschrijving op de profielpagina van een beheerder

#### Scenario: Dubbele permissienaam laat het opstarten mislukken
- **GIVEN** twee ingeschakelde modules declareren allebei permissie `Sample.Read`
- **WHEN** de host start
- **THEN** het opstarten mislukt met een fout die de dubbele permissie noemt

### Requirement: Rollen worden via configuratie aan permissies gekoppeld
De instelling `Authorization:RolePermissions` SHALL waarden van de `roles`-claim aan permissies koppelen. Een aanroeper
SHALL de vereniging van de permissies van al zijn rollen hebben. De waarde `*` SHALL alle permissies van ingeschakelde
modules geven.

#### Scenario: Permissies uit meerdere rollen worden samengevoegd
- **GIVEN** rol `employee` is gekoppeld aan `Sample.Read` en rol `admin` aan `Sample.Manage`
- **WHEN** een gebruiker met rollen ["employee", "admin"] inlogt
- **THEN** de gebruiker heeft `Sample.Read` en `Sample.Manage`

#### Scenario: Jokerteken geeft elke permissie
- **GIVEN** rol `admin` is gekoppeld aan `*`
- **WHEN** een gebruiker met rol `admin` inlogt
- **THEN** de gebruiker heeft elke permissie die ingeschakelde modules declareren

#### Scenario: Niet-gekoppelde rol geeft niets
- **WHEN** een gebruiker inlogt wiens enige rol niet in de koppeling voorkomt
- **THEN** de gebruiker is ingelogd maar heeft geen permissies

#### Scenario: Koppeling naar een onbekende permissie laat het opstarten mislukken
- **GIVEN** de koppeling verwijst naar permissie `Unknown.Do`, die geen enkele ingeschakelde module declareert
- **WHEN** de host start
- **THEN** het opstarten mislukt met een fout die `Unknown.Do` noemt en de rol die ernaar verwijst

### Requirement: Systeempermissies gebruiken dezelfde koppeling
Voor aanroepers die met client credentials zijn geauthenticeerd SHALL de waarden in hun `roles`-claim (bijvoorbeeld
`doctrina.persons.read`) via dezelfde instelling `Authorization:RolePermissions` aan permissies gekoppeld worden.

#### Scenario: Clientpermissie geeft toegang tot de API
- **GIVEN** `doctrina.import` is gekoppeld aan `Import.Execute` en een endpoint vereist `Import.Execute`
- **WHEN** een systeem waarvan het token `roles` ["doctrina.import"] heeft dat endpoint aanroept
- **THEN** het verzoek is geautoriseerd

### Requirement: Pagina's en endpoints vereisen permissies
Pagina's en API-endpoints SHALL te beveiligen zijn door een vereiste permissie te noemen. Aanroepers zonder die
permissie SHALL geweigerd worden: ingelogde gebruikers met een pagina "Geen toegang", API-aanroepers met status 403 en
een ProblemDetails-body.

#### Scenario: Pagina geweigerd zonder permissie
- **GIVEN** een pagina die `Sample.Manage` vereist
- **WHEN** een ingelogde gebruiker zonder `Sample.Manage` haar opent
- **THEN** de pagina "Geen toegang" wordt getoond met status 403 en de gebruiker blijft ingelogd

#### Scenario: API geweigerd zonder permissie
- **GIVEN** een endpoint dat `Sample.Manage` vereist
- **WHEN** een aanroeper met een geldig token maar zonder `Sample.Manage` het aanroept
- **THEN** de responsestatus is 403 met een ProblemDetails-body waarvan `code` gelijk is aan `Auth.Forbidden`

#### Scenario: Pagina toegestaan met permissie
- **GIVEN** een pagina die `Sample.Manage` vereist
- **WHEN** een ingelogde gebruiker met `Sample.Manage` haar opent
- **THEN** de pagina wordt getoond

### Requirement: Endpoints zijn standaard beveiligd
Elk `/api/v1`-endpoint SHALL een geauthenticeerde aanroeper vereisen, tenzij het expliciet als anoniem is gemarkeerd.

#### Scenario: Ongemarkeerd endpoint vereist authenticatie
- **GIVEN** een module-endpoint onder `/api/v1` zonder expliciete autorisatiemetadata
- **WHEN** het zonder token wordt aangeroepen
- **THEN** de responsestatus is 401
