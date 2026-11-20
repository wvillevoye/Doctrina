namespace Doctrina.UI.Components;

/// <summary>Item in the main menu (<see cref="DcNavMenu"/>). The host decides which items are visible.</summary>
/// <param name="Title">Visible text.</param>
/// <param name="Href">Local path, e.g. <c>/profile</c>.</param>
/// <param name="MatchAll">Only "current page" for exactly the same path (for <c>/</c>); otherwise also for subpaths.</param>
/// <param name="Icon">Optional name of an icon in <see cref="Icons.DcIconSet"/>; decorative, the title always stays visible.</param>
public sealed record DcNavItem(string Title, string Href, bool MatchAll = false, string? Icon = null);
