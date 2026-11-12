using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Core.Modules;

/// <summary>
/// Een feature-module van Doctrina (proposal §3.4, design D1/D2). Modules worden meegecompileerd en via
/// <c>Modules:Enabled</c> aangezet; een uitgeschakelde module levert niets aan de host.
/// </summary>
/// <remarks>
/// Razor-componenten worden uit de assembly van de module gehaald. Health checks voegt een module toe in
/// <see cref="ConfigureServices"/> met de tag <see cref="ModuleHealthChecks.Tag"/>.
/// </remarks>
public interface IModule
{
    /// <summary>Unieke naam, zoals gebruikt in <c>Modules:Enabled</c> en als prefix van permissies (bv. <c>Academic</c>).</summary>
    string Name { get; }

    /// <summary>Namen van de modules die eerder moeten starten en ingeschakeld moeten zijn.</summary>
    IReadOnlyCollection<string> DependsOn { get; }

    /// <summary>Navigatie-items die de host in het menu opneemt.</summary>
    IReadOnlyCollection<NavItem> Navigation { get; }

    /// <summary>Permissies die deze module gebruikt; namen beginnen met <see cref="Name"/>.</summary>
    IReadOnlyCollection<Permission> Permissions { get; }

    /// <summary>Registreert de services van de module (DbContext, handlers, health checks, …).</summary>
    /// <param name="services">De servicecollectie van de host.</param>
    /// <param name="configuration">De configuratie van de host.</param>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Koppelt de API-endpoints van de module.</summary>
    /// <param name="api">
    /// De <c>/api/v1</c>-routegroep: alleen bearer-tokens en standaard authenticatie vereist, zodat een module het
    /// versieprefix en de beveiliging niet kan omzeilen.
    /// </param>
    void MapEndpoints(IEndpointRouteBuilder api);
}
