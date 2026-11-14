# Design

## Context

Zie `proposal.md` voor de aanleiding en de scope. Wat er al is en waar deze change op leunt:

- Modulesysteem (`IModule`, `KnownModules`, `Modules:Enabled`), `ModuleDbContext` met eigen schema en migraties,
  `ExchangeableEntity` (`ExternalId`, `Source`, `Status`, `DateLastModified`) en de `DateLastModifiedInterceptor`, die
  alleen bij `Added`/`Modified` stempelt.
- `Result`/`Error` en fouttitels uit `.resx` per module (`AddErrorTitles`), met Engelse titels in de UI.
- Permissies per module, gekoppeld aan rollen via `Authorization:RolePermissions`; menu-items filteren op
  `RequiredPermission`.
- `Doctrina.UI` met `DcTable`, `DcPageHeader`, `DcCard`, `DcAlert`, `DcResult`, `DcForm`.
- Tests op LocalDB via `Doctrina.TestKit` en `DoctrinaHostFactory`; de testmodules in `Doctrina.TestKit` laten het
  patroon voor een module zien.
- Er is nog geen productmodule; `src/Modules/README.md` en `docs/ontwikkelen/modules.md` beschrijven de opzet.

## Goals / Non-Goals

**Goals:**
- Een eerste productmodule die het patroon zet voor de volgende: indeling, invoer via één validator en upsert-service,
  alleen-lezen schermen en tests.
- Invoer die `add-api-and-import` later zonder herbouw kan hergebruiken.

**Non-Goals:**
- Prestaties voor grote bestanden (staging, `SqlBulkCopy`, `MERGE`): pas in `add-api-and-import`. De grens van 10.000
  regels houdt de synchrone import binnen redelijke tijd.
- Meertaligheid van namen (OOAPI `LanguageTypedString`): één naam per record (zie D2).
- Een `.Contracts`-API voor andere modules: komt met de eerste module die hem nodig heeft (`add-groups`). Het
  `.Contracts`-project wordt wel aangemaakt, leeg op de foutcodes na.

## Decisions

### D1: Indeling en testprojecten

Module volgens `docs/Doctrina-voorstel.md` §3.2: `src/Modules/Academic/Doctrina.Modules.Academic` (Domain, Application,
Infrastructure, Components) en `.Contracts`. Er komt **één** testproject `tests/Modules/Doctrina.Modules.Academic.Tests`
met unit-tests (validators, CSV-lezer) én tests tegen LocalDB (upsert, verwijzingen). Pagina's en de koppeling met
het inloggen worden getest in `Doctrina.Host.IntegrationTests`, waar de host en de stub-provider al klaarstaan.

Afwijking van het voorstel (§3.1/§3.3: aparte `.Tests` en `.IntegrationTests`, Testcontainers): LocalDB is al de
afspraak (AGENTS.md), en een tweede project per module levert nu alleen extra opzet op. Splitsen kan als de testtijd
daarom vraagt.

### D2: Datamodel

Velden volgen OOAPI v5; `name` is één string (geen `LanguageTypedString`), omdat Doctrina nu één taal heeft voor
inhoud uit het SIS. In de CSV heet de `ExternalId`-kolom zoals de OOAPI-id van het type.

| Type | CSV-sleutel (= `ExternalId`) | Velden | Verwijst naar |
|---|---|---|---|
| `Organization` | `organizationId` | `name`*, `shortName`, `organizationType`* (`root`, `institution`, `faculty`, `department`, `branch`, `academy`, `school`) | `parentOrganizationId` (optioneel) |
| `AcademicSession` | `academicSessionId` | `name`*, `academicSessionType`* (`academic year`, `semester`, `trimester`, `quarter`, `testing period`, `period`), `startDate`*, `endDate`* | `parentAcademicSessionId` (optioneel) |
| `Course` | `courseId` | `name`*, `abbreviation`, `description`, `studyLoad` (ECTS, decimaal ≥ 0) | `organizationId` (optioneel) |
| `CourseOffering` | `offeringId` | `name`*, `startDate`, `endDate` | `courseId`*, `academicSessionId`* |
| `Person` | `personId` | `givenName`*, `surnamePrefix`, `surname`*, `displayName`, `mail` | – |
| `Association` | `associationId` | `role`* | `personId`*, `offeringId`* |

`*` = verplicht. Elke CSV heeft daarnaast een optionele kolom `status` (`active`, standaard, of `toBeDeleted`).
Verwijzingen worden opgeslagen als `Guid`-foreign keys, opgezocht via `ExternalId` binnen dezelfde `Source` (D4).
Unieke index op (`ExternalId`, `Source`) per tabel (bestaande afspraak) en op (`PersonId`, `CourseOfferingId`, `Role`)
voor inschrijvingen.

Rollen (`student`, `lecturer`, `teaching-assistant`, `coordinator`) zijn de OOAPI-rollen voor een offering die
overeenkomen met §4.3 van het voorstel (student, docent, tutor/aide, beheerder); in de UI "Student", "Lecturer",
"Teaching assistant", "Coordinator". Afwijking: §4.3 noemt "beheerder" als rol per inschrijving; OOAPI kent daarvoor
`coordinator`, en "beheerder" als systeemrol is al de rol `admin` uit het token.

### D3: Eén invoerpad: input → validator → upsert-service

Invoer is per record een woordenboek OOAPI-veldnaam → tekst, met `ExternalId`-verwijzingen. Een CSV-regel is dat al,
en REST kan later een JSON-object daarop afbeelden; zo zijn er geen zes invoerrecords en zes mappers nodig. Per type
(`AcademicType`: velden, validatie, toepassen) een validator die een `Result` met fouten per veld geeft
(`Academic.<Type>.Invalid`), en één generieke upsert die:

1. de input valideert;
2. verwijzingen omzet naar `Id`'s (D4);
3. het record op (`ExternalId`, `Source`) zoekt, aanmaakt of de velden overschrijft;
4. aan de hand van de change tracker bepaalt of het `Created`, `Updated` of `Unchanged` is, en opslaat.

Validators zijn met de hand geschreven: er is geen validatiepakket in de solution, en de regels zijn eenvoudig
(verplicht, lengte, datum, enum). Alternatief FluentValidation (Apache-2.0) is afgewezen: een extra afhankelijkheid
voor zes kleine validators. "Ongewijzigd" komt vanzelf uit EF Core: dezelfde waarde toewijzen markeert geen property
als gewijzigd, dus de interceptor stempelt `DateLastModified` niet.

`add-api-and-import` roept later dezelfde upsert-service aan vanuit REST en de generieke import.

### D4: Verwijzingen binnen dezelfde bron

Een verwijzing (`courseId` in een offering, `personId` in een inschrijving) wordt opgezocht op `ExternalId` met
dezelfde `Source` als het record dat wordt ingevoerd. Records uit verschillende bronnen verwijzen dus niet naar
elkaar; dat past bij §4.4 (het SIS is bron van al deze typen). Een verwijzing naar een record met status
`ToBeDeleted` is toegestaan (het record bestaat nog).

### D5: CSV lezen met `TextFieldParser`

`Microsoft.VisualBasic.FileIO.TextFieldParser` zit in het .NET-framework en kan aanhalingstekens, scheidingstekens
binnen velden en regeleinden binnen velden aan. Het scheidingsteken wordt gekozen op basis van de kopregel (meer `;`
dan `,` → `;`). De BOM handelt `StreamReader` af. Alternatief CsvHelper (MS-PL/Apache-2.0) is afgewezen: geen nieuw
pakket voor wat het framework al kan.

Kolommen worden op naam (hoofdletterongevoelig) aan de velden van het invoerrecord gekoppeld; elk type heeft een
lijst met kolommen en welke verplicht zijn. Die lijst gebruikt `add-api-and-import` later ook voor de lege sjablonen.

### D6: Synchrone import op een interactieve pagina

`/academic/import` is een Blazor-pagina met `InputFile` (interactief, zoals de stijlgids). Het bestand wordt tot de
grens (`Academic:Import:MaxFileSize`, `MaxRows`) ingelezen, daarna regel voor regel door de upsert-service gehaald,
met één `SaveChangesAsync` per regel en `ChangeTracker.Clear()` per 500 regels. Een fout op een regel komt in het
rapport en de volgende regel gaat door. Het rapport staat alleen op het scherm; bewaren en downloaden komt met
`add-api-and-import`.

Alternatief: een minimal-API-endpoint met formulier-POST. Afgewezen, omdat een rapport bij een formulier-POST zonder
interactiviteit een tweede pagina of opslag nodig heeft; dat is precies wat `add-api-and-import` gaat bouwen.

### D7: Koppeling van login aan persoon in de module

Een tabel `academic.PersonLogins` (`PersonId`, `Issuer`, `Oid`, unieke index op `Issuer` + `Oid`) en een scoped
service `CurrentPerson` in de module. Bij de eerste vraag in een verzoek zoekt hij op issuer + `oid`; zonder treffer op
`ExternalId` = `personId` (alleen actieve personen) en legt dan de koppeling vast. Zonder `oid`-claim zoekt hij alleen
op `personId` en legt niets vast.

Waarom niet bij het inloggen zelf (bv. in `OnTokenValidated` of via een event): Infrastructure zou dan van Academic
moeten weten, of er komt een nieuw event alleen voor dit doel. Koppelen bij het eerste gebruik geeft hetzelfde
zichtbare gedrag met minder onderdelen. Afwijking van `docs/doctrina-integratie.md` ("anders e-mail"): er is geen
terugval op e-mail, want e-mail is niet stabiel en niet uniek genoeg om iemand aan een persoon te koppelen.

### D8: Pagina's, routes en permissies

| Pagina | Route | Permissie |
|---|---|---|
| Overzicht (links naar de lijsten) | `/academic` | `Academic.Read` |
| Lijst per type | `/academic/{organizations,academic-sessions,courses,course-offerings,persons,associations}` | `Academic.Read` |
| Detail | `/academic/<type>/{id}` (Guid, `"D"`-notatie) | `Academic.Read` |
| Import | `/academic/import` | `Academic.Import` |
| My courses | `/my-courses` | `Academic.MyCourses` |

Lijsten en details zijn statisch server-gerenderd: zoeken en paginering via een GET-formulier en queryparameters
(`?q=…&page=2`), zodat ze zonder scripts werken en te delen zijn. Menu-items: "Academic" (`Academic.Read`) en
"My courses" (`Academic.MyCourses`).

`Academic.MyCourses` is een permissie in plaats van "iedereen die is ingelogd", omdat `NavItem` alleen op permissie
filtert. Zo blijft `Doctrina.Core` ongewijzigd. `appsettings.Development.json` zet de module aan en geeft hem aan
`employee` en `student` (`admin` heeft al `*`). Niet in `appsettings.json`: daar staat de module niet aan, en een
koppeling naar een permissie van een uitgeschakelde module laat de host niet starten. Een productie-installatie zet
beide in haar eigen configuratie.

Omgekeerd mag een ontwikkelaar Academic in Development uitzetten zonder de koppeling aan te passen: een koppeling naar
een permissie van een **bekende maar uitgeschakelde** module wordt genegeerd (`ModuleCatalog`/`PermissionRegistry`,
spec-delta `access-control`). Een echt onbekende naam (typfout) laat het opstarten nog steeds mislukken.

### D9: Testdata uit de dev-stack

`generate-realm.ps1` schrijft naast de realm de bestanden `dev-stack/data/*.csv` (organizations, academic-sessions,
courses, course-offerings, persons, associations), met dezelfde personen en `personId`'s als in Keycloak. De generator
verdeelt studenten over een handvol uitvoeringen en zet `docent` als `lecturer` erin, zodat "My courses" voor de vaste
testaccounts iets toont. De bestanden worden gecommit, net als de realm-bestanden. Importvolgorde staat in
`dev-stack/README.md` en `docs/ontwikkelen/starten.md`.

## Risks / Trade-offs

- [Synchrone import van 10.000 regels duurt te lang voor een Blazor-circuit] → één `SaveChanges` per regel is bij LocalDB
  in de orde van seconden tot een halve minuut; de pagina toont "Importing…" en de grens is instelbaar. Echte bulk komt
  in `add-api-and-import`.
- [Validators met de hand: regels raken uit de pas met de kolomlijst] → de kolomlijst (D5) en de validator per type
  staan in hetzelfde bestand en worden samen getest.
- [Koppeling op `personId` geeft de verkeerde persoon als de IdP een fout nummer meegeeft] → alleen actieve personen,
  en de koppeling is per issuer + `oid` zichtbaar op de detailpagina van de persoon, zodat beheer het kan zien.
  Ontkoppelen in de UI valt buiten deze change (alleen-lezen).
- [Gegenereerde CSV's raken achter op de realm] → beide komen uit hetzelfde script in één run.

## Migration Plan

Nieuw schema `academic` met een eerste migratie; bestaande schema's veranderen niet. Aanzetten met `Modules:Enabled`
(`appsettings.Development.json` zet `Academic` aan). Terugdraaien: module uitzetten; het schema blijft staan.
Archiveren pas na `add-design-system`, `add-idp-unavailable-notice` en `switch-ui-to-english`, omdat die de specs
leveren waar deze change op rekent (app-shell, Engelse UI).
