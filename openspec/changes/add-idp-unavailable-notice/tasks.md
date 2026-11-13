# Taken

## 1. Herkennen en testopstelling

- [ ] 1.1 Maak in `Doctrina.Infrastructure/Authentication` de herkenning van een onbereikbare provider (design D2: netwerkfout in de keten plus een exceptie uit `Microsoft.IdentityModel.*` of `Microsoft.AspNetCore.Authentication*`); controleer met unittests in `tests/Doctrina.Host.IntegrationTests/UserAuthentication`: echte IDX20803-keten (via een `ConfigurationManager` met falende `HttpClient`) → ja; `AuthenticationFailureException` met `HttpRequestException` → ja; time-out → ja; `HttpRequestException` zonder authenticatiebron → nee; andere exceptie → nee
- [ ] 1.2 Voeg aan `DoctrinaHostFactory`/`Doctrina.TestKit` een schakelbare provider toe (design D7: doorgevende `IConfigurationManager` en een backchannel die kan falen); controleer met een test dat inloggen werkt met de provider aan en dat `/inloggen` met de provider uit nu nog de foutpagina (500) geeft, zodat de volgende taken echt iets veranderen

## 2. Middleware en meldingspagina

- [ ] 2.1 Voeg `AuthenticationEndpoints.UnavailablePath = "/inloggen-onbereikbaar"` en de middleware toe (design D1, D3, D4, D6), geregistreerd in `Program.cs` na `UseExceptionHandler` en vóór `UseAuthentication`; controleer met tests `EenOnbereikbareInlogdienstGeeftEenDuidelijkeMelding.InloggenTerwijlDeInlogdienstOnbereikbaarIs` (302 naar de pagina met `returnUrl=%2Fprofiel%3Ftab%3D2`), `…BeveiligdePaginaOpenenTerwijlDeInlogdienstOnbereikbaarIs` en `…AndereFoutenBlijvenDeFoutpaginaGeven` (`/sample/fout` blijft 500)
- [ ] 2.2 Maak de pagina `/inloggen-onbereikbaar` in de host (`DcPageHeader` "Inloggen lukt nu niet", `DcAlert` Warning, `DcButton` "Opnieuw proberen" naar `/inloggen?returnUrl=…` via `LocalPathOrRoot`, trace-id; status 503 en `Retry-After: 60`; anoniem toegankelijk); controleer met een test dat de pagina 503 + `Retry-After` geeft, geen exceptietekst bevat, dat de knop het terugkeeradres bevat en dat een extern `returnUrl` wordt vervangen door `/`; de architectuurtest voor hostpagina's blijft groen
- [ ] 2.3 Vang de mislukte callback af (token-endpoint onbereikbaar); controleer met test `…TerugkeerVanDeProviderMisluktDoorEenNetwerkfout` (meldingspagina, geen sessiecookie gezet)
- [ ] 2.4 Beëindig bij een mislukte sign-out toch de Doctrina-sessie (design D5) en toon de variant "uitgelogd" van de pagina; controleer met test `UitloggenBeeindigtDeSessieOokAlsDeInlogdienstOnbereikbaarIs.UitloggenTerwijlDeInlogdienstOnbereikbaarIs` (eerst inloggen met de provider aan, dan uit; de cookie `doctrina.session` wordt verwijderd, de pagina zegt "uitgelogd bij Doctrina" en daarna is `/profiel` niet meer toegankelijk zonder inloggen)
- [ ] 2.5 Controleer de logregel met test `EenOnbereikbareInlogdienstWordtGelogdZonderPersoonsgegevens.DeOorzaakStaatInDeLogZonderPersoonsgegevens` (een vastleggende `ILoggerProvider`: één waarschuwing met authority en trace-id; geen naam, e-mail of persoonsnummer van `StubUser.Student`; geen `code=` of `state=` uit de callback)

## 3. Documentatie en afronding

- [ ] 3.1 Beschrijf het gedrag in `docs/ontwikkelen/starten.md` (rij in "Problemen": "Inloggen lukt nu niet" → dev-stack starten) en in `docs/doctrina-integratie.md` (wat beheer ziet als de provider onbereikbaar is: pagina, 503, logregel); controleer dat de genoemde paden en de logtekst overeenkomen met de code
- [ ] 3.2 Draai `dotnet test` voor de hele solution; controleer dat alles slaagt en dat elk scenario uit de spec-delta een test heeft (lijst in de PR-beschrijving)
- [ ] 3.3 Handmatig met de dev-stack: Keycloak stoppen (`docker stop keycloak`), in de browser op "Inloggen" klikken en de pagina zien, Keycloak starten en "Opnieuw proberen" gebruiken; controleer dat je daarna ingelogd terugkomt op de oorspronkelijke pagina

## Vervolg in de workflow

- Beoordeel de change vóór `/opsx:apply`.
- Bouwen na de merge van `add-design-system`, of op een branch die op `feature/add-design-system` voortbouwt.
- Na afronding: commit op een branch, push, PR; na de merge `/opsx:archive`.
