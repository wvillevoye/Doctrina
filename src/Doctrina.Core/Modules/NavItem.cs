namespace Doctrina.Core.Modules;

/// <summary>An item in the main menu, supplied by a module.</summary>
/// <param name="Title">Visible text (English).</param>
/// <param name="Href">Relative URL of the page, e.g. <c>/academic/cursussen</c>.</param>
/// <param name="RequiredPermission">
/// Permission the user needs to see the item; <see langword="null"/> = visible to everyone.
/// </param>
/// <param name="Order">Sort order in the menu (low first).</param>
/// <param name="Icon">
/// Optional name of a Lucide icon from the embedded icon set of Doctrina.UI (e.g. <c>book-open</c>); an unknown name is a
/// developer error. <see langword="null"/> = no icon.
/// </param>
public sealed record NavItem(string Title, string Href, string? RequiredPermission = null, int Order = 0, string? Icon = null);
