namespace Doctrina.UI.Tests;

/// <summary>Paden naar de bronbestanden van <c>Doctrina.UI</c>, gevonden vanaf de uitvoermap van de tests.</summary>
internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string Ui => Path.Combine(Root, "src", "Doctrina.UI");

    public static string Styles => Path.Combine(Ui, "Styles");

    public static string Tokens => Path.Combine(Styles, "_tokens.scss");

    /// <summary>Door AspNetCore.SassCompiler gegenereerd tijdens de build van Doctrina.UI.</summary>
    public static string CompiledCss => Path.Combine(Ui, "wwwroot", "css", "doctrina.css");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Doctrina.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Doctrina.slnx niet gevonden boven " + AppContext.BaseDirectory);
    }
}
