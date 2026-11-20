namespace Doctrina.UI.Theming;

/// <summary>Theme choice of the user (spec theming, design D3).</summary>
public enum DcTheme
{
    /// <summary>Follows the device's system setting (default).</summary>
    System,

    /// <summary>Always light.</summary>
    Light,

    /// <summary>Always dark.</summary>
    Dark,
}

/// <summary>Cookie, endpoint and attribute values of the theme choice.</summary>
public static class DcThemes
{
    /// <summary>Functional cookie with the choice <c>light</c> or <c>dark</c>; without a cookie <see cref="DcTheme.System"/> applies.</summary>
    public const string CookieName = "doctrina.theme";

    /// <summary>Form endpoint that saves the choice (POST, antiforgery, <c>theme</c> + <c>returnUrl</c>).</summary>
    public const string EndpointPath = "/theme";

    /// <summary>Value for <c>data-dc-theme</c> and the form field <c>theme</c>.</summary>
    public static string ToValue(this DcTheme theme) => theme switch
    {
        DcTheme.Light => "light",
        DcTheme.Dark => "dark",
        _ => "system",
    };

    /// <summary>Display name for the theme choice.</summary>
    public static string ToDisplayName(this DcTheme theme) => theme switch
    {
        DcTheme.Light => "Light",
        DcTheme.Dark => "Dark",
        _ => "System",
    };

    /// <summary>Reads a value from the cookie or form; unknown or empty becomes <see langword="null"/>.</summary>
    public static DcTheme? TryParse(string? value) => value switch
    {
        "light" => DcTheme.Light,
        "dark" => DcTheme.Dark,
        "system" => DcTheme.System,
        _ => null,
    };
}
