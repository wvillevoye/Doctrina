namespace Doctrina.Core.Modules;

/// <summary>Afspraak voor health checks van modules (design D2).</summary>
public static class ModuleHealthChecks
{
    /// <summary>
    /// Tag die elke module-health check krijgt; de host groepeert de readiness-resultaten per module op basis van
    /// de tag <c>module:&lt;Name&gt;</c> (zie <see cref="TagFor"/>).
    /// </summary>
    public const string Tag = "module";

    /// <summary>Tag voor de health checks van één module.</summary>
    /// <param name="moduleName">De <see cref="IModule.Name"/>.</param>
    public static string TagFor(string moduleName) => $"{Tag}:{moduleName}";
}
