namespace Doctrina.Core.Modules;

/// <summary>Een permissie die een module declareert; rollen worden er via configuratie aan gekoppeld (design D8).</summary>
/// <param name="Name">Unieke naam met module-prefix, bv. <c>Import.Execute</c>.</param>
/// <param name="Description">Omschrijving voor beheerders (Nederlands).</param>
public sealed record Permission(string Name, string Description);
