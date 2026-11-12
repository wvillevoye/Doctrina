namespace Doctrina.Infrastructure.Modules;

/// <summary>
/// De moduleconfiguratie is ongeldig; bevat álle gevonden fouten tegelijk, zodat je ze in één keer kunt oplossen
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

    /// <summary>De afzonderlijke fouten.</summary>
    public IReadOnlyList<string> Errors { get; }

    private static string FormatMessage(IReadOnlyList<string> errors) =>
        "De moduleconfiguratie is ongeldig:" + string.Concat(errors.Select(e => Environment.NewLine + "- " + e));
}
