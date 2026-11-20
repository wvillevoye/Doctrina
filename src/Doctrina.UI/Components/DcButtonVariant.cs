namespace Doctrina.UI.Components;

/// <summary>Variants of <see cref="DcButton"/> (design D7).</summary>
public enum DcButtonVariant
{
    /// <summary>Main action of a screen, in the accent colour.</summary>
    Primary,

    /// <summary>Ordinary action, with a border.</summary>
    Secondary,

    /// <summary>Less important action or icon button, without a border.</summary>
    Ghost,

    /// <summary>Irreversible action, such as delete.</summary>
    Danger,
}
