# Spec-delta

## Purpose
Een ingelogde gebruiker koppelen aan de persoon uit het SIS, zonder dat de identity provider bron van personen wordt.

## ADDED Requirements

### Requirement: Een login wordt via personId aan een persoon gekoppeld
Als Doctrina de persoon van een ingelogde gebruiker nodig heeft (bv. voor "My courses"), SHALL Doctrina eerst de persoon
zoeken die al aan de issuer en de claim `oid` van de login gekoppeld is. Is die er niet, dan SHALL Doctrina de actieve
persoon zoeken waarvan de `ExternalId` gelijk is aan de claim `personId`, en bij een treffer de issuer en `oid` bij die
persoon vastleggen. Volgende verzoeken met dezelfde issuer en `oid` SHALL bij dezelfde persoon uitkomen, ook als
`personId` dan ontbreekt.

#### Scenario: Eerste gebruik door een geïmporteerde persoon
- **GIVEN** een geïmporteerde persoon "I6000001" en een gebruiker die inlogt met `personId` "I6000001"
- **WHEN** de gebruiker "My courses" opent
- **THEN** is de gebruiker gekoppeld aan persoon "I6000001" en zijn issuer en `oid` bij die persoon vastgelegd

#### Scenario: Volgende login zonder personId
- **GIVEN** een gebruiker die eerder via `personId` aan persoon "I6000001" is gekoppeld
- **WHEN** dezelfde gebruiker opnieuw inlogt met dezelfde issuer en `oid`, maar zonder claim `personId`, en "My courses" opent
- **THEN** is de gebruiker weer gekoppeld aan persoon "I6000001"

### Requirement: Inloggen maakt nooit een persoon aan
Doctrina SHALL NOT een persoon aanmaken of wijzigen op basis van een login, behalve het vastleggen van issuer en `oid`.
Een gebruiker zonder bijbehorende actieve persoon SHALL gewoon ingelogd blijven, maar SHALL op pagina's die een persoon
nodig hebben de melding "We can't find your details in Doctrina yet" krijgen.

#### Scenario: Login zonder bijbehorende persoon
- **GIVEN** geen persoon met `ExternalId` "I6999999"
- **WHEN** een gebruiker met `personId` "I6999999" inlogt en "My courses" opent
- **THEN** is de gebruiker ingelogd, is er geen persoon aangemaakt en toont de pagina de melding "We can't find your details in Doctrina yet"

#### Scenario: Persoon op te verwijderen
- **GIVEN** een persoon "I6000001" met status `ToBeDeleted`
- **WHEN** een gebruiker met `personId` "I6000001" inlogt en "My courses" opent
- **THEN** wordt de gebruiker niet aan die persoon gekoppeld en toont de pagina de melding "We can't find your details in Doctrina yet"
