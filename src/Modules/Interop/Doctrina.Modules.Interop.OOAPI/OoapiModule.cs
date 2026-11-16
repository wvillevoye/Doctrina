using Doctrina.Core.Modules;
using Doctrina.Modules.Interop.OOAPI.Api;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Modules.Interop.OOAPI;

/// <summary>The Ooapi module: the core model of Academic, read-only, in the format of OOAPI v5 (change add-ooapi-interop).</summary>
public sealed class OoapiModule : IModule
{
    public const string ModuleName = "Ooapi";

    public string Name => ModuleName;

    public IReadOnlyCollection<string> DependsOn => ["Academic"];

    public IReadOnlyCollection<Permission> Permissions => [new(OoapiPermissions.Read, "Read the core model through the OOAPI v5 endpoints")];

    public IReadOnlyCollection<NavItem> Navigation => [];

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
        services.Configure<OoapiOptions>(configuration.GetSection(OoapiOptions.Section));

    // Nothing under /api/v1: this module only offers the standard API under /ooapi/v5.
    public void MapEndpoints(IEndpointRouteBuilder api)
    {
    }

    public void MapOoapiEndpoints(IEndpointRouteBuilder ooapi) => OoapiEndpoints.Map(ooapi);
}

/// <summary>Permissions of the Ooapi module.</summary>
public static class OoapiPermissions
{
    public const string Read = "Ooapi.Read";
}

/// <summary>Settings under <c>Ooapi:</c>: values OOAPI requires that Doctrina does not store (design D5, D9).</summary>
public sealed class OoapiOptions
{
    public const string Section = "Ooapi";

    /// <summary>Language tag of every text, e.g. <c>nl</c> (OOAPI types its strings by language).</summary>
    public string Language { get; set; } = "nl";

    /// <summary>Teaching language (three letters) of every course and offering.</summary>
    public string TeachingLanguage { get; set; } = "nld";

    /// <summary>OOAPI level of every course: bachelor, master, undefined, …</summary>
    public string Level { get; set; } = "undefined";

    public bool ResultExpected { get; set; }

    public string ContactEmail { get; set; } = string.Empty;

    public string Specification { get; set; } = "https://open-education-api.github.io/specification/v5/docs.html";

    public string Documentation { get; set; } = "https://github.com/wvillevoye/Doctrina/blob/main/docs/doctrina-integratie.md";
}
