# Doctrina ↔ dev-stack: inloggen (Keycloak → Entra ID)

Hoort bij `docs/Doctrina-voorstel.md` §2 (OIDC configureerbaar), §3.3 (dev-stack, staat in `dev-stack/`) en §6.6-D (client credentials).

## Architectuur

```
                    ┌──────────── lokaal (deze dev-stack) ────────────┐
Doctrina.Host  ──OIDC (code+PKCE)──>  Keycloak realm "doctrina"  ──OIDC broker──>  Microsoft Entra ID
 https://localhost:5001              http://localhost:8080          (knop "Inloggen met Microsoft"
                                      + 250 testgebruikers            of direct via IdpHint=entra)
Systeem (SIS) ──client credentials──>  Keycloak  ──> Bearer-token ──> Doctrina /api/v1
```

Doctrina praat **alleen standaard OIDC**. Keycloak levert tokens in **Entra-vorm**, dus dezelfde code werkt tegen:

| Omgeving | Authority | Opmerking |
|---|---|---|
| Lokaal, testgebruikers | `http://localhost:8080/realms/doctrina` | geen internet/tenant nodig |
| Lokaal, via Entra | idem + `IdpHint: "entra"` | Keycloak stuurt direct door naar Microsoft |
| Productie, Entra direct | `https://login.microsoftonline.com/<tenant>/v2.0` | zie onderaan |
| SURFconext | `https://connect.surfconext.nl` | rollen dan via eigen mapping (geen `roles`-claim) |

## Clients in realm `doctrina`

| Client | Type | Gebruik | Secret (alleen dev) |
|---|---|---|---|
| `doctrina-web` | confidential, code + PKCE | Blazor-app (gebruikers) | `doctrina-web-secret` |
| `doctrina-api` | resource | audience (`aud`) van access tokens voor `/api/v1` | – |
| `doctrina-sis-sync` | client credentials | voorbeeld-systeemkoppeling (SIS/bulk-import) | `doctrina-sis-sync-secret` |
| `doctrina-dev-cli` | public, password grant | tokens halen in scripts/tests | – |

Redirect URI's van `doctrina-web`: `https://localhost:5001/signin-oidc` (en `http://localhost:5000/...`).
Post-logout: `/signout-callback-oidc` en `/`. **Doctrina.Host draait dus op poort 5001/5000** (launchSettings).

## Claimcontract (Keycloak ≡ Entra)

| Claim | Inhoud | Entra-equivalent |
|---|---|---|
| `sub` | stabiele id per IdP | `sub` (pairwise per app) |
| `oid` | gebruikers-id | `oid` (object-id) |
| `name`, `given_name`, `family_name`, `email`, `preferred_username` | profiel | idem (`email` als optionele claim) |
| `roles` | **platte lijst**: `admin`, `employee`, `student` | app roles (value) |
| `roles` (client credentials) | `doctrina.persons.read`, `…write`, `doctrina.courses.read`, `…write`, `doctrina.import` | app roles met *allowedMemberTypes = Application* |
| `aud` | `doctrina-api` (access token) | `api://<doctrina-api>` of client-id |
| `affiliation` | `employee` / `student` (OOAPI-waarden) | – (afleiden uit `roles`) |
| `department` | korte code faculteit/dienst: `FHML`, `SBE`, `FdR`, `FPN`, `FASoS`, `FSE`, `ICTS`, `FS`, `SSC`, `UB`, `FIN`, `MUO` | `department` (optioneel; waarde zoals in Entra) |
| `department_name` | volledige naam, bv. *Faculty of Arts and Social Sciences* | – |
| `personId` | persoonsnummer van de instelling: studenten `I6` + 6 cijfers (bv. `I6365806`), medewerkers `P7` + 6 cijfers (bv. `P7002809`); naam volgt OOAPI `personId` | `employeeId` / extension-attribuut (via optionele claim) |
| `employee_number`, `student_number` | zelfde nummer, apart per soort | `employeeId` (extension) |
| `groups` | groepsnamen | groep-object-id's (anders!) |

Afspraken voor Doctrina:
- `MapInboundClaims = false`, `NameClaimType = "name"`, `RoleClaimType = "roles"`.
- Autorisatie alleen op `roles` — niet op `groups` (die verschillen per IdP).
- Koppel een login aan een `Person` op **issuer + `oid`**, met als eerste koppeling **`personId` ↔ `ExternalId`** uit het SIS (anders e-mail). Een `oid` uit Keycloak is **niet** dezelfde als die uit Entra; `personId` wel, mits Entra het nummer als claim meegeeft.

## API: scopes, permissions and source

Systems call `/api/v1` with a client-credentials token. Their scopes (in `roles`) map to Doctrina permissions in
`Authorization:RolePermissions` (change `add-rest-api`); in Development:

| Scope | Permissions | Allows |
|---|---|---|
| `doctrina.persons.read`, `doctrina.courses.read` | `Academic.Read`, `Groups.Read` | `GET` on the Academic types and on `groups` |
| `doctrina.persons.write`, `doctrina.courses.write` | `Academic.Write` | `POST`, `PUT …/by-external-id/{key}`, `PATCH`, `DELETE`, `POST …/batch` |

What a system writes gets the **source** that `Api:Sources:{client}` (the client id from claim `azp`) configures,
otherwise `SIS`; a `source` in the body is ignored. A system can only change or delete records of its own source
(otherwise 403, `Academic.Record.OtherSource`). Upserts match on the key of the source system plus that source. Batches
are limited per client (`Api:BatchesPerMinute`, default 60; above it 429).

## API: OOAPI v5 (read-only)

Systems that speak OOAPI v5 read the core model under `/ooapi/v5` (change `add-ooapi-interop`, module `Ooapi`, switched
on with `Ooapi` in `Modules:Enabled`). It is read-only; writing stays on `/api/v1`. The same bearer-only rules apply as
for `/api/v1`: a client-credentials token, never the session cookie. The permission is `Ooapi.Read`; in Development the
scopes `doctrina.persons.read` and `doctrina.courses.read` carry it, because the persons and associations hold personal
data (names, e-mail addresses). Without a token the answer is 401, without the permission 403.

| Path | |
|---|---|
| `GET /ooapi/v5` | service data: `contactEmail`, `specification`, `documentation` |
| `GET /ooapi/v5/organizations`, `/academic-sessions`, `/courses`, `/persons` (+ `/{id}`) | lists and one record; list filter `primaryCode` |
| `GET /ooapi/v5/offerings/{id}` and `…/courses/{id}/offerings`, `…/academic-sessions/{id}/offerings`, `…/organizations/{id}/offerings` | a course offering is an offering of type `course` |
| `GET /ooapi/v5/associations/{id}`, `…/offerings/{id}/associations` (filter `role`), `…/persons/{id}/associations` | an enrolment; roles `student`, `lecturer`, `teaching assistant`, `coordinator` |

As in the standard there is no top-level `/offerings` or `/associations`. Lists are paged with `pageNumber` (from 1) and
`pageSize` (10, 20, 50, 100 or 250; default 10), and answer with `pageSize`, `pageNumber`, `hasPreviousPage`,
`hasNextPage`, `totalPages` and `items`. Errors are `application/problem+json` with `status` and `title`. A record's
`ExternalId` is its `primaryCode` (`codeType` `identifier`); the Doctrina id is the OOAPI id. Only active records show.

OOAPI requires some fields Doctrina does not store. They are derived (`state` is `associated`; a person's
`affiliations` come from the roles, `guest` without any; `activeEnrollment` is true for a student; a missing
description is the name) or set per installation under `Ooapi:`: `Language` (language tag of all texts, default `nl`),
`TeachingLanguage` (default `nld`), `Level` (default `undefined`), `ResultExpected` (default `false`) and
`ContactEmail`, `Specification`, `Documentation` for `GET /ooapi/v5`. A person without an e-mail address has no `mail`.

```bash
# token of a system with scope doctrina.persons.read, then:
curl -H "Authorization: Bearer $TOKEN" "https://localhost:5001/ooapi/v5/courses?pageSize=20&pageNumber=2"
```

## API: import

Larger deliveries go through an import job (change `add-bulk-import`), with scope `doctrina.import` (→ `Academic.Import`):

| Endpoint | |
|---|---|
| `POST /api/v1/imports` | multipart: `file` (CSV or zip) and `type` for a CSV; `?dryRun=true` only shows what would happen. 202 with the job and a `Location` |
| `GET /api/v1/imports/{id}` | `status` (`queued`, `running`, `completed`, `failed`), counts, message |
| `GET /api/v1/imports/{id}/report` | result per row as JSON (paged), or `?format=csv` |
| `GET /api/v1/imports/templates/{type}.csv` | empty CSV with the columns of a type |

A zip holds one CSV per type; the type comes from `manifest.csv` (`file,type`) or the file name, and the files are
processed in a fixed order (organizations → academic-sessions → courses → course-offerings → persons → associations), so
records may refer to records in an earlier file of the same zip. The source is the client's (`Api:Sources:{azp}`), as
with REST. A problem with a file as a whole (missing column, unknown file, too large) fails the job without saving
anything; a bad row only fails that row. Uploads are rate-limited like batches.

## `appsettings.Development.json` en `appsettings.Secrets.json` voor Doctrina.Host

```json
{
  "Authentication": {
    "Oidc": {
      "Authority": "http://localhost:8080/realms/doctrina",
      "ClientId": "doctrina-web",
      "RequireHttpsMetadata": false,
      "Scopes": [ "openid", "profile", "email" ],
      "IdpHint": ""
    },
    "Api": { "Audience": "doctrina-api" }
  },
  "ConnectionStrings": {
    "Doctrina": "Server=(localdb)\MSSQLLocalDB;Database=Doctrina;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Het client secret staat **niet** in dit bestand maar in het niet-gecommitte `appsettings.Secrets.json`
(kopieer `appsettings.Secrets.example.json`):

```json
{ "Authentication": { "Oidc": { "ClientSecret": "doctrina-web-secret" } } }
```

`IdpHint: "entra"` → het Keycloak-scherm wordt overgeslagen, gebruikers gaan direct naar Microsoft.
Laat leeg om met de testgebruikers in te loggen.

De werkende referentie-implementatie staat in `dev-stack/samples/Doctrina.AuthCheck/Program.cs` (cookie + OIDC voor de UI,
JwtBearer voor `/api/v1`, policies op `roles`). Dat is bewust geen Doctrina-code: die komt via de OpenSpec-change
`add-modular-foundation` in de Doctrina-repo.

## Testen

```powershell
# app starten
cd dev-stack\samples\Doctrina.AuthCheck; dotnet run --launch-profile https
# browser: https://localhost:5001  →  inloggen als student/student, beheerder/beheerder of femke.coenen/test123
```
```bash
# end-to-end (Git Bash), terwijl de app draait
bash dev-stack/samples/Doctrina.AuthCheck/test-e2e.sh
```
```powershell
# systeemtoken (client credentials)
curl.exe -d grant_type=client_credentials -d client_id=doctrina-sis-sync -d client_secret=doctrina-sis-sync-secret `
  http://localhost:8080/realms/doctrina/protocol/openid-connect/token
```

## Als de inlogdienst onbereikbaar is

Kan Doctrina de provider niet bereiken (geen verbinding, time-out of andere netwerkfout bij het ophalen van
`.well-known/openid-configuration` of bij het token-endpoint), dan ziet de gebruiker geen algemene foutpagina maar:

- **Inloggen** (knop, beveiligde pagina of terugkeer van de provider): een redirect naar `/sign-in-unavailable`. Die
  pagina "Sign-in unavailable" geeft status **503** met `Retry-After: 60` en een knop "Try again" die opnieuw inlogt met
  het oorspronkelijke terugkeeradres. Na een mislukte terugkeer is dat de startpagina.
- **Uitloggen**: de Doctrina-sessie wordt toch beëindigd. De pagina meldt dat de gebruiker bij Doctrina is uitgelogd,
  maar mogelijk nog niet bij de inlogdienst.

Per geval schrijft Doctrina één waarschuwing (categorie
`Doctrina.Infrastructure.Authentication.IdentityProviderUnavailableMiddleware`, EventId 1001):

```
Identity provider idp.example.org/realms/doctrina can't be reached during sign-in of /sign-in (trace 00-…-01, SocketException).
```

De actie is `sign-in`, `callback` of `sign-out`. De trace-id staat ook op de pagina, zodat een melding van een gebruiker
terug te vinden is. De regel bevat geen query (dus geen `code` of `state`), geen claims en geen gebruikersnaam.
Andere fouten blijven de foutpagina `/error` met status 500 geven.

## Entra koppelen (lokaal, via Keycloak)

1. Entra → App registrations → *New registration* → "Doctrina dev (Keycloak)".
   Redirect URI (Web): `http://localhost:8080/realms/doctrina/broker/entra/endpoint`.
2. *Certificates & secrets* → client secret aanmaken.
3. *Token configuration* → optionele claim `email` (ID-token), evt. `department`… via *Add optional claim*.
4. *App roles* → aanmaken met **value** `admin`, `employee`, `student` (*Users/Groups*) en toewijzen via
   Enterprise applications → Users and groups.
5. In de map `Keycloak` (naast de checkout): `.env` vullen en `scripts\configure-entra.ps1` draaien (standaard realm `doctrina`).

## Later: Entra direct (zonder Keycloak)

Twee app-registraties in Entra, met dezelfde namen voor rollen als hierboven:

| Registratie | Instellingen |
|---|---|
| **Doctrina API** | *Expose an API* → Application ID URI `api://doctrina-api`; *App roles* (Application): `doctrina.persons.read`, … |
| **Doctrina web** | redirect `https://<host>/signin-oidc`; *App roles* (Users): `admin`, `employee`, `student`; API-permissie op Doctrina API |

Config: `Authority = https://login.microsoftonline.com/<tenant>/v2.0`, `Api:Audience = api://doctrina-api`, `IdpHint` leeg.
Systemen vragen een token met `scope=api://doctrina-api/.default`. De applicatiecode verandert niet.
