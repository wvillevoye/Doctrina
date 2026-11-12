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
/// Testmodule <c>Sample</c> uit de specs: endpoint <c>GET samples</c>, pagina <c>/sample</c>, een openbaar
/// navigatie-item, een item dat <c>Sample.Manage</c> vereist en een eigen
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
        NavItems.Add(new NavItem(ManageNavTitle, "/sample/beheer", "Sample.Manage", Order: 20));
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

    /// <summary>Fout met vertalingen (nl/en) in <c>Resources/SampleErrors.resx</c>.</summary>
    public static Error ItemNotFound { get; } = Error.NotFound("Sample.Item.NotFound", "Item bestaat niet.");

    /// <summary>Fout zonder vertalingen: de titel valt terug op de melding.</summary>
    public static Error Untranslated { get; } = Error.NotFound("Sample.Item.Untranslated", "Item not found");

    public const string ManagePageTitle = "Voorbeeldbeheer";

    public override void MapEndpoints(IEndpointRouteBuilder api)
    {
        // Zonder autorisatiemetadata: valt onder "standaard beveiligd" van de /api/v1-groep.
        api.MapGet("samples", () => Samples);
        api.MapGet("samples/beheer", () => Samples).RequirePermission("Sample.Manage");
        api.MapGet("samples/ontbreekt", () => Result<string>.Failure(ItemNotFound).ToHttpResult());
        api.MapGet("samples/onvertaald", () => Result.Failure(Untranslated).ToHttpResult());
    }
}
