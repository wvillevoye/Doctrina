using Doctrina.Core.Modules;
using Doctrina.Core.Results;
using Doctrina.Infrastructure.Api;
using Doctrina.Infrastructure.Authorization;
using Doctrina.Infrastructure.Persistence;
using Doctrina.TestKit.Modules.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.TestKit.Modules;

/// <summary>
/// Test module <c>Sample</c> from the specs: endpoint <c>GET samples</c>, page <c>/sample</c>, a public
/// navigation item, an item that requires <c>Sample.Manage</c> and its own
/// database (schema <c>sample</c>, <see cref="SampleDbContext"/>).
/// </summary>
public sealed class SampleModule : TestModule
{
    public const string ModuleName = "Sample";
    public const string PageTitle = "Voorbeeldmodule";
    public const string NavTitle = "Voorbeeld";
    public const string ManageNavTitle = "Voorbeeldbeheer";

    public SampleModule()
        : base(ModuleName)
    {
        NavItems.Add(new NavItem(NavTitle, "/sample", Order: 10));
        NavItems.Add(new NavItem(ManageNavTitle, "/sample/manage", "Sample.Manage", Order: 20));
        DeclaredPermissions.Add(new Permission("Sample.Read", "Voorbeelden bekijken"));
        DeclaredPermissions.Add(new Permission("Sample.Manage", "Voorbeelden beheren"));
    }

    public static IReadOnlyList<string> Samples { get; } = ["een", "twee"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);
        services.AddModuleDbContext<SampleDbContext>(configuration, ModuleName);
        services.AddErrorTitles("Doctrina.TestKit.Modules.Resources.SampleErrors", typeof(SampleModule).Assembly);
    }

    /// <summary>Error with translations (nl/en) in <c>Resources/SampleErrors.resx</c>.</summary>
    public static Error ItemNotFound { get; } = Error.NotFound("Sample.Item.NotFound", "Item bestaat niet.");

    /// <summary>Error without translations: the title falls back to the message.</summary>
    public static Error Untranslated { get; } = Error.NotFound("Sample.Item.Untranslated", "Item not found");

    public const string ManagePageTitle = "Voorbeeldbeheer";

    // A standard API under its own prefix (change add-ooapi-interop): same bearer-only protection as /api/v1.
    public override void MapOoapiEndpoints(IEndpointRouteBuilder ooapi) => ooapi.MapGet("samples", () => Samples);

    public override void MapEndpoints(IEndpointRouteBuilder api)
    {
        // Without authorization metadata: falls under "secured by default" of the /api/v1 group.
        api.MapGet("samples", () => Samples);
        api.MapGet("samples/beheer", () => Samples).RequirePermission("Sample.Manage");
        api.MapGet("samples/ontbreekt", () => Result<string>.Failure(ItemNotFound).ToHttpResult());
        api.MapGet("samples/onvertaald", () => Result.Failure(Untranslated).ToHttpResult());
    }
}
