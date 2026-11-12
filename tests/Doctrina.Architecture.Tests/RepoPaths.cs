namespace Doctrina.Architecture.Tests;

/// <summary>Paden in de repository, gevonden vanaf de uitvoermap van de tests.</summary>
internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    /// <summary><c>src/Modules</c>: de productmodules.</summary>
    public static string ProductModules => Path.Combine(Root, "src", "Modules");

    /// <summary>Opzettelijk foute en goede fixture-modules voor de tests van de regels zelf.</summary>
    public static string FixtureModules => Path.Combine(Root, "tests", "Doctrina.Architecture.Tests.Fixtures", "Modules");

    public static string Project(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>Gecompileerde assembly naast de tests (via projectverwijzingen gekopieerd).</summary>
    public static string OutputAssembly(string name) => Path.Combine(AppContext.BaseDirectory, name + ".dll");

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
