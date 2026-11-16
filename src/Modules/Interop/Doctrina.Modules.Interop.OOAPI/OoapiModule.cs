using Doctrina.Core.Modules;
using Doctrina.Modules.Interop.OOAPI.Api;
using Doctrina.Modules.Interop.OOAPI.Client;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Interop.OOAPI;

/// <summary>The Ooapi module: the core model of Academic as OOAPI v5, read-only (change add-ooapi-interop), and reading an external OOAPI source into it (change add-ooapi-consumer).</summary>
public sealed class OoapiModule : IModule
{
    public const string ModuleName = "Ooapi";

    public string Name => ModuleName;

    public IReadOnlyCollection<string> DependsOn => ["Academic"];

    public IReadOnlyCollection<Permission> Permissions =>
    [
        new(OoapiPermissions.Read, "Read the core model through the OOAPI v5 endpoints"),
        new(OoapiPermissions.Sync, "Start a read of an external OOAPI source"),
    ];

    public IReadOnlyCollection<NavItem> Navigation => [];

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OoapiOptions>().Bind(configuration.GetSection(OoapiOptions.Section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<OoapiOptions>, OoapiOptionsValidator>();
        services.AddHttpClient(OoapiClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SyncGate>();
        services.AddScoped<OoapiSync>();
        services.AddHostedService<OoapiSchedule>();
    }

    // The standard API lives under /ooapi/v5; under /api/v1 there is only the way to start a read of a source.
    public void MapEndpoints(IEndpointRouteBuilder api) => SyncEndpoints.Map(api);

    public void MapOoapiEndpoints(IEndpointRouteBuilder ooapi) => OoapiEndpoints.Map(ooapi);
}

/// <summary>Permissions of the Ooapi module.</summary>
public static class OoapiPermissions
{
    public const string Read = "Ooapi.Read";
    public const string Sync = "Ooapi.Sync";
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

    /// <summary>The OOAPI sources Doctrina reads, by name (change add-ooapi-consumer).</summary>
    public Dictionary<string, OoapiSourceOptions> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string Specification { get; set; } = "https://open-education-api.github.io/specification/v5/docs.html";

    public string Documentation { get; set; } = "https://github.com/wvillevoye/Doctrina/blob/main/docs/doctrina-integratie.md";
}
