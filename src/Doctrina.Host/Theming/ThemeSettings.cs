using Microsoft.Extensions.Options;

namespace Doctrina.Host.Theming;

/// <summary>
/// White-label per installatie (spec theming "Een instelling kan het thema aanpassen zonder code", design D4):
/// sectie <c>Theme</c> in de configuratie. Zie docs/ontwikkelen/vormgeving.md.
/// </summary>
public sealed class ThemeSettings
{
    public const string Section = "Theme";

    /// <summary>Lokaal pad van een stylesheet met alleen <c>--dc-*</c>-waarden, geladen ná <c>doctrina.css</c>.</summary>
    public string? StylesheetPath { get; set; }

    /// <summary>Lokaal pad van een logo dat het woordmerk "Doctrina" vervangt.</summary>
    public string? LogoPath { get; set; }

    /// <summary>Alternatieve tekst van het logo; verplicht als <see cref="LogoPath"/> is gezet.</summary>
    public string? LogoAlt { get; set; }

    /// <summary>Alleen paden op deze site: begint met één <c>/</c> (geen externe domeinen, spec "zelf gehost").</summary>
    public static bool IsLocalPath(string path) =>
        path.StartsWith('/')
        && !path.StartsWith("//", StringComparison.Ordinal)
        && !path.StartsWith("/\\", StringComparison.Ordinal)
        && !path.Contains("://", StringComparison.Ordinal);
}

/// <summary>Controleert <see cref="ThemeSettings"/> bij het opstarten; een fout laat het opstarten mislukken.</summary>
public sealed class ThemeSettingsValidator : IValidateOptions<ThemeSettings>
{
    public ValidateOptionsResult Validate(string? name, ThemeSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (!string.IsNullOrEmpty(options.StylesheetPath) && !ThemeSettings.IsLocalPath(options.StylesheetPath))
        {
            failures.Add($"Theme:StylesheetPath '{options.StylesheetPath}' moet een lokaal pad zijn dat met één '/' begint, bv. /theme/instelling.css.");
        }

        if (!string.IsNullOrEmpty(options.LogoPath))
        {
            if (!ThemeSettings.IsLocalPath(options.LogoPath))
            {
                failures.Add($"Theme:LogoPath '{options.LogoPath}' moet een lokaal pad zijn dat met één '/' begint, bv. /theme/logo.svg.");
            }

            if (string.IsNullOrWhiteSpace(options.LogoAlt))
            {
                failures.Add("Theme:LogoAlt is verplicht als Theme:LogoPath is gezet (alternatieve tekst voor het logo, WCAG 1.1.1).");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
