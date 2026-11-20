using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace Doctrina.Architecture.Tests.Rules;

/// <summary>
/// Rule "modules don't reference each other's implementation" (house rule 2, spec module-system). A module consists of
/// <c>Doctrina.Modules.&lt;Name&gt;</c> (implementation) and <c>Doctrina.Modules.&lt;Name&gt;.Contracts</c>.
/// </summary>
internal static class ModuleBoundaries
{
    public const string Prefix = "Doctrina.Modules.";
    public const string ContractsSuffix = ".Contracts";

    /// <summary>Module name of a project or assembly, or <see langword="null"/> if it is not a module.</summary>
    public static string? ModuleOf(string projectName) =>
        projectName.StartsWith(Prefix, StringComparison.Ordinal)
            ? projectName[Prefix.Length..] is var rest && rest.EndsWith(ContractsSuffix, StringComparison.Ordinal)
                ? rest[..^ContractsSuffix.Length]
                : rest
            : null;

    public static bool IsImplementation(string projectName) =>
        ModuleOf(projectName) is not null && !projectName.EndsWith(ContractsSuffix, StringComparison.Ordinal);

    /// <summary>Error message if <paramref name="from"/> references the implementation of another module.</summary>
    public static string? Check(string from, string to)
    {
        if (!IsImplementation(from) || !IsImplementation(to) || ModuleOf(from) == ModuleOf(to))
        {
            return null;
        }

        return $"{from} verwijst naar de implementatie van module '{ModuleOf(to)}' ({to}); "
            + $"verwijs alleen naar {Prefix}{ModuleOf(to)}{ContractsSuffix} of gebruik events.";
    }

    /// <summary>Implementation projects of modules under <paramref name="modulesRoot"/>.</summary>
    public static IReadOnlyList<string> FindImplementationProjects(string modulesRoot) =>
        Directory.Exists(modulesRoot)
            ? [.. Directory.EnumerateFiles(modulesRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(p => IsImplementation(Path.GetFileNameWithoutExtension(p)))
                .Order(StringComparer.Ordinal)]
            : [];

    /// <summary>Violations at project level (<c>ProjectReference</c> in the <c>.csproj</c> files).</summary>
    public static IReadOnlyList<string> ProjectViolations(IEnumerable<string> projectFiles)
    {
        var violations = new List<string>();
        foreach (var project in projectFiles)
        {
            var from = Path.GetFileNameWithoutExtension(project);
            var references = XDocument.Load(project).Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => (string?)e.Attribute("Include"))
                .Where(i => !string.IsNullOrEmpty(i))
                .Select(i => Path.GetFileNameWithoutExtension(i!.Replace('\\', Path.DirectorySeparatorChar)));
            violations.AddRange(references.Select(to => Check(from, to)).OfType<string>());
        }

        return violations;
    }

    /// <summary>Violations at assembly level (references in the compiled metadata).</summary>
    public static IReadOnlyList<string> AssemblyViolations(IEnumerable<string> assemblyFiles)
    {
        var violations = new List<string>();
        foreach (var file in assemblyFiles)
        {
            using var stream = File.OpenRead(file);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            var from = metadata.GetString(metadata.GetAssemblyDefinition().Name);
            var references = metadata.AssemblyReferences
                .Select(h => metadata.GetString(metadata.GetAssemblyReference(h).Name));
            violations.AddRange(references.Select(to => Check(from, to)).OfType<string>());
        }

        return violations;
    }
}
