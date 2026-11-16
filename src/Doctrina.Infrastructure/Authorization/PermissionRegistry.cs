using Doctrina.Core.Modules;
using Microsoft.Extensions.Configuration;

namespace Doctrina.Infrastructure.Authorization;

/// <summary>
/// De permissies van de ingeschakelde modules en de koppeling van <c>roles</c>-waarden aan permissies uit
/// <c>Authorization:RolePermissions</c> (design D8, spec access-control). Gebruikersrollen (<c>admin</c>,
/// <c>employee</c>, …) en systeempermissies (<c>doctrina.import</c>, …) gebruiken dezelfde koppeling.
/// </summary>
public sealed class PermissionRegistry
{
    /// <summary>Configuratiesectie met de koppeling rol → permissies.</summary>
    public const string RolePermissionsSection = "Authorization:RolePermissions";

    /// <summary>Waarde die alle permissies van ingeschakelde modules geeft.</summary>
    public const string Wildcard = "*";

    private readonly Dictionary<string, Permission> _byName;
    private readonly Dictionary<string, IReadOnlyList<string>> _byRole;

    private PermissionRegistry(Dictionary<string, Permission> byName, Dictionary<string, IReadOnlyList<string>> byRole)
    {
        _byName = byName;
        _byRole = byRole;
        All = [.. byName.Values.OrderBy(p => p.Name, StringComparer.Ordinal)];
    }

    /// <summary>Alle permissies van de ingeschakelde modules, gesorteerd op naam.</summary>
    public IReadOnlyList<Permission> All { get; }

    /// <summary>De permissie met deze naam, of <see langword="null"/>.</summary>
    public Permission? Find(string name) => _byName.GetValueOrDefault(name);

    /// <summary>De vereniging van de permissies van alle <paramref name="roles"/>; onbekende rollen geven niets.</summary>
    public IReadOnlySet<string> PermissionsFor(IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var result = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            if (_byRole.TryGetValue(role, out var permissions))
            {
                result.UnionWith(permissions);
            }
        }

        return result;
    }

    /// <summary>Leest <see cref="RolePermissionsSection"/>: per rol een lijst permissienamen (of <see cref="Wildcard"/>).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadMapping(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetSection(RolePermissionsSection).GetChildren().ToDictionary(
            role => role.Key,
            role => (IReadOnlyList<string>)[.. role.GetChildren().Select(p => p.Value?.Trim()).Where(p => !string.IsNullOrEmpty(p)).Cast<string>()],
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Bouwt het register en voegt fouten toe aan <paramref name="errors"/>: permissie zonder modulenaam als prefix,
    /// dubbele permissienaam, en een koppeling naar een onbekende permissie.
    /// </summary>
    internal static PermissionRegistry Create(
        IReadOnlyList<IModule> enabled,
        IReadOnlyDictionary<string, IReadOnlyList<string>> mapping,
        List<string> errors,
        IReadOnlySet<string>? ignoredPermissions = null)
    {
        var byName = new Dictionary<string, Permission>(StringComparer.Ordinal);
        var declaredBy = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var module in enabled)
        {
            foreach (var permission in module.Permissions)
            {
                if (!permission.Name.StartsWith(module.Name + ".", StringComparison.Ordinal))
                {
                    errors.Add($"Permissie '{permission.Name}' van module '{module.Name}' moet beginnen met '{module.Name}.'.");
                }

                byName.TryAdd(permission.Name, permission);
                if (!declaredBy.TryGetValue(permission.Name, out var modules))
                {
                    declaredBy[permission.Name] = modules = [];
                }

                modules.Add(module.Name);
            }
        }

        foreach (var (name, modules) in declaredBy.Where(d => d.Value.Count > 1))
        {
            errors.Add($"Permissie '{name}' wordt door meerdere modules gedeclareerd: {string.Join(", ", modules)}.");
        }

        var byRole = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (role, permissions) in mapping)
        {
            foreach (var unknown in permissions.Where(p => p != Wildcard && !byName.ContainsKey(p) && ignoredPermissions?.Contains(p) != true))
            {
                errors.Add($"Rol '{role}' in {RolePermissionsSection} verwijst naar onbekende permissie '{unknown}'.");
            }

            byRole[role] = permissions.Contains(Wildcard)
                ? [.. byName.Keys]
                : [.. permissions.Where(byName.ContainsKey)];
        }

        return new PermissionRegistry(byName, byRole);
    }
}
