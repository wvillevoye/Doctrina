using System.Reflection;
using Doctrina.Core.Modules;
using Doctrina.Infrastructure.Authorization;
using Microsoft.Extensions.Configuration;

namespace Doctrina.Infrastructure.Modules;

/// <summary>
/// De ingeschakelde modules in afhankelijkheidsvolgorde, met wat ze aan de host leveren (spec module-system).
/// Een uitgeschakelde module komt hier niet in voor en draagt dus niets bij.
/// </summary>
public sealed class ModuleCatalog
{
    /// <summary>Configuratiesleutel met de namen van de modules die starten.</summary>
    public const string EnabledSection = "Modules:Enabled";

    private ModuleCatalog(IReadOnlyList<IModule> enabled, PermissionRegistry permissions)
    {
        Enabled = enabled;
        Permissions = permissions;
        ComponentAssemblies = [.. enabled.Select(m => m.GetType().Assembly).Distinct()];
        Navigation = [.. enabled.SelectMany(m => m.Navigation).OrderBy(n => n.Order)];
    }

    /// <summary>Ingeschakelde modules; een module staat altijd ná de modules waarvan zij afhankelijk is.</summary>
    public IReadOnlyList<IModule> Enabled { get; }

    /// <summary>Assemblies met de Razor-componenten van de ingeschakelde modules.</summary>
    public IReadOnlyList<Assembly> ComponentAssemblies { get; }

    /// <summary>Navigatie-items van de ingeschakelde modules, gesorteerd op <see cref="NavItem.Order"/>.</summary>
    public IReadOnlyList<NavItem> Navigation { get; }

    /// <summary>Permissies van de ingeschakelde modules en de rolkoppeling (design D8).</summary>
    public PermissionRegistry Permissions { get; }

    /// <summary>Leest <see cref="EnabledSection"/> uit de configuratie.</summary>
    public static IReadOnlyList<string> ReadEnabledNames(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return [.. configuration.GetSection(EnabledSection).GetChildren()
            .Select(c => c.Value?.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .Cast<string>()];
    }

    /// <summary>
    /// Kiest de ingeschakelde modules uit <paramref name="known"/>, controleert namen en afhankelijkheden en sorteert
    /// ze topologisch.
    /// </summary>
    /// <param name="known">Alle modules die de host kent.</param>
    /// <param name="enabledNames">Namen uit <see cref="EnabledSection"/>.</param>
    /// <param name="rolePermissions">Koppeling rol → permissies (<see cref="PermissionRegistry.ReadMapping"/>); leeg als weggelaten.</param>
    /// <exception cref="ModuleConfigurationException">
    /// Bij onbekende namen, ontbrekende afhankelijkheden, cycli, ongeldige of dubbele permissies en koppelingen naar
    /// onbekende permissies — alle fouten tegelijk.
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
        var permissions = PermissionRegistry.Create(ordered, rolePermissions ?? new Dictionary<string, IReadOnlyList<string>>(), errors);

        if (errors.Count > 0)
        {
            throw new ModuleConfigurationException(errors);
        }

        return new ModuleCatalog(ordered, permissions);
    }

    /// <summary>Depth-first topologische sortering; behoudt verder de volgorde uit de configuratie.</summary>
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
