using Doctrina.Core.Modules;
using Microsoft.Extensions.Configuration;

namespace Doctrina.Infrastructure.Authorization;

/// <summary>
/// The permissions of the enabled modules and the mapping of <c>roles</c> values to permissions from
/// <c>Authorization:RolePermissions</c> (design D8, spec access-control). User roles (<c>admin</c>,
/// <c>employee</c>, …) and system permissions (<c>doctrina.import</c>, …) use the same mapping.
/// </summary>
public sealed class PermissionRegistry
{
    /// <summary>Configuration section with the role → permissions mapping.</summary>
    public const string RolePermissionsSection = "Authorization:RolePermissions";

    /// <summary>Value that grants all permissions of enabled modules.</summary>
    public const string Wildcard = "*";

    private readonly Dictionary<string, Permission> _byName;
    private readonly Dictionary<string, IReadOnlyList<string>> _byRole;

    private PermissionRegistry(Dictionary<string, Permission> byName, Dictionary<string, IReadOnlyList<string>> byRole)
    {
        _byName = byName;
        _byRole = byRole;
        All = [.. byName.Values.OrderBy(p => p.Name, StringComparer.Ordinal)];
    }

    /// <summary>All permissions of the enabled modules, sorted by name.</summary>
    public IReadOnlyList<Permission> All { get; }

    /// <summary>The permission with this name, or <see langword="null"/>.</summary>
    public Permission? Find(string name) => _byName.GetValueOrDefault(name);

    /// <summary>The union of the permissions of all <paramref name="roles"/>; unknown roles yield nothing.</summary>
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

    /// <summary>Reads <see cref="RolePermissionsSection"/>: per role a list of permission names (or <see cref="Wildcard"/>).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadMapping(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetSection(RolePermissionsSection).GetChildren().ToDictionary(
            role => role.Key,
            role => (IReadOnlyList<string>)[.. role.GetChildren().Select(p => p.Value?.Trim()).Where(p => !string.IsNullOrEmpty(p)).Cast<string>()],
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the registry and adds errors to <paramref name="errors"/>: a permission without the module name as prefix,
    /// a duplicate permission name, and a mapping to an unknown permission.
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
