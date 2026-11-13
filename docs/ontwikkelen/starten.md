# Starten met Doctrina

Deze handleiding brengt je van een verse checkout naar een draaiende Doctrina waarop je kunt inloggen als `student`.
Alles werkt op Windows; commando's zijn voor PowerShell, tenzij er Git Bash staat.

## 1. Vereisten (eenmalig)

| Wat | Controle |
|---|---|
| .NET SDK 10.0.403 of nieuwer 10.0.x (zie `global.json`) | `dotnet --version` |
| Docker Desktop (voor Keycloak en Mailpit) | `docker version` |
| SQL Server LocalDB (komt mee met Visual Studio of SQL Server Express) | `sqllocaldb info MSSQLLocalDB` |
| Git Bash (alleen voor de rooktest) | `bash --version` |

Daarna, in de root van de repo:

```powershell
dotnet tool restore                      # dotnet-ef in de juiste versie (dotnet-tools.json)
dotnet dev-certs https --trust           # ontwikkelcertificaat voor https://localhost:5001
copy src\Doctrina.Host\appsettings.Secrets.example.json src\Doctrina.Host\appsettings.Secrets.json
```

`appsettings.Secrets.json` staat in `.gitignore` en bevat het client secret van `doctrina-web` uit de dev-stack.
Zonder dit bestand start de host niet: je krijgt dan een melding dat `Authentication:Oidc:ClientSecret` ontbreekt.

## 2. Dev-stack starten

```powershell
cd dev-stack; docker compose up -d; cd ..
```

Dit start Keycloak (http://localhost:8080) en Mailpit (http://localhost:8025). Wacht tot Keycloak gezond is:
`docker ps` toont dan `keycloak … (healthy)`. Meer over de dev-stack: [`dev-stack/README.md`](../../dev-stack/README.md).

De database draait niet in Docker maar op LocalDB. De host maakt de database `Doctrina` en de tabellen zelf aan bij het
starten, zodra er een ingeschakelde module met migraties is (zie [database.md](database.md)). Zolang er nog geen
productmodules zijn, bestaat de database niet en meldt `/health/ready` `"database": "Unhealthy"` (503); dat is
verwacht. Wil je dat nu al groen zien, maak de lege database dan eenmalig aan:
`sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "CREATE DATABASE Doctrina"`.

## 3. Doctrina starten

```powershell
dotnet run --project src/Doctrina.Host --launch-profile https
```

Open https://localhost:5001. De omgeving is `Development`; de instellingen komen uit
`src/Doctrina.Host/appsettings.Development.json` plus je `appsettings.Secrets.json`.

De eerste build compileert ook de stylesheet (`AspNetCore.SassCompiler`, zonder Node.js). Rechtsboven in de header
kies je het thema: licht, donker of systeem (standaard). De keuze wordt onthouden in een cookie. Alle componenten
staan in de stijlgids op https://localhost:5001/style-guide (alleen in `Development`); meer in
[vormgeving.md](vormgeving.md).

## 4. Inloggen

Klik op **Sign in**. Je komt bij Keycloak; log in met een testaccount:

| Account | Wachtwoord | Rollen | Wat je ziet |
|---|---|---|---|
| `student` | `student` | `student` | **My courses** (na het importeren van de testdata, zie §5) |
| `docent` | `docent` | `employee` | **My courses**, als lecturer |
| `beheerder` | `beheerder` | `admin`, `employee` | alle permissies van ingeschakelde modules (`admin` → `*`), ook **Academic** en **Import** |
| bv. `femke.coenen` | `test123` | wisselend | een van de 250 nepgebruikers |

Na het inloggen ga je naar **My profile** (`/profile`): naam, persoonsnummer (`personId`, bv. `I6000001`), de issuer
(`http://localhost:8080/realms/doctrina`), je rollen en je permissies, met per rol waar ze vandaan komen. **Sign out**
beëindigt de sessie bij Doctrina én bij Keycloak.

Welke rol welke permissies geeft, staat in `Authorization:RolePermissions` in `appsettings.json` (en voor de module
Academic in `appsettings.Development.json`).

## 5. Academic test data

In Development the **Academic** module is switched on (`Modules:Enabled` in `appsettings.Development.json`); its
schema `academic` is created on start-up. The database starts empty. The dev-stack has CSV files with the same people
as in Keycloak (same `personId`), plus organizations, sessions, courses, offerings and enrolments:

1. Sign in as `beheerder` and open **Academic** → **Import** (`/academic/import`).
2. Import the files from `dev-stack/data/` one by one, with source `SIS`, in this order (later files refer to earlier ones):
   `organizations.csv`, `academic-sessions.csv`, `courses.csv`, `course-offerings.csv`, `persons.csv`, `associations.csv`.
   Choose the matching type each time. Every row should say `created`; importing a file again says `unchanged`.
3. Sign out and sign in as `student`: **My courses** (`/my-courses`) shows three courses. As `docent` you see two, as lecturer.

Under **Academic** (`/academic`) the admin pages list every type with search and a detail page per record. They are
read-only: changes go through the source (CSV now, REST later).

The CSV files are generated together with the realm by `dev-stack/scripts/generate-realm.ps1` (see
`dev-stack/README.md`). The format: one file per type, a header row with OOAPI field names, `,` or `;`, UTF-8 with or
without BOM, dates as `YYYY-MM-DD`, and an optional `status` column (`active` or `toBeDeleted`).

### Groups

With the test data imported, try the **Groups** module (also switched on in Development):

1. Sign in as `docent` and open **My courses** → **Cell biology** (`/courses/{offering}`). The page shows the course,
   the period and your role, and a section **My groups** with the button **Manage groups**.
2. On **Manage groups** (`/courses/{offering}/groups`): create a group set (for example "Project teams", visible to
   students), then **Distribute randomly** over a number of groups, or add groups and put students in them one by one.
   A student is in at most one group per set; a maximum group size is optional.
3. Sign in as `student`: **My courses** → **Cell biology** shows your group and its members under **My groups**,
   for sets that are visible to students.

Only lecturers and coordinators of the offering, and users with `Groups.Manage` (role `admin`), can manage groups.

## 6. De API proberen

De API (`/api/v1`) accepteert alleen bearer-tokens. Een systeemtoken (client credentials) haal je zo op:

```powershell
$token = (Invoke-RestMethod -Method Post http://localhost:8080/realms/doctrina/protocol/openid-connect/token `
  -Body @{ grant_type = "client_credentials"; client_id = "doctrina-sis-sync"; client_secret = "doctrina-sis-sync-secret" }).access_token
Invoke-RestMethod https://localhost:5001/api/v1/me -Headers @{ Authorization = "Bearer $token" }
```

De documentatie van de API staat in Development op https://localhost:5001/scalar (OpenAPI-document:
`/openapi/v1.json`). Health: `/health/live` en `/health/ready`.

## 7. Inloggen via Microsoft Entra ID

Keycloak kan Entra ID als identity provider doorgeven. Eenmalig inrichten: zie
[doctrina-integratie.md](../doctrina-integratie.md#entra-koppelen-lokaal-via-keycloak) en draai daarna
`dev-stack\scripts\configure-entra.ps1`.

Daarna overslaan van het Keycloak-scherm, zodat je direct naar Microsoft gaat: zet in
`appsettings.Secrets.json` (of in een omgevingsvariabele `Authentication__Oidc__IdpHint`):

```json
{ "Authentication": { "Oidc": { "IdpHint": "entra" } } }
```

Laat de waarde leeg om weer met de testaccounts in te loggen. Doctrina zelf verandert niet: alleen de configuratie.

## 8. Tests draaien

```powershell
dotnet test                              # alle tests: geen Keycloak en geen Docker nodig (nagebootste provider, LocalDB)
```

De integratietests draaien in de omgeving `Test` (`appsettings.Test.json`) en maken per test een eigen
LocalDB-database aan die daarna weer verdwijnt.

Rooktest tegen de échte Keycloak (Git Bash, terwijl Doctrina draait):

```bash
bash dev-stack/scripts/smoke-login.sh
```

Die logt in als `student` en `beheerder`, controleert `/profile` en roept de API aan met een systeemtoken.

## Problemen

| Melding of symptoom | Oplossing |
|---|---|
| `Authentication:Oidc:ClientSecret ontbreekt` | stap 1: `appsettings.Secrets.example.json` kopiëren naar `appsettings.Secrets.json` |
| Pagina "Sign-in unavailable" (`/sign-in-unavailable`) bij inloggen of uitloggen | dev-stack draait niet of Keycloak is nog niet gezond: `docker ps`, zo nodig `cd dev-stack; docker compose up -d`, daarna "Try again" |
| Browser waarschuwt voor het certificaat | `dotnet dev-certs https --trust` en de browser opnieuw starten |
| `/health/ready` geeft 503 met `"database": "Unhealthy"` | database `Doctrina` bestaat nog niet (zie stap 2) of LocalDB is gestopt |
| Fout over `(localdb)\MSSQLLocalDB` | `sqllocaldb start MSSQLLocalDB` (zie [database.md](database.md#problemen)) |
| Na een rolwijziging in Keycloak nog oude rechten | opnieuw inloggen; een open pagina werkt de rechten uiterlijk na 30 minuten bij |
