namespace Doctrina.UI.Components;

/// <summary>Item in het hoofdmenu (<see cref="DcNavMenu"/>). De host bepaalt welke items zichtbaar zijn.</summary>
/// <param name="Title">Zichtbare tekst.</param>
/// <param name="Href">Lokaal pad, bv. <c>/profiel</c>.</param>
/// <param name="MatchAll">Alleen "huidige pagina" bij exact hetzelfde pad (voor <c>/</c>); anders ook voor subpaden.</param>
public sealed record DcNavItem(string Title, string Href, bool MatchAll = false);
