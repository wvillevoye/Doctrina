# Ontwerp

## Context

Motivatie: zie `proposal.md`; gedrag: spec-delta `user-authentication`. Inloggen loopt via de OpenID Connect-handler van
ASP.NET Core (`AddOpenIdConnect`, `src/Doctrina.Infrastructure/Authentication`). Die haalt de providerconfiguratie
(`.well-known/openid-configuration`) pas op bij de eerste challenge of sign-out, en bewaart haar daarna (standaard 12 uur,
bij een mislukte verversing blijft de oude gelden). Is de provider dan onbereikbaar, dan gooit
`ConfigurationManager` een `InvalidOperationException` (IDX20803) met een `HttpRequestException` erin. De
uitzonderingsafhandeling van de host (`UseExceptionHandler` → `/fout`) maakt daar een 500 met de algemene foutpagina
van.

Die fout ontstaat op drie plekken:
1. **Challenge**: `GET /inloggen`, en automatisch bij een beveiligde pagina (`AuthorizationMiddleware`).
2. **Callback** (`/signin-oidc`): het token-endpoint is onbereikbaar. De handler maakt daar een
   `AuthenticationFailureException` van, met de netwerkfout erin.
3. **Sign-out**: `POST /uitloggen`, als de configuratie nog nooit is opgehaald (bv. de host is herstart terwijl de
   provider plat lag).

## Doelen / Geen doelen

**Doelen:**
- Eén plek die alle drie de paden afvangt, zonder iets te veranderen aan het normale inloggen.
- De meldingspagina zelf mag de provider nooit nodig hebben.

**Geen doelen:**
- Onderscheid tussen soorten storingen (DNS, time-out, TLS) in de tekst voor de gebruiker; dat hoort alleen in de log.

## Besluiten

### D1 — Een middleware rond de pipeline, niet de events van de OIDC-handler
Een middleware in `Doctrina.Infrastructure.Authentication` vangt de exceptie op. Hij staat ná `UseExceptionHandler` en
vóór `UseAuthentication`/`UseAuthorization`, zodat challenge, callback en sign-out er alle drie doorheen gaan.
Herkent hij een onbereikbare provider, dan stuurt hij door naar de meldingspagina. Anders gooit hij de exceptie opnieuw,
zodat de gewone foutafhandeling blijft gelden (scenario "Andere fouten blijven de foutpagina geven").
*Alternatieven:*
- `OnRemoteFailure`: dekt alleen de callback.
- Een try/catch in het endpoint `/inloggen`: mist de automatische challenge van beveiligde pagina's en de sign-out.
- De events `OnRedirectToIdentityProvider*`: die komen pas ná het ophalen van de configuratie en worden dus nooit
  bereikt.

### D2 — Herkennen: een netwerkfout binnen een exceptie van de authenticatie
Een aparte, unit-geteste klasse beslist. Het geval is "inlogdienst onbereikbaar" als in de keten van
(inner) excepties beide gelden:
- er zit een netwerkfout in: `HttpRequestException`, `SocketException`, `IOException` of een
  `TaskCanceledException`/`TimeoutException` door een time-out;
- een exceptie in de keten komt uit `Microsoft.IdentityModel.*` of `Microsoft.AspNetCore.Authentication*`
  (`Exception.Source`).

Zo leidt een netwerkfout van iets anders (bv. een module die een externe API aanroept) niet tot de inlogmelding.
*Alternatief:* herkennen aan de IDX-codes in de foutmelding. Afgewezen: die teksten zijn geen contract en verschillen
per versie.

### D3 — Doorsturen naar een pagina in de host; de pagina zet 503
De middleware stuurt met 302 door naar `/inloggen-onbereikbaar?returnUrl=…` (bij uitloggen `&uitgelogd=1`). Het pad
staat als constante naast `SignInPath`/`SignOutPath` in `AuthenticationEndpoints`. De pagina staat in de host (Razor,
`Dc*`-componenten), is anoniem toegankelijk en zet zelf status 503 met `Retry-After: 60`, net zoals `/geen-toegang` 403
zet. Is het antwoord al begonnen (`Response.HasStarted`), dan gooit de middleware de exceptie opnieuw.
*Alternatief:* de pagina direct vanuit de middleware renderen met status 503. Afgewezen: dat vraagt om Razor-componenten
te renderen buiten de router; de redirect is eenvoudiger en de pagina is los te testen.

### D4 — Terugkeeradres
"Try again" gaat naar `/inloggen?returnUrl=<terugkeeradres>`. Het terugkeeradres komt bij `/inloggen` uit de
query `returnUrl` en bij een beveiligde pagina uit pad + query van het verzoek. Bij de callback is het `/`, omdat het
oorspronkelijke adres in de versleutelde `state` zit en een terugval naar de startpagina daar volstaat. Elk adres gaat
door `AuthenticationEndpoints.LocalPathOrRoot` (geen open redirect).

### D5 — Uitloggen beëindigt de Doctrina-sessie toch
Mislukt `POST /uitloggen` op de provider, dan heeft de cookie-handler zijn `Set-Cookie` al gezet, maar dat antwoord
wordt weggegooid. De middleware wist daarom het antwoord, roept zelf `SignOutAsync` aan voor het cookieschema en stuurt
dan door met `uitgelogd=1`. De pagina zegt dan dat de gebruiker bij Doctrina is uitgelogd, maar mogelijk nog niet bij de
inlogdienst, en raadt aan de browser te sluiten op een gedeelde computer.
In de praktijk komt dit alleen voor als de configuratie sinds het opstarten nooit is opgehaald. Met een opgehaalde
configuratie stuurt Doctrina de browser gewoon naar het end-session-endpoint, en ligt een storing daar bij de provider.

### D6 — Loggen zonder persoonsgegevens
Eén waarschuwing per geval (`LogWarning`, met EventId), met:
- de actie: inloggen, terugkeer of uitloggen;
- de authority (host en pad) en het pad van het verzoek, zonder query (de callback bevat `code` en `state`);
- de trace-id;
- het type van de netwerkfout.

Geen gebruikersnaam en geen claims. De exceptie gaat als parameter mee voor de stacktrace; haar meldingen bevatten
alleen adressen van de provider.

### D7 — Testaanpak
De testfactory krijgt een variant met een **schakelbare provider**:
- **Configuratie**: een `IConfigurationManager<OpenIdConnectConfiguration>` die doorgeeft aan óf de bestaande statische
  configuratie, óf een echte `ConfigurationManager` met een `HttpDocumentRetriever` waarvan de `HttpClient` een
  `HttpRequestException` gooit. Zo komt de exceptie echt uit `Microsoft.IdentityModel`, zoals in productie (D2).
- **Callback**: de backchannel (`HttpClient` naar het token-endpoint) kan op dezelfde manier falen.

Zo dekt één host alle scenario's: eerst inloggen met een werkende provider, daarna de provider "uitzetten" en
uitloggen. De logscenario's gebruiken een vastleggende `ILoggerProvider`, zoals de bestaande test
`OnverwachteFoutenLekkenGeenDetails`.

### D8 — Teksten
De UI is Engels (taalafspraak 2026-11-12). De pagina is daarmee Engels, terwijl de rest van de UI pas in een eigen
change wordt omgezet. De paden (`/inloggen-onbereikbaar`, `uitgelogd=1`) volgen de bestaande routes `/inloggen` en
`/uitloggen` en gaan mee in die omzetting.

| Onderdeel | Tekst |
|---|---|
| Paginatitel (`DcPageHeader`) | Sign-in unavailable |
| Melding (`DcAlert`, Warning) | The sign-in service can't be reached right now. This is usually temporary; please try again in a minute. |
| Knop (`DcButton`) | Try again |
| Melding bij `uitgelogd=1` | You have been signed out of Doctrina, but you may still be signed in at the sign-in service. On a shared computer, close your browser. |

## Risico's / Afwegingen

- [Een netwerkfout uit de authenticatie die geen onbereikbare provider is, bv. een TLS-fout door een verkeerd
  certificaat] → valt ook onder "onbereikbaar". Voor de gebruiker is dat terecht: het is tijdelijk of een
  beheerprobleem. De log noemt het type, zodat beheer het verschil ziet.
- [Een 302 gevolgd door 503 in plaats van direct 503] → browsers en monitoring zien de 503 op de meldingspagina; voor
  een gebruikersscherm is dat genoeg.
- [Een gebruiker die op een beveiligde pagina begon en via de callback terugkomt, landt na "opnieuw proberen" op `/`
  (D4)] → acceptabel bij een storing; het adres staat niet onversleuteld ter beschikking.

## Migratieplan

Geen data, configuratie of API. Terugdraaien = de commit terugdraaien.
