using System.Xml.Linq;
using Doctrina.Architecture.Tests.Rules;
using Doctrina.Core.Results;
using NetArchTest.Rules;

// Tests volgen spec openspec/specs/module-system/spec.md
// (requirement "Modulegrenzen worden tijdens de build bewaakt") en taak 4.1.
namespace Doctrina.Architecture.Tests;

public sealed class ModulegrenzenWordenTijdensDeBuildBewaakt
{
    private static string FixtureProject(string module, string project) =>
        Path.Combine(RepoPaths.FixtureModules, module, project, project + ".csproj");

    [Fact]
    public void VerwijzingNaarDeImplementatieVanEenAndereModuleLaatDeTestsFalen()
    {
        var projectViolations = ModuleBoundaries.ProjectViolations([FixtureProject("Alpha", "Doctrina.Modules.Alpha")]);
        var assemblyViolations = ModuleBoundaries.AssemblyViolations([RepoPaths.OutputAssembly("Doctrina.Modules.Alpha")]);

        foreach (var violations in new[] { projectViolations, assemblyViolations })
        {
            var violation = Assert.Single(violations);
            Assert.Contains("Doctrina.Modules.Alpha", violation, StringComparison.Ordinal);
            Assert.Contains("Doctrina.Modules.Beta", violation, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void VerwijzingNaarEenContractsProjectIsToegestaan()
    {
        Assert.Empty(ModuleBoundaries.ProjectViolations([FixtureProject("Gamma", "Doctrina.Modules.Gamma")]));
        Assert.Empty(ModuleBoundaries.AssemblyViolations([RepoPaths.OutputAssembly("Doctrina.Modules.Gamma")]));
    }

    [Fact]
    public void ProductmodulesVerwijzenNietNaarElkaarsImplementatie()
    {
        var projects = ModuleBoundaries.FindImplementationProjects(RepoPaths.ProductModules);
        var assemblies = projects.Select(p => RepoPaths.OutputAssembly(Path.GetFileNameWithoutExtension(p))).ToList();

        Assert.All(assemblies, a => Assert.True(File.Exists(a), $"{Path.GetFileName(a)} ontbreekt: Doctrina.Host moet naar elke module verwijzen (design D1)."));
        Assert.Empty(ModuleBoundaries.ProjectViolations(projects));
        Assert.Empty(ModuleBoundaries.AssemblyViolations(assemblies));
    }
}

public sealed class DoctrinaCoreBevatGeenInfrastructuur
{
    private static readonly string[] Forbidden =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore.Authentication",
        "Microsoft.AspNetCore.Identity",
        "Microsoft.AspNetCore.Components",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.Data.SqlClient",
        "Doctrina.Infrastructure",
        "Doctrina.UI",
        "Doctrina.Host",
        "Doctrina.Modules",
    ];

    [Fact]
    public void CoreHangtNietAfVanEfCoreAuthenticatieOfUi()
    {
        var result = Types.InAssembly(typeof(Result).Assembly).ShouldNot().HaveDependencyOnAny(Forbidden).GetResult();

        Assert.True(result.IsSuccessful, "Afhankelijk van verboden namespaces: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void CoreGebruiktGeenNuGetPackagesEnAlleenHetAspNetCoreFramework()
    {
        var project = XDocument.Load(RepoPaths.Project("src", "Doctrina.Core", "Doctrina.Core.csproj"));
        string[] Items(string name) => [.. project.Descendants().Where(e => e.Name.LocalName == name).Select(e => (string?)e.Attribute("Include") ?? string.Empty)];

        Assert.Empty(Items("PackageReference"));
        Assert.Empty(Items("ProjectReference"));
        Assert.Equal(["Microsoft.AspNetCore.App"], Items("FrameworkReference"));
    }
}

public sealed class ModulesGebruikenGeenRuweBootstrapClasses
{
    [Fact]
    public void RuweBootstrapClassInEenModuleWordtHerkend()
    {
        var violations = RawBootstrap.Violations(RepoPaths.FixtureModules);

        // Alleen de foute fixture (Alpha) wordt gemeld; Gamma gebruikt dc-classes.
        Assert.Collection(
            violations,
            v => Assert.Contains("RuweBootstrap.razor:2: ruwe Bootstrap-class(es) container, mt-3", v, StringComparison.Ordinal),
            v => Assert.Contains("RuweBootstrap.razor:3: ruwe Bootstrap-class(es) btn, btn-primary", v, StringComparison.Ordinal));
    }

    [Fact]
    public void ProductmodulesBevattenGeenRuweBootstrapClasses() =>
        Assert.Empty(RawBootstrap.Violations(RepoPaths.ProductModules));
}
