namespace Doctrina.UI.Components;

/// <summary>Variants of <see cref="DcBadge"/>.</summary>
public enum DcBadgeVariant
{
    /// <summary>Grey, without special meaning.</summary>
    Neutral,

    /// <summary>In the accent colour, e.g. "new".</summary>
    Accent,

    /// <summary>Positive, e.g. "active".</summary>
    Success,

    /// <summary>Needs attention, e.g. "draft".</summary>
    Warning,

    /// <summary>Negative, e.g. "blocked".</summary>
    Danger,

    /// <summary>Informational.</summary>
    Info,
}
