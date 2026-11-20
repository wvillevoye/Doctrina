using Doctrina.Infrastructure.Modules;
using Doctrina.Modules.Academic;
using Doctrina.Modules.Content;
using Doctrina.Modules.Groups;
using Doctrina.Modules.Interop.OOAPI;
using Doctrina.Modules.Lti;

namespace Doctrina.Host;

/// <summary>
/// The static module catalog (design D1): every product module is made known here once. Whether a module
/// starts is determined by <c>Modules:Enabled</c>.
/// </summary>
internal static class KnownModules
{
    public static IServiceCollection AddKnownModules(this IServiceCollection services)
    {
        services.AddDoctrinaModule<AcademicModule>();
        services.AddDoctrinaModule<GroupsModule>();
        services.AddDoctrinaModule<ContentModule>();
        services.AddDoctrinaModule<LtiModule>();
        services.AddDoctrinaModule<OoapiModule>();
        return services;
    }
}
