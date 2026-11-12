namespace Doctrina.Host;

/// <summary>
/// De statische modulecatalogus (design D1): elke productmodule wordt hier één keer bekendgemaakt. Of een module
/// start, bepaalt <c>Modules:Enabled</c>.
/// </summary>
internal static class KnownModules
{
    public static IServiceCollection AddKnownModules(this IServiceCollection services)
    {
        // Nog geen productmodules; de eerste komt met change add-academic-core, bv.:
        // services.AddDoctrinaModule<AcademicModule>();
        return services;
    }
}
