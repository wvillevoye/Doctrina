namespace Doctrina.UI.Theming;

/// <summary>Themakeuze van de gebruiker (spec theming, design D3).</summary>
public enum DcTheme
{
    /// <summary>Volgt de systeeminstelling van het apparaat (standaard).</summary>
    System,

    /// <summary>Altijd licht.</summary>
    Light,

    /// <summary>Altijd donker.</summary>
    Dark,
}

/// <summary>Cookie, endpoint en attribuutwaarden van de themakeuze.</summary>
public static class DcThemes
{
    /// <summary>Functionele cookie met de keuze <c>light</c> of <c>dark</c>; zonder cookie geldt <see cref="DcTheme.System"/>.</summary>
    public const string CookieName = "doctrina.theme";

    /// <summary>Formulier-endpoint dat de keuze opslaat (POST, antiforgery, <c>theme</c> + <c>returnUrl</c>).</summary>
    public const string EndpointPath = "/thema";

    /// <summary>Waarde voor <c>data-dc-theme</c> en het formulierveld <c>theme</c>.</summary>
    public static string ToValue(this DcTheme theme) => theme switch
    {
        DcTheme.Light => "light",
        DcTheme.Dark => "dark",
        _ => "system",
    };

    /// <summary>Nederlandse naam voor de themakeuze.</summary>
    public static string ToDisplayName(this DcTheme theme) => theme switch
    {
        DcTheme.Light => "Licht",
        DcTheme.Dark => "Donker",
        _ => "Systeem",
    };

    /// <summary>Leest een waarde uit cookie of formulier; onbekend of leeg wordt <see langword="null"/>.</summary>
    public static DcTheme? TryParse(string? value) => value switch
    {
        "light" => DcTheme.Light,
        "dark" => DcTheme.Dark,
        "system" => DcTheme.System,
        _ => null,
    };
}
