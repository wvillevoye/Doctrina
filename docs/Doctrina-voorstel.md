# Doctrina — Voorstel & startpunt voor OpenSpec

*Doctrina* (Latijn): onderwijs, onderricht, leer.

> Doel van dit document: input voor `openspec explore` / het opstellen van de eerste OpenSpec-changes.
> Status: idee / verkenning — nog niets gebouwd.

---

## 1. Waarom

We willen een **eenvoudig, eigen LMS** dat:
- **modulair** is: functies (cursussen, groepen, templates, LTI, bulk-API, …) als losse modules toevoegen of uitzetten;
- **LTI 1.3** ingebouwd heeft om externe tools te starten;
- via **open standaarden** koppelt met andere systemen (SIS, SURF/Edu-V, externe tools);
- **bulk gevuld** kan worden via een API (SQL Server);
- een **eigen, herkenbaar ontwerp** heeft — **Bootstrap als technische basis, maar niet herkenbaar als Bootstrap**;
- spec-driven wordt gebouwd met **Claude + OpenSpec**.

## 2. Techniek

| Onderdeel | Keuze |
|---|---|
| Framework | .NET 10, C#, Blazor Web App (Interactive Server) |
| API | ASP.NET Core Minimal APIs, OpenAPI + **Scalar**, versie `/api/v1` |
| Data | EF Core 10, **SQL Server**, één SQL-schema per module |
| Auth gebruikers | **OpenID Connect** (bv. Entra ID, SURFconext, Keycloak — configureerbaar) |
| Auth systemen | OAuth 2.0 client credentials (API, LTI-services) |
| Achtergrondtaken | Hangfire (bulk-imports, LTI-cijfer-retries) |
| Styling | Bootstrap 5.3 vanuit **SCSS** + eigen design tokens + eigen componentenbibliotheek |
| Tests | xUnit, bUnit, Testcontainers (SQL Server) |
| Versiebeheer / deploy | Git (GitLab/GitHub), container-image voor Kubernetes/OpenShift of Docker |

Uitgangspunt: officiële of OSI-gelicenseerde packages; **geen commercieel gelicenseerde libraries** (bv. MediatR v13+, AutoMapper v15+, EFCore.BulkExtensions).

## 3. Architectuur: modulaire monoliet

### 3.1 Solution-structuur

```
Doctrina/
├─ Doctrina.slnx                         ← nieuwe XML-solutionindeling (.NET 10)
├─ Directory.Build.props                 ← net10.0, Nullable, TreatWarningsAsErrors, LangVersion
├─ Directory.Packages.props              ← Central Package Management (alle versies op één plek)
├─ global.json                           ← vastgezette .NET SDK
├─ .editorconfig
├─ AGENTS.md / CLAUDE.md
├─ openspec/                             ← project.md, specs/, changes/
├─ docs/                                 ← dit voorstel, architectuur, handleidingen
├─ infra/
│  ├─ doctrina-realm.json                ← Keycloak-realm (wordt in dev-omgeving geïmporteerd)
│  └─ Dockerfile
├─ src/
│  ├─ Doctrina.Host/                     ← Blazor Web App: Program.cs, App.razor, routing, auth, module-loading
│  ├─ Doctrina.Core/                     ← IModule, IEventBus, Entity-basisklassen, Permission, NavItem,
│  │                                        IClock, Result-type, gedeelde conventies (geen domeinlogica)
│  ├─ Doctrina.Infrastructure/           ← EF Core-basis (ModuleDbContext, conventies, SequentialGuid,
│  │                                        audit-interceptor), Hangfire-setup, OIDC-setup, logging
│  ├─ Doctrina.UI/                       ← Razor Class Library: SCSS, tokens, Dc*-componenten, layouts, icons
│  └─ Modules/
│     ├─ Academic/
│     │  ├─ Doctrina.Modules.Academic/             ← implementatie
│     │  └─ Doctrina.Modules.Academic.Contracts/   ← publieke interfaces, DTO's, events
│     ├─ Groups/            (idem: module + .Contracts)
│     ├─ Content/           (idem)
│     ├─ Templates/         (idem)
│     ├─ Lti/               (idem)
│     ├─ Import/
│     │  └─ Doctrina.Modules.Import/              ← CSV/zip-import, jobs, rapporten, sjablonen
│     └─ Interop/
│        ├─ Doctrina.Modules.Interop.OOAPI/
│        └─ Doctrina.Modules.Interop.OneRoster/    (later/optioneel)
└─ tests/
   ├─ Doctrina.Architecture.Tests/       ← afhankelijkheidsregels afdwingen (bv. NetArchTest/ArchUnitNET):
   │                                        modules verwijzen niet naar elkaars implementatie, UI zonder ruwe Bootstrap
   ├─ Doctrina.Core.Tests/
   ├─ Doctrina.UI.Tests/                 ← bUnit-tests van Dc*-componenten (incl. toegankelijkheid)
   ├─ Modules/
   │  ├─ Doctrina.Modules.Academic.Tests/            ← unit-tests domein + services
   │  ├─ Doctrina.Modules.Academic.IntegrationTests/ ← SQL Server via Testcontainers, echte migraties
   │  ├─ Doctrina.Modules.Lti.Tests/                 ← JWT/JWKS, launch-flow, AGS/NRPS
   │  ├─ Doctrina.Modules.Import.IntegrationTests/   ← CSV-parsing, staging + MERGE, idempotentie, grote bestanden
   │  └─ …                                           (per module een .Tests en waar nodig .IntegrationTests)
   ├─ Doctrina.Host.IntegrationTests/    ← WebApplicationFactory: opstarten, module-loading, auth, API-contracten
   └─ Doctrina.TestKit/                  ← gedeelde helpers: SqlServer-fixture, testdata-builders, fake clock, auth-handler
```

### 3.2 Indeling binnen een module

```
Doctrina.Modules.Academic/
├─ AcademicModule.cs            ← implementeert IModule
├─ Domain/                      ← entiteiten, value objects, domeinregels
├─ Application/                 ← use cases / services, validatie
├─ Infrastructure/
│  ├─ AcademicDbContext.cs      ← HasDefaultSchema("academic")
│  ├─ Configurations/           ← IEntityTypeConfiguration per entiteit
│  └─ Migrations/
├─ Api/                         ← Minimal API-endpoints (MapGroup("/api/v1/academic"))
├─ Components/                  ← Razor-pagina's en componenten van deze module
└─ Events/                      ← handlers voor events van andere modules
```

### 3.3 Afspraken
- Testprojecten spiegelen de `src`-structuur; testnamen verwijzen naar de spec-requirement.
- Integratietests gebruiken **echte SQL Server** (Testcontainers), geen in-memory provider.
- De architectuurtests falen de build als een module een andere module-implementatie refereert.
- Lokale ontwikkelomgeving (Keycloak, SQL Server, Mailpit) is een **apart project** (`dev-stack`); Doctrina bevat alleen zijn eigen realm-bestand en `appsettings.Development.json`.

### 3.4 Module-contract (schets)
```csharp
public interface IModule
{
    string Name { get; }
    IEnumerable<string> DependsOn { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration config);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
    IEnumerable<NavItem> Navigation { get; }
    IEnumerable<Permission> Permissions { get; }
}
```

### 3.5 Regels
- Modules aan/uit via `appsettings.json` → `Modules:Enabled`.
- Volgorde van laden op basis van `DependsOn`; ontbrekende afhankelijkheid = duidelijke startup-fout.
- Elke module: eigen DbContext, **eigen SQL-schema**, eigen migraties.
- Modules verwijzen **nooit** naar elkaars implementatie; alleen naar `.Contracts` of via **events** (`IEventBus`, in-process).
- Razor-componenten van modules worden via `AdditionalAssemblies` geladen; navigatie wordt opgebouwd uit wat modules aanleveren.
- Health checks per module.

### 3.6 Result-pattern (`Doctrina.Core`)

**Principe**
- **Verwachte fouten** (niet gevonden, validatie, geen rechten, conflict/dubbele ExternalId) → `Result` / `Result<T>`, géén exception.
- **Onverwachte fouten** (database weg, bug) → exception → globale exception-handler → log + generieke ProblemDetails (500).

**Eigen, klein type** (geen extern package nodig; ErrorOr/FluentResults (MIT) zijn alternatieven):

```csharp
public enum ErrorType { Validation, NotFound, Conflict, Forbidden, Unauthorized, Failure }

public sealed record Error(string Code, string Message, ErrorType Type,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static Error NotFound(string code, string msg)   => new(code, msg, ErrorType.NotFound);
    public static Error Validation(string code, string msg,
        IReadOnlyDictionary<string, string[]>? details = null) => new(code, msg, ErrorType.Validation, details);
    public static Error Conflict(string code, string msg)   => new(code, msg, ErrorType.Conflict);
    public static Error Forbidden(string code, string msg)  => new(code, msg, ErrorType.Forbidden);
}

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }
    protected Result(bool ok, Error? e) { IsSuccess = ok; Error = e; }
    public static Result Success() => new(true, null);
    public static Result Failure(Error e) => new(false, e);
    public static implicit operator Result(Error e) => Failure(e);
}

public sealed class Result<T> : Result
{
    public T? Value { get; }
    private Result(T v) : base(true, null) => Value = v;
    private Result(Error e) : base(false, e) { }
    public static Result<T> Success(T v) => new(v);
    public static new Result<T> Failure(Error e) => new(e);
    public static implicit operator Result<T>(T v) => Success(v);
    public static implicit operator Result<T>(Error e) => Failure(e);

    public TOut Match<TOut>(Func<T, TOut> ok, Func<Error, TOut> fail)
        => IsSuccess ? ok(Value!) : fail(Error!);
}
```

**Afspraken**
- **Foutcodes** met module-prefix en stabiel: `Academic.Course.NotFound`, `Academic.Person.DuplicateExternalId`, `Lti.Tool.InvalidJwks`, `Import.Row.MissingColumn`. Per module vastgelegd in een statische klasse (`AcademicErrors`).
- **API**: één extensie `result.ToHttpResult()` vertaalt naar **ProblemDetails (RFC 9457)** met `code` als extensieveld:
  | ErrorType | HTTP |
  |---|---|
  | Validation | 400 (met fouten per veld) |
  | Unauthorized | 401 |
  | Forbidden | 403 |
  | NotFound | 404 |
  | Conflict | 409 |
  | Failure | 500 |
- **Blazor**: `<DcResult>` / toast-service toont fouten; validatiefouten per veld bij het formulier.
- **Import**: elke CSV-regel levert een `Result`; het rapport verzamelt ze (zie 6.6).
- **Contracts tussen modules** geven altijd `Result`/`Result<T>` terug — nooit `null` of een exception voor een verwacht geval.
- Foutmeldingen voor gebruikers vertaalbaar (resource-bestanden NL/EN) op basis van de foutcode.

## 4. Datamodel — gebaseerd op Open Education API (OOAPI v5)

**Keuze:** eigen domeinmodel dat de **OOAPI-begrippen** volgt, zodat mapping triviaal is. OOAPI is de koppel-standaard (NL), niet één-op-één het databaseschema. LMS-specifieke zaken (content, opdrachten, LTI, templates) staan niet in OOAPI en maken we zelf.

| OOAPI v5 | Doctrina | OneRoster (ter referentie) |
|---|---|---|
| organization | Organization (universiteit, faculteit, vakgroep) | org |
| academicSession | AcademicSession (jaar, periode) | academicSession |
| program / programOffering | Program / ProgramOffering (opleiding) | – |
| course | Course (catalogus / template) | course |
| courseOffering | CourseOffering (uitvoering) | class |
| component / componentOffering | Component (college, toets, practicum) | – |
| person | Person | user |
| association | Association (inschrijving + rol + resultaat) | enrollment / result |
| group | Group (+ GroupSet, LMS-specifiek) | – |

### 4.1 Vaste velden op elke uitwisselbare entiteit
- `Id` (Guid, intern — zie 4.2)
- `ExternalId` / `SourcedId` (string, uniek, stabiel — sleutel voor koppelen en upserts)
- `Status` (actief / te verwijderen)
- `DateLastModified` (UTC) → maakt **delta-sync** mogelijk
- `Source` (welk systeem is eigenaar van dit record)

### 4.2 Sleutels en GUID's

**Opslag**
- `Id` altijd als SQL Server **`uniqueidentifier`** (16 bytes). Nooit als `char(32)`/`varchar(36)`: 2–2,5× groter in elke index en foreign key, tragere vergelijkingen, hoofdletter-issues.
- Streepjes zijn alleen **weergave**: `id.ToString("N")` = zonder, `"D"` = met. `Guid.Parse` leest beide.

**Volgorde / fragmentatie**
- Willekeurige GUID's (`Guid.NewGuid()`) als clustered key → indexfragmentatie, vooral bij bulk-imports.
- **Keuze: EF Core genereert de sleutel** (standaard sequentiële generator voor `Guid`-keys op SQL Server, afgestemd op de sorteervolgorde van SQL Server). Daarom in code **geen** `Guid.NewGuid()` voor entiteit-Id's invullen.
- Voor de bulk-pipeline (SqlBulkCopy buiten EF om): Id's laten genereren met dezelfde sequentiële generator, of `DEFAULT NEWSEQUENTIALID()` op de kolom.
- **Niet** `Guid.CreateVersion7()` gebruiken als clustered key: tijdgesorteerd in .NET, maar SQL Server sorteert `uniqueidentifier` op de laatste bytes → toch fragmentatie.
- Optie voor later bij zeer grote tabellen (bv. logging, xAPI-statements): clustered `bigint IDENTITY` + `uniqueidentifier` als unieke publieke sleutel.

**Externe sleutels**
- `ExternalId` / `SourcedId`: `nvarchar(255)` met **unieke index** (eventueel samen met `Source`). Formaat bepaalt het bronsysteem.
- Bulk-upserts (`MERGE`) matchen op `ExternalId` (+ `Source`), nooit op `Id`.

**Weergave in API en URL's**
- Intern en in eigen API: standaardvorm **met streepjes** (`"D"`), consequent overal.
- OOAPI-endpoints: standaard UUID-notatie (met streepjes), zoals de spec verwacht.
- Wil je kortere URL's in de UI: `"N"`-notatie is toegestaan, mits overal hetzelfde en de parser beide accepteert.

### 4.3 Rollen per inschrijving
student, docent, tutor/aide, beheerder.

### 4.4 Bronafspraken (vast te leggen per koppeling)
- SIS is leidend voor: personen, cursussen, offerings, inschrijvingen.
- LMS is leidend voor: groepen, content, opdrachten, LTI-configuratie.
- **Identity provider ≠ bron van personen** (besloten 2026-11-12). Keycloak (lokaal), Entra ID en SURFconext zeggen alleen
  *wie* er inlogt; ze hebben een eigen opslag die Doctrina nooit uitleest. Personen komen in Doctrina via het SIS
  (REST of CSV-import, §6.6), met het persoonsnummer als `ExternalId`. Bij inloggen koppelt Doctrina het token via
  `personId` ↔ `ExternalId` aan de persoon en onthoudt daarna issuer + `oid` (zie `docs/doctrina-integratie.md`).
  **Geen just-in-time aanmaken** van personen bij de eerste login: anders wordt de IdP de bron en ontbreken mensen die
  nog nooit hebben ingelogd (bv. bij groepsindeling). Wie inlogt zonder bijbehorende persoon, krijgt een duidelijke melding.

## 5. Standaarden

| Standaard | Waarvoor | Fase |
|---|---|---|
| **OOAPI v5** (SURF / Edu-V) | Primaire NL-koppeling: aanbieden én consumeren | 1–2 |
| **LTI 1.3 + Advantage** (1EdTech) | Externe tools starten; Deep Linking 2.0, AGS (cijfers), NRPS (deelnemers) | 2 |
| OneRoster 1.2 (1EdTech) | Optionele CSV/REST-koppeling internationaal | later |
| QTI 3.0 / Common Cartridge 1.3 | Toetsen / cursussen uitwisselen (bv. migratie uit Canvas) | later |
| xAPI | Leeranalytics | optioneel |
| ISO 8601 / ISO 639-1 / ISO 3166-1 | Datums, talen, landen | altijd |
| ISO/IEC 27001-maatregelen | Security, logging, least privilege, geen PII in logs | altijd |
| WCAG 2.2 AA / EN 301 549 | Toegankelijkheid | altijd |
| AVG | Dataminimalisatie; LTI deelt alleen geconfigureerde claims | altijd |

Let op: LTI 1.1 **niet** ondersteunen (verouderd).

## 6. Modules (eerste set)

### 6.1 Academic (kern)
Organisaties, sessies, programma's, cursussen, offerings, componenten, personen, inschrijvingen + rollen. Beheerschermen en "mijn cursussen".

Vastgelegd voor change `add-academic-core` (2026-11-12): personen komen binnen via de **CSV-route** (§4.4, §6.6 B), niet
via de identity provider. Voor lokaal ontwikkelen maakt `dev-stack/scripts/generate-realm.ps1` naast de Keycloak-realm
ook een personen-CSV in Doctrina-formaat met dezelfde mensen en hetzelfde `personId`, zodat testaccounts (`student`,
`docent`, `beheerder` en de 250 nepgebruikers) na import in beide systemen bestaan en de koppeling bij inloggen werkt.

### 6.2 Groups
GroupSets binnen een offering; groepen handmatig, willekeurig of zelf-inschrijven; max. grootte; zichtbaar voor rollen. Event `GroupMembershipChanged` (bv. voor LTI-NRPS).

### 6.3 Templates
Een **Course** dient als template; een **CourseOffering** wordt daaruit aangemaakt (structuur, componenten, content, LTI-links kopiëren). Daarnaast herbruikbare **contentblok-templates**.
> *Open vraag: bedoelen we vooral cursussjablonen, pagina-/layouttemplates, of beide?*

### 6.4 Content
Modules/secties binnen een offering, pagina's, bestanden, links, LTI-items; publiceren / verbergen; volgorde.

### 6.5 LTI (platform-rol)
- Toolregistratie: client_id, deployment_id, login-URL, redirect-URI's, JWKS-URL (later: Dynamic Registration).
- OIDC third-party login initiation → `id_token` (JWT, RS256) → launch.
- Eigen **JWKS-endpoint**, sleutelrotatie.
- **Deep Linking 2.0** (docent kiest content in tool), **AGS** (cijfers terug naar LMS), **NRPS** (deelnemerslijst).
- Claims per tool configureerbaar (AVG).
- Implementatie met `Microsoft.IdentityModel.Tokens` (geen goede officiële .NET-platformlibrary).
- Later: Doctrina ook als LTI-**tool**.
- Conformance testen met de 1EdTech-certificeringssuite.

### 6.6 API & import

Eén API met **twee ingangen** die dezelfde validatie en dezelfde services gebruiken:

```
REST-aanroep ─┐
              ├─> DTO ─> zelfde validator ─> zelfde upsert-service ─> Result
CSV-regel ────┘
```

#### A. Losse records (REST)
Elke module levert zijn eigen endpoints (bv. Academic levert persons/courses/offerings/associations, Groups levert groups), allemaal onder `/api/v1`, OOAPI-naamgeving, gedocumenteerd met Scalar.

| Endpoint | Doel |
|---|---|
| `GET /api/v1/persons` | lijst, met paginering, filters, `modifiedSince` (delta) |
| `GET /api/v1/persons/{id}` | één record op Doctrina-Id |
| `POST /api/v1/persons` | nieuw record |
| `PUT /api/v1/persons/by-external-id/{externalId}` | **upsert** op sleutel van het bronsysteem — bronsysteem hoeft onze GUID niet te kennen |
| `PATCH /api/v1/persons/{id}` | gedeeltelijk bijwerken |
| `DELETE /api/v1/persons/{id}` | **soft delete**: status → te verwijderen |
| `POST /api/v1/persons/batch` | tot ±500 records in één JSON-aanroep, resultaat per record |

Zelfde patroon voor `courses`, `course-offerings`, `components`, `associations` (inschrijvingen), `groups`, `organizations`, `academic-sessions`.

#### B. CSV-/zip-import (module `Import`)

| Endpoint | Doel |
|---|---|
| `POST /api/v1/imports` | upload (multipart): één CSV per type of een zip met meerdere CSV's + `manifest.csv` |
| `POST /api/v1/imports?dryRun=true` | alleen valideren, niets opslaan — laat zien wat er zou gebeuren |
| `GET /api/v1/imports/{importId}` | status (`queued` / `running` / `completed` / `failed`) + samenvatting |
| `GET /api/v1/imports/{importId}/report` | rapport per regel (aangemaakt / bijgewerkt / overgeslagen / fout + foutcode), als JSON of CSV |
| `GET /api/v1/imports/templates/{type}.csv` | leeg sjabloon met de juiste kolommen |

- **Asynchroon**: upload geeft direct `202 Accepted` + `importId`; Hangfire verwerkt.
- **Formaten**: eigen Doctrina-CSV (kolommen volgen OOAPI-velden) en optioneel OneRoster 1.2-CSV via `Interop.OneRoster`.
- **Volgorde** automatisch bij zip: organizations → academic-sessions → courses → course-offerings → components → persons → associations → groups.
- **Praktisch**: UTF-8 (met/zonder BOM), scheidingsteken `,` of `;` automatisch herkend (NL-Excel), datums ISO 8601, kopregel verplicht, onbekende kolommen geven een waarschuwing.
- **Modi**: `upsert` (standaard, alleen toevoegen/bijwerken) of `fullSync` (records van deze bron die niet in het bestand staan → status te verwijderen).
- **Foute regels stoppen de batch niet**; elke regel krijgt een eigen `Result`, het rapport verzamelt ze.
- **Idempotent**: hetzelfde bestand twee keer = geen dubbele data (match op `ExternalId` + `Source`); optioneel `Idempotency-Key`-header bij upload.

#### C. Performance
- Klein (< ±10.000 regels): per regel via de upsert-service (EF Core).
- Groot: valideren → **SqlBulkCopy naar staging-tabel** → `MERGE` op `ExternalId` + `Source` → rapport. Id's sequentieel gegenereerd (zie 4.2).
- Bestanden tijdelijk opgeslagen (bewaartermijn instelbaar, AVG), daarna verwijderd.

#### D. Beveiliging
- Systemen: OAuth2 **client credentials** met scopes, bv. `doctrina.persons.read`, `doctrina.persons.write`, `doctrina.import`.
- Gebruikers (UI): rol `admin` of specifieke permissie `Import.Execute`.
- Rate limiting op import- en batch-endpoints; maximale bestandsgrootte instelbaar.
- Audit-log: wie importeerde wat, wanneer, met welk resultaat.

#### E. Importscherm in Doctrina
Beheerder uploadt CSV/zip → ziet dry-run-resultaat → bevestigt → volgt voortgang → downloadt rapport. Sjablonen zijn op hetzelfde scherm te downloaden.

### 6.7 Interop.OOAPI
- Biedt OOAPI v5-endpoints aan (lezen; associations ook schrijven).
- Kan OOAPI-bronnen consumeren om het LMS te vullen (via bulk-pipeline).
- Implementeer de **profielen/consumers** die de instelling en koppelpartners echt gebruiken, niet de hele spec.

## 7. Styling — Bootstrap, maar niet Bootstrap-look

### 7.0 Eigen ontwerp — richting "Doctrina"
Geen huisstijl van een instelling, maar een eigen identiteit die past bij de naam: **rustig, academisch, modern**.

- **Karakter:** "moderne studeerkamer" — veel witruimte, papierachtige ondergrond, inktkleuren, één warm accent.
- **Palet (voorstel, te verfijnen):**
  | Token | Rol | Richting |
  |---|---|---|
  | `--dc-ink` | primair / tekst | diep inktblauw (#1F2A44) |
  | `--dc-accent` | acties, focus | terracotta / zegelrood (#C2573A) |
  | `--dc-sage` | secundair, success-achtig | gedempt saliegroen (#6F8F7A) |
  | `--dc-paper` | achtergrond | warm gebroken wit (#FAF7F2) |
  | `--dc-stone-*` | neutralen | warme grijzen (50–900) |
  Dark mode: diepe inkt-achtergrond, perkament-tekst, accent iets lichter.
- **Typografie:** serif voor koppen (bv. *Fraunces* of *Source Serif 4*) + strakke sans voor tekst/UI (bv. *Inter*), beide zelf gehost. De serif-koppen geven direct het "Doctrina"-gevoel en breken de Bootstrap-look.
- **Vormtaal:** ruime radius (0.75rem) op cards, knoppen iets compacter (0.5rem), dunne 1px-randen i.p.v. schaduwen; schaduw alleen voor zwevende lagen (modals, menus).
- **Details:** eigen focus-ring in accentkleur, onderstreepte links met offset, iconset consequent (bv. Lucide of Tabler, open source), subtiele micro-animaties (150–200 ms).
- **Logo/merk:** woordmerk "Doctrina" in de serif, eventueel een eenvoudig symbool (open boek / zuil / lauriertak) — later uitwerken.
- **White-label:** instellingen kunnen logo en tokens overschrijven zonder code te wijzigen.

### 7.1 Design tokens
Eén bron van waarheid als CSS-variabelen `--dc-*`:
kleuren (eigen Doctrina-palet, accent, neutralen, semantisch: success/warning/danger), typografie (font, schaal), spacing, radius, schaduw, motion. Dark mode via `data-bs-theme`; een instelling of faculteit kan een eigen thema krijgen = alleen andere tokenwaarden (white-label).

### 7.2 Bootstrap vanuit SCSS
Alleen benodigde delen importeren (reboot, grid, utilities, forms, …) en variabelen overschrijven:
```scss
$font-family-base: "Inter", sans-serif;      // zelf gehost (AVG)
$headings-font-family: "Fraunces", serif;
$border-radius: .75rem;
$btn-padding-y: .625rem;
$enable-shadows: false;
$focus-ring-color: rgba(var(--dc-accent-rgb), .35);
@import "bootstrap/scss/bootstrap";
```
Wat Bootstrap herkenbaar maakt en wij aanpassen: font, knoppen (vorm, kleur, gewicht), focus-ring/schaduwen, cards en tabellen (witruimte, randen, hover).

### 7.3 Componentenbibliotheek `Doctrina.UI`
- Modules gebruiken **nooit** ruwe Bootstrap-classes, maar `DcButton`, `DcCard`, `DcTable`, `DcModal`, `DcForm…`, `DcPageHeader`, enz.
- Stijl centraal → consistent, en Bootstrap is later vervangbaar.
- Component-specifieke stijl via CSS-isolatie (`.razor.css`), altijd met tokens, nooit hardcoded kleuren.

### 7.4 Verder
- Mobile-first, CSS Grid + container queries.
- WCAG 2.2 AA: contrast, zichtbare focus, toetsenbord, `prefers-reduced-motion`.
- Build: Sass-compilatie (`AspNetCore.SassCompiler` of npm-stap) + PurgeCSS.
- **Stijlgids-pagina** in de app die alle componenten toont.

## 8. Fasering → OpenSpec-changes

| # | Change-id | Inhoud | Afhankelijk van |
|---|---|---|---|
| 1 | `add-modular-foundation` | Solution-opzet, IModule, module-loading, event bus, per-module schema, OIDC-login, autorisatie | – |
| 2 | `add-design-system` | Tokens, SCSS-build, `Doctrina.UI`-componenten, layout, stijlgids, dark mode | 1 |
| 3 | `add-academic-core` | OOAPI-gebaseerd kernmodel + beheerschermen + "mijn cursussen" | 1, 2 |
| 4 | `add-groups` | GroupSets en groepen | 3 |
| 5 | `add-api-and-import` | REST per resource (incl. upsert op ExternalId, batch) + CSV/zip-import, dry-run, jobs, rapportage | 3 |
| 6 | `add-ooapi-interop` | OOAPI v5-endpoints aanbieden/consumeren | 3, 5 |
| 7 | `add-content` | Content-structuur binnen offerings | 3 |
| 8 | `add-lti-platform` | LTI 1.3 core + Deep Linking + AGS + NRPS | 3, 7 |
| 9 | `add-course-templates` | Offering aanmaken vanuit cursus-template | 3, 7, 8 |
| later | `add-oneroster-interop`, `add-common-cartridge-import`, `add-lti-tool-role`, `add-xapi` | | |

Per change: `proposal.md`, `tasks.md`, `design.md` (bij architectuurkeuzes), delta-specs in `specs/<capability>/spec.md`. Alle artefacten (ook de specs) en de gebruikersdocumentatie in het Nederlands; alleen de structuurwoorden die de validator nodig heeft blijven Engels (`## Why`, `### Requirement:`, `#### Scenario:`, SHALL/MUST, GIVEN/WHEN/THEN) — design D12 van `add-modular-foundation`.

## 9. Huisregels voor Claude (voor `AGENTS.md` / `CLAUDE.md`)

1. Lees eerst `openspec/project.md`; geen code zonder goedgekeurde change.
2. Elke feature = een module; geen verwijzingen naar implementatie van andere modules.
3. Elke module eigen SQL-schema + migraties.
4. Geen ruwe Bootstrap-classes of hardcoded kleuren in modules; alleen `Doctrina.UI` + tokens.
5. Entiteit-Id's als `uniqueidentifier`, gegenereerd door EF Core (sequentieel) — nooit `Guid.NewGuid()` of `CreateVersion7()` voor sleutels.
6. Uitwisselbare entiteiten altijd met `Id`, `ExternalId`, `Status`, `DateLastModified`, `Source`.
7. Standaard eerst (OOAPI, LTI 1.3, …) — volg naamgeving en datamodel van de standaard.
8. Geen commercieel gelicenseerde packages zonder akkoord.
9. Async + `CancellationToken`, nullable aan, warnings = errors.
10. Elk spec-scenario heeft minstens één test.
11. Verwachte fouten via `Result`/`Result<T>` met stabiele foutcodes (module-prefix); API vertaalt naar ProblemDetails via `ToHttpResult()`.
12. REST-endpoint en CSV-import gebruiken dezelfde validator en upsert-service — nooit validatielogica dupliceren.

## 10. Open vragen

- Templates: cursussjablonen, layout/contentblokken, of beide?
- Welke OOAPI-profielen/consumers gebruiken de beoogde instellingen of koppelpartners (SURF/Edu-V)?
- Welk SIS is de bron, en levert dat OOAPI of iets anders?
- Eerste LTI-tools om mee te testen (bv. een eigen tool of de 1EdTech reference tool)?
- Multi-tenant (meerdere faculteiten met eigen thema) nodig, of één instantie?
- Bestandsopslag: SQL, bestandsshare of object storage (S3-compatibel)?
- Relatie met Canvas: naast elkaar, migratie, of alleen als experiment?

## 11. Starten op de computer

```bash
# repo aanmaken
mkdir Doctrina && cd Doctrina && git init

# OpenSpec initialiseren
npm install -g @fission-ai/openspec
openspec init            # kies Claude Code als tool

# dit document neerzetten
mkdir -p docs && cp ~/Downloads/Doctrina-voorstel.md docs/
```

Daarna in Claude Code:

> Lees `docs/Doctrina-voorstel.md`. Vul `openspec/project.md` en `AGENTS.md` op basis van hoofdstuk 2, 3, 4 en 9. Maak daarna de eerste change `add-modular-foundation` (proposal, tasks, design en delta-specs voor `module-system`). Stel eerst je vragen bij onduidelijkheden, en valideer met `openspec validate add-modular-foundation --strict`.
