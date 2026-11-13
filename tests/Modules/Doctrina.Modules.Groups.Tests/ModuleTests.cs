namespace Doctrina.Modules.Groups.Tests;

// Smoke test of the test setup (tasks 2.1, 2.2): the module migrates into its own schema and the fake directory works.
public sealed class DeModuleMigreertNaarEenEigenSchema
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LegeDatabaseKrijgtHetSchemaGroups()
    {
        await using var db = await GroupsDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var student = directory.Add("I6000001");

        var tables = await db.Database.QueryAsync(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'groups' ORDER BY TABLE_NAME", Ct);

        Assert.Equal(["__EFMigrationsHistory", "GroupMembers", "Groups", "GroupSets"], tables.Select(r => r[0]));
        Assert.Equal(student.PersonId, await directory.CurrentPersonIdAsync(FakeDirectory.User(student.PersonId), Ct));
        Assert.True((await directory.FindOfferingAsync(directory.OfferingId, Ct)).IsSuccess);
    }
}
