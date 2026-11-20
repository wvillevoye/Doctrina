namespace Doctrina.Core.Modules;

/// <summary>Convention for module health checks (design D2).</summary>
public static class ModuleHealthChecks
{
    /// <summary>
    /// Tag that every module health check receives; the host groups the readiness results per module based on
    /// the tag <c>module:&lt;Name&gt;</c> (see <see cref="TagFor"/>).
    /// </summary>
    public const string Tag = "module";

    /// <summary>Tag for the health checks of one module.</summary>
    /// <param name="moduleName">The <see cref="IModule.Name"/>.</param>
    public static string TagFor(string moduleName) => $"{Tag}:{moduleName}";
}
