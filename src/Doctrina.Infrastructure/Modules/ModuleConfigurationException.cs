namespace Doctrina.Infrastructure.Modules;

/// <summary>
/// The module configuration is invalid; contains all errors found at once, so you can fix them in one go
/// (design D2).
/// </summary>
public sealed class ModuleConfigurationException : Exception
{
    public ModuleConfigurationException(IReadOnlyList<string> errors)
        : base(FormatMessage(errors)) => Errors = errors;

    public ModuleConfigurationException()
        : this([])
    {
    }

    public ModuleConfigurationException(string message)
        : base(message) => Errors = [message];

    public ModuleConfigurationException(string message, Exception innerException)
        : base(message, innerException) => Errors = [message];

    /// <summary>The individual errors.</summary>
    public IReadOnlyList<string> Errors { get; }

    private static string FormatMessage(IReadOnlyList<string> errors) =>
        "De moduleconfiguratie is ongeldig:" + string.Concat(errors.Select(e => Environment.NewLine + "- " + e));
}
