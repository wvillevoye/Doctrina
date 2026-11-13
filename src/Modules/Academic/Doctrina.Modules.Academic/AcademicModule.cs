using Doctrina.Core.Modules;
using Doctrina.Infrastructure.Api;
using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Academic.Application;
using Doctrina.Modules.Academic.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Modules.Academic;

/// <summary>
/// The Academic module: the OOAPI v5 core model (organizations, sessions, courses, offerings, persons, associations),
/// CSV import, read-only admin pages and "My courses" (change add-academic-core).
/// </summary>
public sealed class AcademicModule : IModule
{
    public const string ModuleName = "Academic";

    public string Name => ModuleName;

    public IReadOnlyCollection<string> DependsOn => [];

    public IReadOnlyCollection<Permission> Permissions =>
    [
        new(AcademicPermissions.Read, "View organizations, sessions, courses, offerings, persons and enrolments"),
        new(AcademicPermissions.Import, "Import academic data from CSV files"),
        new(AcademicPermissions.Write, "Create, change and delete academic data through the API"),
        new(AcademicPermissions.MyCourses, "See your own courses"),
    ];

    public IReadOnlyCollection<NavItem> Navigation =>
    [
        new("My courses", "/my-courses", AcademicPermissions.MyCourses, Order: 10),
        new("Academic", "/academic", AcademicPermissions.Read, Order: 90),
    ];

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AcademicDbContext>(configuration, Name);
        services.AddErrorTitles("Doctrina.Modules.Academic.Resources.AcademicErrors", typeof(AcademicModule).Assembly);
        services.AddOptions<AcademicImportOptions>().BindConfiguration(AcademicImportOptions.Section);
        services.AddScoped<AcademicImporter>();
        services.AddScoped<AcademicQueries>();
        services.AddScoped<CurrentPerson>();
        services.AddScoped<Doctrina.Modules.Academic.Contracts.IAcademicDirectory, AcademicDirectory>();
        services.AddHealthChecks().AddCheck<AcademicHealthCheck>("academic", tags: [ModuleHealthChecks.TagFor(Name)]);
    }

    public void MapEndpoints(IEndpointRouteBuilder api) => Api.AcademicEndpoints.Map(api);
}

/// <summary>Permissions of the Academic module.</summary>
public static class AcademicPermissions
{
    public const string Read = "Academic.Read";
    public const string Import = "Academic.Import";
    public const string MyCourses = "Academic.MyCourses";
    public const string Write = "Academic.Write";
}
