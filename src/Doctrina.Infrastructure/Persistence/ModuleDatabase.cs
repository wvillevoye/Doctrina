using Doctrina.Infrastructure.Events;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Doctrina.Infrastructure.Persistence;

/// <summary>Gedeelde instellingen voor de databases van modules (design D3/D13).</summary>
public static class ModuleDatabase
{
    /// <summary>Naam van de connection string (<c>ConnectionStrings:Doctrina</c>).</summary>
    public const string ConnectionStringName = "Doctrina";

    /// <summary>Naam van de migratiegeschiedenistabel; staat per module in het eigen schema.</summary>
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";

    /// <summary>Lokale standaarddatabase voor ontwikkelen en ontwerptijd (<c>dotnet ef</c>), zie design D13.</summary>
    public const string LocalDbConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=Doctrina;Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>SQL-schema van een module: de modulenaam in kleine letters.</summary>
    public static string SchemaFor(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        return moduleName.Replace(".", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    /// <summary>Configureert SQL Server met het moduleschema voor de migratiegeschiedenis.</summary>
    public static DbContextOptionsBuilder UseModuleSqlServer(this DbContextOptionsBuilder options, string connectionString, string moduleName)
    {
        ArgumentNullException.ThrowIfNull(options);
        var schema = SchemaFor(moduleName);
        return options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(MigrationsHistoryTable, schema));
    }

    /// <summary>Opties zonder DI, bv. voor <c>dotnet ef</c> of tests; zonder <paramref name="events"/> worden entiteit-events niet afgeleverd.</summary>
    public static DbContextOptions<TContext> CreateOptions<TContext>(
        string connectionString,
        string moduleName,
        TimeProvider? time = null,
        InProcessEventBus? events = null)
        where TContext : ModuleDbContext
    {
        var options = new DbContextOptionsBuilder<TContext>();
        options.UseModuleSqlServer(connectionString, moduleName).AddInterceptors(new DateLastModifiedInterceptor(time ?? TimeProvider.System));
        if (events is not null)
        {
            options.AddInterceptors(new DomainEventsInterceptor(events));
        }

        return options.Options;
    }

    /// <summary>
    /// Is de fout een schending van een unieke index (SQL Server 2601/2627), bv. een dubbele <c>ExternalId</c> binnen een
    /// <c>Source</c>? De upsert-services vertalen dit naar een <c>Conflict</c>-fout.
    /// </summary>
    public static bool IsUniqueConstraintViolation(this DbUpdateException exception) =>
        exception?.InnerException is SqlException { Number: 2601 or 2627 };
}

/// <summary>Een DbContext van een ingeschakelde module, in afhankelijkheidsvolgorde geregistreerd (voor migraties).</summary>
public sealed record ModuleDbContextRegistration(string ModuleName, Type ContextType);

/// <summary>Registratie van module-DbContexts.</summary>
public static class ModuleDatabaseServiceCollectionExtensions
{
    /// <summary>
    /// Registreert <typeparamref name="TContext"/> voor module <paramref name="moduleName"/>: eigen schema, eigen
    /// migratiegeschiedenis, <c>DateLastModified</c>-interceptor en aflevering van entiteit-events na het opslaan. Roep aan vanuit <c>IModule.ConfigureServices</c>, zodat
    /// alleen ingeschakelde modules een database krijgen.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, IConfiguration configuration, string moduleName)
        where TContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ModuleDatabase.SchemaFor(moduleName);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<DateLastModifiedInterceptor>();
        services.AddDoctrinaEvents();
        services.TryAddScoped<DomainEventsInterceptor>();
        services.AddDbContext<TContext>((provider, options) =>
        {
            var connectionString = configuration.GetConnectionString(ModuleDatabase.ConnectionStringName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ModuleDatabase.ConnectionStringName} ontbreekt.");
            options.UseModuleSqlServer(connectionString, moduleName)
                .AddInterceptors(
                    provider.GetRequiredService<DateLastModifiedInterceptor>(),
                    provider.GetRequiredService<DomainEventsInterceptor>());
        });
        services.AddSingleton(new ModuleDbContextRegistration(moduleName, typeof(TContext)));
        return services;
    }
}

/// <summary>
/// Basis voor de ontwerptijd-factory van een module, zodat <c>dotnet ef</c> werkt zonder de host te starten
/// (zie <c>docs/ontwikkelen/database.md</c>). Connection string: argument <c>--connection</c> na <c>--</c>, anders de
/// omgevingsvariabele <c>ConnectionStrings__Doctrina</c>, anders LocalDB.
/// </summary>
public abstract class ModuleDesignTimeDbContextFactory<TContext>(string moduleName) : IDesignTimeDbContextFactory<TContext>
    where TContext : ModuleDbContext
{
    public TContext CreateDbContext(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var index = Array.IndexOf(args, "--connection");
        var connectionString = index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : Environment.GetEnvironmentVariable($"ConnectionStrings__{ModuleDatabase.ConnectionStringName}") ?? ModuleDatabase.LocalDbConnectionString;
        return (TContext)Activator.CreateInstance(typeof(TContext), ModuleDatabase.CreateOptions<TContext>(connectionString, moduleName))!;
    }
}
