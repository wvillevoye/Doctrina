namespace Doctrina.Core.Modules;

/// <summary>A permission a module declares; roles are linked to it through configuration (design D8).</summary>
/// <param name="Name">Unique name with module prefix, e.g. <c>Import.Execute</c>.</param>
/// <param name="Description">Description for administrators (English).</param>
public sealed record Permission(string Name, string Description);
