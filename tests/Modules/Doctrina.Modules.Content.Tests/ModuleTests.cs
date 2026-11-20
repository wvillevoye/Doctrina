global using Doctrina.TestKit.Fakes;

namespace Doctrina.Modules.Content.Tests;

// Smoke test of the test setup (task 2.1): the module migrates into its own schema.
public sealed class DeModuleMigreertNaarEenEigenSchema
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LegeDatabaseKrijgtHetSchemaContent()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);

        var tables = await db.Database.QueryAsync(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'content' ORDER BY TABLE_NAME", Ct);

        Assert.Equal(["__EFMigrationsHistory", "Introductions", "IntroductionTemplates", "Items", "Sections"], tables.Select(r => r[0]));
    }
}
