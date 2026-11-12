namespace Doctrina.UI.Components;

/// <summary>Varianten van <see cref="DcButton"/> (design D7).</summary>
public enum DcButtonVariant
{
    /// <summary>Hoofdactie van een scherm, in de accentkleur.</summary>
    Primary,

    /// <summary>Gewone actie, met rand.</summary>
    Secondary,

    /// <summary>Minder belangrijke actie of icoonknop, zonder rand.</summary>
    Ghost,

    /// <summary>Onomkeerbare actie, zoals verwijderen.</summary>
    Danger,
}
