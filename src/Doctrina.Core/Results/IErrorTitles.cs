namespace Doctrina.Core.Results;

/// <summary>
/// Titles for error codes in the language of the UI (English, change switch-ui-to-english). An abstraction so the
/// component library (<c>DcResult</c>) can show errors without depending on the infrastructure (design D8 of add-design-system).
/// </summary>
public interface IErrorTitles
{
    /// <summary>The English title for <paramref name="code"/>, or <paramref name="fallback"/> if there is none.</summary>
    string For(string code, string fallback);
}
