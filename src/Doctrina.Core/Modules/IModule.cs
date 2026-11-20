using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Core.Modules;

/// <summary>
/// A feature module of Doctrina (proposal §3.4, design D1/D2). Modules are compiled in and enabled through
/// <c>Modules:Enabled</c>; a disabled module contributes nothing to the host.
/// </summary>
/// <remarks>
/// Razor components are taken from the module's assembly. A module adds health checks in
/// <see cref="ConfigureServices"/> with the tag <see cref="ModuleHealthChecks.Tag"/>.
/// </remarks>
public interface IModule
{
    /// <summary>Unique name, as used in <c>Modules:Enabled</c> and as the prefix of permissions (e.g. <c>Academic</c>).</summary>
    string Name { get; }

    /// <summary>Names of the modules that must start earlier and be enabled.</summary>
    IReadOnlyCollection<string> DependsOn { get; }

    /// <summary>Navigation items the host includes in the menu.</summary>
    IReadOnlyCollection<NavItem> Navigation { get; }

    /// <summary>Permissions this module uses; names start with <see cref="Name"/>.</summary>
    IReadOnlyCollection<Permission> Permissions { get; }

    /// <summary>Registers the module's services (DbContext, handlers, health checks, …).</summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">The host's configuration.</param>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps the module's API endpoints.</summary>
    /// <param name="api">
    /// The <c>/api/v1</c> route group: only bearer tokens and default authentication required, so a module cannot bypass
    /// the version prefix and the security.
    /// </param>
    void MapEndpoints(IEndpointRouteBuilder api);

    /// <summary>
    /// Maps the module's browser endpoints (cookie authentication, e.g. a launch or a public JWKS address).
    /// By default a module does nothing.
    /// </summary>
    /// <param name="app">
    /// The group <c>/{module name in lower case}</c>: signed-in required; an endpoint can explicitly make itself
    /// <c>AllowAnonymous()</c>.
    /// </param>
    void MapBrowserEndpoints(IEndpointRouteBuilder app)
    {
    }

    /// <summary>
    /// Maps the endpoints of an external standard API that lives under <c>/ooapi/v5</c> (change add-ooapi-interop).
    /// By default a module does nothing.
    /// </summary>
    /// <param name="ooapi">
    /// The <c>/ooapi/v5</c> route group: like <c>/api/v1</c>, only bearer tokens and default authentication required.
    /// </param>
    void MapOoapiEndpoints(IEndpointRouteBuilder ooapi)
    {
    }
}
