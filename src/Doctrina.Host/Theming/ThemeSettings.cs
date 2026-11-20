using Microsoft.Extensions.Options;

namespace Doctrina.Host.Theming;

/// <summary>
/// White-label per installation (spec theming "Een instelling kan het thema aanpassen zonder code", design D4):
/// section <c>Theme</c> in the configuration. See docs/ontwikkelen/vormgeving.md.
/// </summary>
public sealed class ThemeSettings
{
    public const string Section = "Theme";

    /// <summary>Local path of a stylesheet with <c>--dc-*</c> values only, loaded after <c>doctrina.css</c>.</summary>
    public string? StylesheetPath { get; set; }

    /// <summary>Local path of a logo that replaces the "Doctrina" wordmark.</summary>
    public string? LogoPath { get; set; }

    /// <summary>Alternative text of the logo; required if <see cref="LogoPath"/> is set.</summary>
    public string? LogoAlt { get; set; }

    /// <summary>Only paths on this site: starts with a single <c>/</c> (no external domains, spec "zelf gehost").</summary>
    public static bool IsLocalPath(string path) =>
        path.StartsWith('/')
        && !path.StartsWith("//", StringComparison.Ordinal)
        && !path.StartsWith("/\\", StringComparison.Ordinal)
        && !path.Contains("://", StringComparison.Ordinal);
}

/// <summary>Checks <see cref="ThemeSettings"/> at start-up; an error makes start-up fail.</summary>
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
