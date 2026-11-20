namespace Doctrina.Architecture.Tests;

/// <summary>Paths in the repository, found from the output folder of the tests.</summary>
internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    /// <summary><c>src/Modules</c>: the product modules.</summary>
    public static string ProductModules => Path.Combine(Root, "src", "Modules");

    /// <summary>Deliberately wrong and correct fixture modules for testing the rules themselves.</summary>
    public static string FixtureModules => Path.Combine(Root, "tests", "Doctrina.Architecture.Tests.Fixtures", "Modules");

    public static string Project(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>Compiled assembly next to the tests (copied via project references).</summary>
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
