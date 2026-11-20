using System.Reflection;
using Doctrina.Core.Modules;
using Doctrina.Infrastructure.Authorization;
using Microsoft.Extensions.Configuration;

namespace Doctrina.Infrastructure.Modules;

/// <summary>
/// The enabled modules in dependency order, with what they contribute to the host (spec module-system).
/// A disabled module does not appear here and so contributes nothing.
/// </summary>
public sealed class ModuleCatalog
{
    /// <summary>Configuration key with the names of the modules that start.</summary>
    public const string EnabledSection = "Modules:Enabled";

    private ModuleCatalog(IReadOnlyList<IModule> enabled, PermissionRegistry permissions)
    {
        Enabled = enabled;
        Permissions = permissions;
        ComponentAssemblies = [.. enabled.Select(m => m.GetType().Assembly).Distinct()];
        Navigation = [.. enabled.SelectMany(m => m.Navigation).OrderBy(n => n.Order)];
    }

    /// <summary>Enabled modules; a module always comes after the modules it depends on.</summary>
    public IReadOnlyList<IModule> Enabled { get; }

    /// <summary>Assemblies with the Razor components of the enabled modules.</summary>
    public IReadOnlyList<Assembly> ComponentAssemblies { get; }

    /// <summary>Navigation items of the enabled modules, sorted by <see cref="NavItem.Order"/>.</summary>
    public IReadOnlyList<NavItem> Navigation { get; }

    /// <summary>Permissions of the enabled modules and the role mapping (design D8).</summary>
    public PermissionRegistry Permissions { get; }

    /// <summary>Reads <see cref="EnabledSection"/> from the configuration.</summary>
    public static IReadOnlyList<string> ReadEnabledNames(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return [.. configuration.GetSection(EnabledSection).GetChildren()
            .Select(c => c.Value?.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .Cast<string>()];
    }

    /// <summary>
    /// Selects the enabled modules from <paramref name="known"/>, checks names and dependencies and sorts
    /// them topologically.
    /// </summary>
    /// <param name="known">All modules the host knows.</param>
    /// <param name="enabledNames">Names from <see cref="EnabledSection"/>.</param>
    /// <param name="rolePermissions">Role → permissions mapping (<see cref="PermissionRegistry.ReadMapping"/>); empty if omitted.</param>
    /// <exception cref="ModuleConfigurationException">
    /// For unknown names, missing dependencies, cycles, invalid or duplicate permissions and mappings to
    /// unknown permissions — all errors at once.
    /// </exception>
    public static ModuleCatalog Create(
        IEnumerable<IModule> known,
        IEnumerable<string> enabledNames,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? rolePermissions = null)
    {
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(enabledNames);

        var errors = new List<string>();
        var knownByName = new Dictionary<string, IModule>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in known)
        {
            if (!knownByName.TryAdd(module.Name, module))
            {
                errors.Add($"Er zijn twee modules met de naam '{module.Name}'.");
            }
        }

        var enabled = new List<IModule>();
        foreach (var name in enabledNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (knownByName.TryGetValue(name, out var module))
            {
                enabled.Add(module);
            }
            else
            {
                var available = knownByName.Count == 0 ? "geen" : string.Join(", ", knownByName.Keys.Order(StringComparer.Ordinal));
                errors.Add($"Onbekende module '{name}' in {EnabledSection}. Bekende modules: {available}.");
            }
        }

        var enabledByName = enabled.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var module in enabled)
        {
            foreach (var dependency in module.DependsOn.Where(d => !enabledByName.ContainsKey(d)))
            {
                errors.Add($"Module '{module.Name}' vereist module '{dependency}', maar '{dependency}' staat niet in {EnabledSection}.");
            }
        }

        var ordered = SortByDependencies(enabled, enabledByName, errors);
        // Permissions of known but disabled modules may stay in the role mapping, so switching a module off doesn't
        // require editing the mapping as well (change add-academic-core).
        var disabledPermissions = knownByName.Values.Except(enabled)
            .SelectMany(m => m.Permissions.Select(p => p.Name))
            .ToHashSet(StringComparer.Ordinal);
        var permissions = PermissionRegistry.Create(ordered, rolePermissions ?? new Dictionary<string, IReadOnlyList<string>>(), errors, disabledPermissions);

        if (errors.Count > 0)
        {
            throw new ModuleConfigurationException(errors);
        }

        return new ModuleCatalog(ordered, permissions);
    }

    /// <summary>Depth-first topological sort; otherwise keeps the order from the configuration.</summary>
    private static List<IModule> SortByDependencies(
        List<IModule> enabled,
        Dictionary<string, IModule> enabledByName,
        List<string> errors)
    {
        var ordered = new List<IModule>(enabled.Count);
        var done = new HashSet<IModule>();
        var path = new List<IModule>();
        var reportedCycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(IModule module)
        {
            if (done.Contains(module))
            {
                return;
            }

            var index = path.IndexOf(module);
            if (index >= 0)
            {
                var cycle = path.Skip(index).Append(module).Select(m => m.Name).ToList();
                var key = string.Join("|", cycle.Skip(1).Order(StringComparer.OrdinalIgnoreCase));
                if (reportedCycles.Add(key))
                {
                    errors.Add($"Cyclische afhankelijkheid tussen modules: {string.Join(" → ", cycle)}.");
                }

                return;
            }

            path.Add(module);
            foreach (var dependency in module.DependsOn)
            {
                if (enabledByName.TryGetValue(dependency, out var dependencyModule))
                {
                    Visit(dependencyModule);
                }
            }

            path.RemoveAt(path.Count - 1);
            if (done.Add(module))
            {
                ordered.Add(module);
            }
        }

        foreach (var module in enabled)
        {
            Visit(module);
        }

        return ordered;
    }
}
