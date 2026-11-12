# Database en migraties

Doctrina gebruikt **één SQL Server-database** (`Doctrina`). Elke module heeft daarin een **eigen schema** (de
modulenaam in kleine letters, bv. `academic`), een **eigen DbContext** en **eigen migraties**, met een eigen
migratiegeschiedenis (`<schema>.__EFMigrationsHistory`). Zo kun je modules los van elkaar toevoegen, migreren of
verwijderen. Achtergrond: design D3, D4 en D13 van change `add-modular-foundation`.

| Omgeving | Database |
|---|---|
| Development | SQL Server LocalDB: `(localdb)\MSSQLLocalDB`, database `Doctrina` (Windows-authenticatie, geen wachtwoord) |
| Test (geautomatiseerde tests) | LocalDB, per test een eigen database `Doctrina_Test_<id>` die na afloop wordt verwijderd |
| Productie | gewone SQL Server; connection string in `appsettings.Secrets.json` of de omgevingsvariabele `ConnectionStrings__Doctrina` |

## Vereisten

```powershell
sqllocaldb info MSSQLLocalDB     # LocalDB aanwezig? (komt mee met Visual Studio / SQL Server Express)
sqllocaldb start MSSQLLocalDB    # starten als hij gestopt is
dotnet tool restore              # installeert dotnet-ef in de versie uit dotnet-tools.json (gelijk aan EF Core)
```

## Een module een database geven

1. **Entiteiten** erven van `Entity` of, als ze met andere systemen worden uitgewisseld, van `ExchangeableEntity`
   (`Id`, `ExternalId`, `Source`, `Status`, `DateLastModified`). Vul `Id` nooit zelf in: EF Core genereert een
   sequentiële GUID bij het opslaan. `DateLastModified` wordt automatisch gezet.
2. **DbContext** erft van `ModuleDbContext`:

   ```csharp
   public sealed class AcademicDbContext(DbContextOptions<AcademicDbContext> options) : ModuleDbContext(options)
   {
       public DbSet<Person> Persons => Set<Person>();

       protected override void OnModelCreating(ModelBuilder modelBuilder)
       {
           modelBuilder.ApplyConfigurationsFromAssembly(typeof(AcademicDbContext).Assembly);
           base.OnModelCreating(modelBuilder); // zet het schema en de conventies (unieke index op Source + ExternalId)
       }
   }
   ```

3. **Registreren** in `IModule.ConfigureServices`, zodat alleen een ingeschakelde module een database krijgt:

   ```csharp
   services.AddModuleDbContext<AcademicDbContext>(configuration, Name);
   ```

4. **Ontwerptijd-factory** voor `dotnet ef`, zodat je de host niet hoeft te starten:

   ```csharp
   public sealed class AcademicDbContextFactory() : ModuleDesignTimeDbContextFactory<AcademicDbContext>("Academic");
   ```

5. In de `.csproj` van de module: `<PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />`.

## Een migratie toevoegen

Algemeen (vanuit de root van de repo):

```powershell
dotnet ef migrations add <Naam> `
  --project src/Modules/<Module>/Doctrina.Modules.<Module> `
  --startup-project src/Modules/<Module>/Doctrina.Modules.<Module> `
  --context <Module>DbContext `
  --output-dir Infrastructure/Migrations
```

Voorbeeld met de testmodule `Sample` (zo zijn de migraties in `tests/Doctrina.TestKit/Modules/Data/Migrations` gemaakt):

```powershell
dotnet ef migrations add Initieel `
  --project tests/Doctrina.TestKit --startup-project tests/Doctrina.TestKit `
  --context SampleDbContext --output-dir Modules/Data/Migrations/Sample
```

- Kies een beschrijvende naam (`PersoonEmailToegevoegd`), één migratie per samenhangende wijziging.
- Controleer de gegenereerde migratie: alles hoort in het schema van de module (`schema: "academic"`).
- Migratiebestanden zijn gegenereerde code; `.editorconfig` zet de analyzers daarvoor uit.
- Verkeerde migratie die nog nergens is toegepast? `dotnet ef migrations remove` met dezelfde parameters.

## Migraties toepassen

**Automatisch in Development.** Bij het starten van de host past `ModuleMigrationService` de openstaande migraties van
alle *ingeschakelde* modules toe, in afhankelijkheidsvolgorde, voordat de host verzoeken aanneemt. Dit staat achter
`Database:MigrateOnStartup` (standaard `true` in Development, anders `false`).

**Handmatig** (bv. om een andere database bij te werken):

```powershell
dotnet ef migrations list   --project <module> --startup-project <module> --context <Module>DbContext --connection "<connection string>"
dotnet ef database update   --project <module> --startup-project <module> --context <Module>DbContext --connection "<connection string>"
```

Zonder `--connection` gebruikt de ontwerptijd-factory de omgevingsvariabele `ConnectionStrings__Doctrina`, en anders
LocalDB (`(localdb)\MSSQLLocalDB`, database `Doctrina`).

**Productie.** Daar migreert de host niet zelf. De deployment-pipeline draait per module een migratiebundel of een
idempotent SQL-script:

```powershell
dotnet ef migrations bundle --project <module> --startup-project <module> --context <Module>DbContext --output efbundle-<module>.exe
.\efbundle-<module>.exe --connection "<connection string>"

dotnet ef migrations script --idempotent --project <module> --startup-project <module> --context <Module>DbContext --output <module>.sql
```

## Fouten bij opslaan

Een dubbele `ExternalId` binnen dezelfde `Source` wordt door de database geweigerd (unieke index). Vang dat af met
`DbUpdateException.IsUniqueConstraintViolation()` en geef een `Conflict`-fout terug (bv.
`Academic.Person.DuplicateExternalId`), geen exception naar de gebruiker.

## Tests

Integratietests gebruiken de echte SQL Server-engine via LocalDB, nooit een in-memory provider:

```csharp
await using var database = await LocalDbTestDatabase.CreateAsync(baseConnectionString, ct); // lege database
var options = ModuleDatabase.CreateOptions<SampleDbContext>(database.ConnectionString, "Sample", fakeTimeProvider);
await using var context = new SampleDbContext(options);
await context.Database.MigrateAsync(ct);
```

De basis-connection string staat in `src/Doctrina.Host/appsettings.Test.json`. De testdatabase wordt na de test
verwijderd. Als er na een afgebroken testrun toch databases `Doctrina_Test_…` blijven staan, kun je ze verwijderen in
SQL Server Management Studio of met `sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "DROP DATABASE [Doctrina_Test_…]"`.

## Problemen

| Melding | Oplossing |
|---|---|
| `A network-related or instance-specific error … (localdb)` | `sqllocaldb start MSSQLLocalDB`; bestaat de instantie niet: `sqllocaldb create MSSQLLocalDB -s` |
| `ConnectionStrings:Doctrina ontbreekt` | connection string in `appsettings.{omgeving}.json` of `appsettings.Secrets.json` zetten |
| `… heeft geen moduleschema` | de DbContext registreren met `AddModuleDbContext` (of opties maken met `ModuleDatabase.CreateOptions`) |
| `dotnet ef` niet gevonden of andere versie | `dotnet tool restore` in de root van de repo |
