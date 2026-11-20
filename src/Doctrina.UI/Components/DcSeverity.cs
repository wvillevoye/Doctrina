namespace Doctrina.UI.Components;

/// <summary>Severity of an alert (<see cref="DcAlert"/>) or kind of label (<see cref="DcBadge"/>).</summary>
public enum DcSeverity
{
    /// <summary>Neutral information.</summary>
    Info,

    /// <summary>Something succeeded.</summary>
    Success,

    /// <summary>Pay attention, but work can continue.</summary>
    Warning,

    /// <summary>Error or irreversible.</summary>
    Danger,
}
