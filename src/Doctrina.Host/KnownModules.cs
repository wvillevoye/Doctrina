using Doctrina.Infrastructure.Modules;
using Doctrina.Modules.Academic;
using Doctrina.Modules.Groups;

namespace Doctrina.Host;

/// <summary>
/// De statische modulecatalogus (design D1): elke productmodule wordt hier één keer bekendgemaakt. Of een module
/// start, bepaalt <c>Modules:Enabled</c>.
/// </summary>
internal static class KnownModules
{
    public static IServiceCollection AddKnownModules(this IServiceCollection services)
    {
        services.AddDoctrinaModule<AcademicModule>();
        services.AddDoctrinaModule<GroupsModule>();
        return services;
    }
}
