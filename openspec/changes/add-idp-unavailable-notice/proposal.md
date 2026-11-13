# Voorstel

## Why

Is de inlogdienst (Keycloak, Entra ID of SURFconext) niet bereikbaar, dan krijgt iemand die op "Inloggen" klikt nu de
algemene foutpagina "Er ging iets mis" met een trace-code. Dat suggereert een fout in Doctrina, terwijl het een
tijdelijke storing elders is waar de gebruiker zelf niets aan kan doen. Lokaal gebeurt het al zodra de dev-stack niet
draait. Bij een instelling gebeurt het tijdens onderhoud of een storing van de provider. Een eigen melding met
"opnieuw proberen" is duidelijker en zorgt voor minder meldingen bij de servicedesk.

Deze change komt niet uit de roadmap (`docs/Doctrina-voorstel.md` §8). Ze is een aanvulling op `user-authentication`
na `add-design-system`, waarvan ze de componenten gebruikt.

## What Changes

- Is de inlogdienst onbereikbaar bij **inloggen**, dan ziet de gebruiker de pagina "Inloggen lukt nu niet" in plaats
  van de foutpagina. Dat geldt voor de knop "Inloggen" én voor het openen van een pagina waarvoor inloggen nodig is.
  De pagina geeft status 503 met `Retry-After` en een knop "Opnieuw proberen" die terugkeert naar de oorspronkelijke
  pagina.
- Hetzelfde geldt als de **terugkeer van de provider** mislukt, omdat Doctrina het token-endpoint niet kan bereiken.
- Is de inlogdienst onbereikbaar bij **uitloggen**, dan beëindigt Doctrina toch de eigen sessie. De pagina zegt dat de
  gebruiker bij Doctrina is uitgelogd, maar mogelijk nog niet bij de inlogdienst, met het advies de browser te sluiten
  op een gedeelde computer.
- De oorzaak komt als waarschuwing in de log, met het adres van de provider en de trace-id, zonder persoonsgegevens.
- Andere onverwachte fouten blijven de gewone foutpagina krijgen.

## Out of scope

- De API (`/api/v1`, bearer-tokens): kan Doctrina de sleutels van de provider niet ophalen, dan blijft het antwoord
  401 zoals nu. Een 503 met ProblemDetails voor systemen is een mogelijke vervolgchange.
- Een health check voor de inlogdienst in `/health/ready`, automatisch opnieuw proberen, of een statusbanner op elke
  pagina.
- Andere fouten van de provider dan onbereikbaarheid, zoals een geweigerde login of een verkeerd client secret. Die
  blijven de foutpagina geven.

## Capabilities

### New Capabilities
<!-- geen -->

### Modified Capabilities
- `user-authentication`: nieuwe requirement over het gedrag als de inlogdienst onbereikbaar is, bij inloggen,
  terugkeren van de provider en uitloggen.

## Impact

- `src/Doctrina.Infrastructure/Authentication`: een middleware die netwerkfouten van de inlogdienst herkent en
  doorstuurt naar de meldingspagina, plus het pad van die pagina naast `/inloggen` en `/uitloggen`.
- `src/Doctrina.Host`: de pagina `/inloggen-onbereikbaar` (met `Dc*`-componenten) en de registratie van de middleware
  in `Program.cs`.
- Tests in `tests/Doctrina.Host.IntegrationTests/UserAuthentication`, met een nagebootste onbereikbare provider.
- Geen nieuwe packages, geen wijziging aan API, database of configuratie.
- Afhankelijk van `add-design-system` (branch `feature/add-design-system`): bouwen na de merge daarvan, of op een branch
  die daarop voortbouwt.
