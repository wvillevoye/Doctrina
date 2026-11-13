namespace Doctrina.Modules.Academic.Tests;

// Smoke test of the test setup (task 1.2): the module migrates an empty database into its own schema.
public sealed class DeModuleMigreertNaarEenEigenSchema
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LegeDatabaseKrijgtHetSchemaAcademic()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);

        var tables = await db.Database.QueryAsync(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'academic' ORDER BY TABLE_NAME", Ct);

        Assert.Equal(
            ["__EFMigrationsHistory", "AcademicSessions", "Associations", "CourseOfferings", "Courses", "Organizations", "PersonLogins", "Persons"],
            tables.Select(r => r[0]));
    }
}
