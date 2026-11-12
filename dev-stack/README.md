# Dev-stack (Keycloak · Mailpit)

Lokale ontwikkelomgeving voor **Doctrina** (zie `../docs/Doctrina-voorstel.md` §3.3) en een algemene demo-realm.
Hoe Doctrina hierop inlogt (ook via **Entra ID**): **[docs/doctrina-integratie.md](../docs/doctrina-integratie.md)**.

| Dienst | Adres | Inlog |
|---|---|---|
| Keycloak admin | http://localhost:8080 | `admin` / `admin` |
| Realm `doctrina` | http://localhost:8080/realms/doctrina | testgebruikers hieronder |
| Realm `demo` | http://localhost:8080/realms/demo | `alice`, `bob`, `carol` (wachtwoord = naam) |
| Mailpit (mail van Keycloak) | http://localhost:8025 · SMTP `localhost:1025` | – |

Versies: Keycloak `quay.io/keycloak/keycloak:26.6.1`, `axllent/mailpit:v1.28` — aan te passen via `.env`.

De **database** zit niet in de dev-stack: Doctrina gebruikt lokaal en in de tests SQL Server LocalDB
(`(localdb)\MSSQLLocalDB`, Windows-authenticatie, geen wachtwoord). LocalDB wordt met Visual Studio meegeïnstalleerd;
controleer met `sqllocaldb info MSSQLLocalDB`.

## Starten / stoppen

    docker compose up -d          # starten
    docker compose logs -f keycloak
    docker compose down           # stoppen (data blijft bewaard)
    docker compose down -v        # alles resetten (realms worden opnieuw geïmporteerd)

## Testgebruikers

Beide realms: **250 nepgebruikers** (175 studenten, 75 medewerkers), wachtwoord `test123`, bijv. `femke.coenen`.
Attributen: e-mail, voor/achternaam, `affiliation`, `department` (korte code) + `departmentName`, `studentNumber`+`program` of `employeeNumber`+`jobTitle`.

| `department` | Naam | Wie |
|---|---|---|
| FHML | Faculty of Health, Medicine and Life Sciences | studenten + medewerkers |
| SBE | School of Business and Economics | studenten + medewerkers |
| FdR | Faculteit der Rechtsgeleerdheid | studenten + medewerkers |
| FPN | Faculty of Psychology and Neuroscience | studenten + medewerkers |
| FASoS | Faculty of Arts and Social Sciences | studenten + medewerkers |
| FSE | Faculty of Science and Engineering | studenten + medewerkers |
| ICTS · FS · SSC | ICT Service Centre · Facility Services · Student Services Centre | alleen medewerkers |
| UB · FIN · MUO | University Library · Finance · Maastricht University Office | alleen medewerkers |

Ongeveer 30% van de medewerkers zit bij een dienst (`$serviceShare` in de generator). Indeling aanpassen: `$faculties` / `$services` bovenin `scripts\generate-realm.ps1`.

| Realm `doctrina` | Wachtwoord | Rollen |
|---|---|---|
| `beheerder` | beheerder | admin, employee |
| `docent` | docent | employee |
| `student` | student | student |
| 250 nepgebruikers | test123 | `employee` of `student` |

In `doctrina` volgen rollen en `affiliation` de OOAPI-waarden (`employee`/`student`); in `demo` heten ze `medewerker`/`student`.

**Nummers en `personId` (alleen realm `doctrina`):** studenten `I6` + 6 cijfers (bv. `I6834263`), medewerkers `P7` + 6 cijfers
(bv. `P7066509`), uniek per gebruiker. Hetzelfde nummer staat in `studentNumber`/`employeeNumber` én in `personId`;
alle drie gaan als claim mee (`student_number`/`employee_number`/`personId`). Vaste accounts: `beheerder` P7000001,
`docent` P7000002, `student` I6000001. In realm `demo` blijft het oude formaat (`i6…`, `E…`), zonder `personId`.

Opnieuw genereren (ander aantal / verhouding):

    powershell -ExecutionPolicy Bypass -File scripts\generate-realm.ps1 -Count 250 -StudentRatio 0.8
    docker compose down -v; docker compose up -d

## Entra ID koppelen

    copy .env.example .env      # ENTRA_TENANT_ID / ENTRA_CLIENT_ID / ENTRA_CLIENT_SECRET invullen
    powershell -ExecutionPolicy Bypass -File scripts\configure-entra.ps1              # realm doctrina
    powershell -ExecutionPolicy Bypass -File scripts\configure-entra.ps1 -Realm demo  # of demo

Redirect URI in de Entra app-registratie: `http://localhost:8080/realms/<realm>/broker/entra/endpoint`.
Stappen en app-rollen: zie [docs/doctrina-integratie.md](../docs/doctrina-integratie.md#entra-koppelen-lokaal-via-keycloak).
Na `docker compose down -v` het script opnieuw draaien.

> In een universiteitstenant mag je vaak zelf geen app registreren (of is admin consent nodig).
> Een Microsoft 365 developer-tenant of eigen Azure-tenant werkt ook.

## Inhoud

    docker-compose.yml
    realm/                      demo-realm.json, doctrina-realm.json (gegenereerd)
    scripts/generate-realm.ps1  genereert de realms
    scripts/configure-entra.ps1 Entra als identity provider
    samples/Doctrina.AuthCheck  .NET 10 referentie-app (OIDC + API) + test-e2e.sh
    ../docs/                    Doctrina-voorstel, integratie (in de repo-root)

> Alleen voor lokaal gebruik: `start-dev`, HTTP, vaste secrets en simpele wachtwoorden.
