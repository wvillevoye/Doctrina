namespace Doctrina.Core.Results;

/// <summary>
/// Vertaalde titels voor foutcodes, in de taal van de gebruiker. Abstractie zodat ook de componentenbibliotheek
/// (<c>DcResult</c>) fouten kan tonen zonder van de infrastructuur af te hangen (design D8 van add-design-system).
/// </summary>
public interface IErrorTitles
{
    /// <summary>De vertaling van <paramref name="code"/>, of <paramref name="fallback"/> als die er niet is.</summary>
    string For(string code, string fallback);
}
