using Doctrina.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Doctrina.TestKit.Modules;

/// <summary>Marker that a module registers in <see cref="IModule.ConfigureServices"/>, to see the start order.</summary>
public sealed record InitializedModule(string Name);

/// <summary>Configurable test module (design D10): name, dependencies, navigation, permissions and health check.</summary>
public class TestModule(string name, params string[] dependsOn) : IModule
{
    public string Name => name;

    public IReadOnlyCollection<string> DependsOn => dependsOn;

    public List<NavItem> NavItems { get; } = [];

    public List<Permission> DeclaredPermissions { get; } = [];

    /// <summary>Result of this module's health check; <see langword="null"/> = no health check.</summary>
    public HealthStatus? Health { get; init; }

    /// <summary>Extra registrations of this module, e.g. event handlers; only run if the module is enabled.</summary>
    public Action<IServiceCollection>? Services { get; init; }

    /// <summary>Extra endpoints under <c>/api/v1</c>; only mapped if the module is enabled.</summary>
    public Action<IEndpointRouteBuilder>? Endpoints { get; init; }

    /// <summary>Extra endpoints under <c>/ooapi/v5</c>; only mapped if the module is enabled.</summary>
    public Action<IEndpointRouteBuilder>? OoapiEndpoints { get; init; }

    /// <summary>Extra browser endpoints under <c>/{name}</c>; only mapped if the module is enabled.</summary>
    public Action<IEndpointRouteBuilder>? BrowserEndpoints { get; init; }

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

    public virtual void MapBrowserEndpoints(IEndpointRouteBuilder app) => BrowserEndpoints?.Invoke(app);

    public virtual void MapOoapiEndpoints(IEndpointRouteBuilder ooapi) => OoapiEndpoints?.Invoke(ooapi);
}

/// <summary>The test modules <c>A</c> and <c>B</c> from the specs.</summary>
public static class TestModules
{
    public static TestModule A(params string[] dependsOn) => new("A", dependsOn);

    public static TestModule B(params string[] dependsOn) => new("B", dependsOn);
}
