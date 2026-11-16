namespace Doctrina.UI.Components;

/// <summary>Ernst van een melding (<see cref="DcAlert"/>) of soort label (<see cref="DcBadge"/>).</summary>
public enum DcSeverity
{
    /// <summary>Neutrale informatie.</summary>
    Info,

    /// <summary>Iets is gelukt.</summary>
    Success,

    /// <summary>Let op, maar het werk kan door.</summary>
    Warning,

    /// <summary>Fout of onomkeerbaar.</summary>
    Danger,
}
