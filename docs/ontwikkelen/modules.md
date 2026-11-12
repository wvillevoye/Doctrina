# Een module toevoegen

Elke feature van Doctrina is een **module**: een eigen project met een eigen SQL-schema, eigen permissies, navigatie,
endpoints en pagina's. Modules zitten in de build, maar starten alleen als ze in `Modules:Enabled` staan. Achtergrond:
`docs/Doctrina-voorstel.md` §3 en design D1, D2 en D8 van change `add-modular-foundation`.

Als voorbeeld gebruiken we een module `Academic`. In `tests/Doctrina.TestKit/Modules` staan werkende testmodules
(`SampleModule`, `NotesModule`) om af te kijken.

## 1. Projecten

```
src/Modules/Academic/
├─ Doctrina.Modules.Academic/             implementatie
└─ Doctrina.Modules.Academic.Contracts/   publieke interfaces, DTO's en events voor andere modules
```

```powershell
dotnet new razorclasslib -o src/Modules/Academic/Doctrina.Modules.Academic
dotnet new classlib      -o src/Modules/Academic/Doctrina.Modules.Academic.Contracts
dotnet sln Doctrina.slnx add src/Modules/Academic/Doctrina.Modules.Academic src/Modules/Academic/Doctrina.Modules.Academic.Contracts
```

Verwijder de voorbeeldbestanden van de templates en houd de `.csproj` kaal (doelplatform, nullable enzovoort komen uit
`Directory.Build.props`; versies van packages uit `Directory.Packages.props`). Verwijzingen:

| Project | Verwijst naar |
|---|---|
| `Doctrina.Modules.Academic` | `Doctrina.Core`, `Doctrina.Infrastructure`, `Doctrina.UI`, eigen `.Contracts`, `.Contracts` van andere modules |
| `Doctrina.Modules.Academic.Contracts` | alleen `Doctrina.Core` |
| `Doctrina.Host` | `Doctrina.Modules.Academic` (elke module, design D1) |

**Nooit** naar de implementatie van een andere module: de architectuurtests laten de build dan falen.

## 2. `IModule`

```csharp
public sealed class AcademicModule : IModule
{
    public string Name => "Academic";
    public IReadOnlyCollection<string> DependsOn => [];

    public IReadOnlyCollection<Permission> Permissions =>
    [
        new("Academic.Read", "Cursussen en personen bekijken"),
        new("Academic.Manage", "Cursussen en personen beheren"),
    ];

    public IReadOnlyCollection<NavItem> Navigation =>
    [
        new("Cursussen", "/cursussen", RequiredPermission: "Academic.Read", Order: 10),
    ];

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AcademicDbContext>(configuration, Name);
        services.AddErrorTitles("Doctrina.Modules.Academic.Resources.AcademicErrors", typeof(AcademicModule).Assembly);
        services.AddEventHandler<GroupMembershipChanged, GroupMembershipChangedHandler>();
        services.AddHealthChecks().AddCheck<AcademicHealthCheck>("academic", tags: [ModuleHealthChecks.TagFor(Name)]);
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var courses = api.MapGroup("courses");                       // → /api/v1/courses
        courses.MapGet("", ListCourses).RequirePermission("Academic.Read");
        courses.MapGet("{id:guid}", GetCourse).RequirePermission("Academic.Read");
    }
}
```

Afspraken:

- **Naam** = de naam in `Modules:Enabled`; het schema wordt de naam in kleine letters (`academic`).
- **Permissies** beginnen met `<Naam>.`; dubbele of verkeerd genoemde permissies laten het opstarten mislukken.
- **Endpoints** komen altijd onder `/api/v1` en vereisen standaard een token. Voeg `.RequirePermission(...)` toe.
- **Pagina's** staan in de module (`Components/`) en vragen een permissie met
  `@attribute [RequirePermission("Academic.Manage")]`. Ze geven alleen vorm met `Dc*`-componenten uit `Doctrina.UI`
  en `--dc-*`-tokens: geen ruwe Bootstrap-classes en geen letterlijke kleuren (de architectuurtests controleren dit).
  Begin elke pagina met `DcPageHeader`; zie [vormgeving.md](vormgeving.md) en de stijlgids op `/stijlgids`.
- **Navigatie-items** met `RequiredPermission` zie je alleen als je die permissie hebt.
- **Health checks** krijgen de tag `ModuleHealthChecks.TagFor(Name)`, zodat `/health/ready` ze per module toont.

## 3. Bekendmaken en aanzetten

In `src/Doctrina.Host/KnownModules.cs`:

```csharp
services.AddDoctrinaModule<AcademicModule>();
```

In `appsettings.json` (of per omgeving):

```json
{
  "Modules": { "Enabled": [ "Academic" ] },
  "Authorization": {
    "RolePermissions": {
      "admin": [ "*" ],
      "employee": [ "Academic.Read" ],
      "doctrina.persons.read": [ "Academic.Read" ]
    }
  }
}
```

Een module die niet in `Modules:Enabled` staat, levert niets: geen endpoints, pagina's, navigatie, permissies, health
checks, eventhandlers of migraties. Staat er een onbekende naam in, ontbreekt een afhankelijkheid (`DependsOn`) of
verwijst de rolkoppeling naar een onbekende permissie, dan start de host niet en noemt de foutmelding álle problemen.

## 4. Database

Entiteiten, `ModuleDbContext`, migraties: zie [database.md](database.md).

## 5. Fouten

Verwachte fouten zijn `Result`/`Result<T>` met een stabiele code met moduleprefix, verzameld in een statische klasse:

```csharp
public static class AcademicErrors
{
    public static Error CourseNotFound(Guid id) => Error.NotFound("Academic.Course.NotFound", $"Cursus {id} bestaat niet.");
}
```

Een endpoint geeft `result.ToHttpResult()` terug: dat wordt ProblemDetails met de juiste status en `code`. De titel
voor gebruikers komt uit `Resources/AcademicErrors.resx` (Nederlands) en `AcademicErrors.en.resx` (Engels), met de
foutcode als sleutel. Zonder vertaling wordt de melding uit de code getoond. Op een pagina toon je dezelfde fout met
`DcResult` of via `DcForm Error="…"`, met dezelfde vertaalde titel ([vormgeving.md](vormgeving.md#voorbeelden)).

## 6. Communiceren met andere modules

- **Contracts:** publieke interfaces en DTO's in `Doctrina.Modules.Academic.Contracts`; ze geven altijd een `Result`
  terug, nooit `null` of een exception voor een verwacht geval.
- **Events:** definieer het event in de contracts (`public sealed record CourseCreated(Guid Id) : IDomainEvent;`).
  - Hoort het bij een wijziging, roep dan `RaiseEvent(...)` aan in de entiteit: het wordt pas na een geslaagde
    `SaveChanges` afgeleverd.
  - Anders publiceer je met `IEventBus.PublishAsync`.
  - Een andere module registreert een handler met `services.AddEventHandler<CourseCreated, …>()`.

## 7. Tests

```
tests/Modules/Doctrina.Modules.Academic.Tests/             unittests van domein en services
tests/Modules/Doctrina.Modules.Academic.IntegrationTests/  echte SQL Server (LocalDB), echte migraties
```

Noem testklassen naar de requirement en testmethoden naar het scenario uit de spec, in het Nederlands
(`ElkeModuleHeeftEenEigenDatabaseschema.TabellenWordenInHetModuleschemaAangemaakt`). Elk spec-scenario heeft minstens
één test (huisregel 10).

## Controlelijst

- [ ] Twee projecten (`…` en `….Contracts`), Host verwijst naar de implementatie
- [ ] `IModule` met naam, permissies (`<Naam>.…`), navigatie, services en endpoints
- [ ] `AddDoctrinaModule<…>()` in `KnownModules.cs`, naam in `Modules:Enabled`, rolkoppeling bijgewerkt
- [ ] DbContext + eerste migratie ([database.md](database.md))
- [ ] Foutcodes met vertalingen
- [ ] Pagina's met `Dc*`-componenten, gecontroleerd in licht en donker
- [ ] Tests per spec-scenario; `dotnet test` groen (inclusief architectuurtests)
