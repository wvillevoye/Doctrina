using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Lti.Tests;

// Task 3.1 of add-lti-platform: the module is set up and migrates into its own schema.
public sealed class DeModuleMigreertNaarEenEigenSchema
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LegeDatabaseKrijgtHetSchemaLti()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var tables = await db.Database.QueryAsync(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'lti' ORDER BY TABLE_NAME", Ct);

        Assert.Equal(["__EFMigrationsHistory", "Keys", "Tools"], tables.Select(r => r[0]));
    }

    [Fact]
    public void ModuleHeeftNaamAfhankelijkhedenPermissieEnMenuItem()
    {
        var module = new LtiModule();

        Assert.Equal("Lti", module.Name);
        Assert.Equal(["Academic", "Content"], module.DependsOn);
        Assert.Equal([LtiPermissions.Manage], module.Permissions.Select(p => p.Name));
        Assert.Equal("/lti/tools", Assert.Single(module.Navigation).Href);
        Assert.Equal(LtiPermissions.Manage, module.Navigation.Single().RequiredPermission);
    }
}
