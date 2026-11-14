# Dev-stack (Doctrina's part)

Keycloak and Mailpit no longer live here. They run in a shared stack outside this repository, in the folder `Keycloak`
next to the folder of this checkout (`..\..\Keycloak` from the repository root), so other applications can use the
same Keycloak. Start it with:

    docker compose -f ..\..\Keycloak\docker-compose.yml up -d      # from the repository root

That stack reads Doctrina's realm from `dev-stack/realm/doctrina-realm.json` in this repository. Starting, stopping,
resetting and Entra ID: see the README in the `Keycloak` folder. How Doctrina signs in:
**[docs/doctrina-integratie.md](../docs/doctrina-integratie.md)**.

The **database** is not in Docker: Doctrina uses SQL Server LocalDB locally and in the tests
(`(localdb)\MSSQLLocalDB`, Windows authentication, no password). Check with `sqllocaldb info MSSQLLocalDB`.

## Test users (realm `doctrina`)

| Account | Password | Roles |
|---|---|---|
| `beheerder` | beheerder | admin, employee |
| `docent` | docent | employee |
| `student` | student | student |
| 250 generated users, e.g. `femke.coenen` | test123 | `employee` or `student` |

Roles and `affiliation` follow the OOAPI values (`employee`/`student`). Attributes: e-mail, first and last name,
`affiliation`, `department` (short code) + `departmentName`, `studentNumber`+`program` or `employeeNumber`+`jobTitle`.

| `department` | Name | Who |
|---|---|---|
| FHML | Faculty of Health, Medicine and Life Sciences | students + employees |
| SBE | School of Business and Economics | students + employees |
| FdR | Faculteit der Rechtsgeleerdheid | students + employees |
| FPN | Faculty of Psychology and Neuroscience | students + employees |
| FASoS | Faculty of Arts and Social Sciences | students + employees |
| FSE | Faculty of Science and Engineering | students + employees |
| ICTS · FS · SSC | ICT Service Centre · Facility Services · Student Services Centre | employees only |
| UB · FIN · MUO | University Library · Finance · Maastricht University Office | employees only |

About 30% of the employees work at a service (`$serviceShare` in the generator). Change the list: `$faculties` /
`$services` at the top of `scripts\generate-realm.ps1`.

**Numbers and `personId`:** students `I6` + 6 digits (e.g. `I6834263`), employees `P7` + 6 digits (e.g. `P7066509`),
unique per user. The same number is in `studentNumber`/`employeeNumber` and in `personId`; all three are sent as a
claim (`student_number`/`employee_number`/`personId`). Fixed accounts: `beheerder` P7000001, `docent` P7000002,
`student` I6000001.

Generate again (other count / ratio), then reset the shared Keycloak so it imports the new file:

    powershell -ExecutionPolicy Bypass -File scripts\generate-realm.ps1 -Count 250 -StudentRatio 0.8
    docker compose -f ..\..\Keycloak\docker-compose.yml down -v      # from the repository root
    docker compose -f ..\..\Keycloak\docker-compose.yml up -d

The generator can still write the `demo` realm (`-Realm demo`), but the shared Keycloak reads its own copy: copy
`realm\demo-realm.json` to `..\..\Keycloak\realm\` afterwards.

### Academic CSV files

The script also writes `data/*.csv` in Doctrina's import format (change `add-academic-core`): the same people with the
same `personId`, the UM faculties, academic year 2026-2027 with periods 1 and 2, eleven courses with one offering
each, and enrolments. Fixed accounts: `student` is enrolled in three offerings, `docent` is lecturer in two,
`beheerder` is coordinator of one. Generated students get two offerings, preferably of their own faculty.

The script also writes `data/academic-testdata.zip` with all six. Import that zip in Doctrina at `/academic/import`
(as `beheerder`, source `SIS`); the order comes from the file names. Steps:
[docs/ontwikkelen/starten.md](../docs/ontwikkelen/starten.md) §5. After regenerating, import again: unchanged rows say
`unchanged`; records that are no longer in a file stay (use `status` = `toBeDeleted` to hide them).

## Contents

    realm/doctrina-realm.json   realm for the shared Keycloak (generated)
    data/                       Academic CSV files (generated, same people as the realm)
    scripts/generate-realm.ps1  generates the realm and data/*.csv
    scripts/smoke-login.sh      smoke test: sign in to Doctrina.Host through Keycloak
    samples/Doctrina.AuthCheck  .NET 10 reference app (OIDC + API) + test-e2e.sh
