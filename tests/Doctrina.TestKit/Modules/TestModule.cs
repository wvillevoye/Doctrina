using Doctrina.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Doctrina.TestKit.Modules;

/// <summary>Markering die een module bij <see cref="IModule.ConfigureServices"/> registreert, om de startvolgorde te zien.</summary>
public sealed record InitializedModule(string Name);

/// <summary>Instelbare testmodule (design D10): naam, afhankelijkheden, navigatie, permissies en health check.</summary>
public class TestModule(string name, params string[] dependsOn) : IModule
{
    public string Name => name;

    public IReadOnlyCollection<string> DependsOn => dependsOn;

    public List<NavItem> NavItems { get; } = [];

    public List<Permission> DeclaredPermissions { get; } = [];

    /// <summary>Uitkomst van de health check van deze module; <see langword="null"/> = geen health check.</summary>
    public HealthStatus? Health { get; init; }

    /// <summary>Extra registraties van deze module, bv. eventhandlers; alleen uitgevoerd als de module is ingeschakeld.</summary>
    public Action<IServiceCollection>? Services { get; init; }

    /// <summary>Extra endpoints onder <c>/api/v1</c>; alleen gekoppeld als de module is ingeschakeld.</summary>
    public Action<IEndpointRouteBuilder>? Endpoints { get; init; }

    public IReadOnlyCollection<NavItem> Navigation => NavItems;

    public IReadOnlyCollection<Permission> Permissions => DeclaredPermissions;

    public virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new InitializedModule(Name));
        Services?.Invoke(services);
        if (Health is { } status)
        {
            services.AddHealthChecks().AddCheck(
                $"{Name.ToLowerInvariant()}-check",
                () => new HealthCheckResult(status, "testcheck"),
                tags: [ModuleHealthChecks.TagFor(Name)]);
        }
    }

    public virtual void MapEndpoints(IEndpointRouteBuilder api) => Endpoints?.Invoke(api);
}

/// <summary>De testmodules <c>A</c> en <c>B</c> uit de specs.</summary>
public static class TestModules
{
    public static TestModule A(params string[] dependsOn) => new("A", dependsOn);

    public static TestModule B(params string[] dependsOn) => new("B", dependsOn);
}
