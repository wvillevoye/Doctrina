namespace Doctrina.Core.Modules;

/// <summary>Een item in het hoofdmenu, aangeleverd door een module.</summary>
/// <param name="Title">Zichtbare tekst (Nederlands).</param>
/// <param name="Href">Relatieve URL van de pagina, bv. <c>/academic/cursussen</c>.</param>
/// <param name="RequiredPermission">
/// Permissie die de gebruiker moet hebben om het item te zien; <see langword="null"/> = zichtbaar voor iedereen.
/// </param>
/// <param name="Order">Sorteervolgorde in het menu (laag eerst).</param>
public sealed record NavItem(string Title, string Href, string? RequiredPermission = null, int Order = 0);
