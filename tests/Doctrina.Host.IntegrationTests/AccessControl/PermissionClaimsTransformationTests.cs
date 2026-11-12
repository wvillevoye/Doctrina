using System.Security.Claims;
using Doctrina.Core.Modules;
using Doctrina.Infrastructure.Authentication;
using Doctrina.Infrastructure.Authorization;
using Doctrina.Infrastructure.Modules;
using Doctrina.TestKit.Modules;

// Aanvulling op spec access-control (design D8): alleen Doctrina bepaalt permissies.
namespace Doctrina.Host.IntegrationTests.AccessControl;

public sealed class PermissiesKomenAlleenUitDeRolkoppeling
{
    private static readonly PermissionRegistry Registry = ModuleCatalog.Create(
        [new SampleModule()],
        [SampleModule.ModuleName],
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["employee"] = ["Sample.Read"] }).Permissions;

    [Fact]
    public async Task PermissieclaimUitHetTokenWordtGenegeerd()
    {
        var principal = Principal(new Claim(DoctrinaClaimTypes.Roles, "employee"), new Claim(DoctrinaClaimTypes.Permission, "Sample.Manage"));

        var result = await new PermissionClaimsTransformation(Registry).TransformAsync(principal);

        Assert.Equal(["Sample.Read"], result.FindAll(DoctrinaClaimTypes.Permission).Select(c => c.Value));
    }

    [Fact]
    public async Task TransformatieGebeurtMaarEenKeer()
    {
        var transformation = new PermissionClaimsTransformation(Registry);

        var once = await transformation.TransformAsync(Principal(new Claim(DoctrinaClaimTypes.Roles, "employee")));
        var twice = await transformation.TransformAsync(once);

        Assert.Same(once, twice);
        Assert.Single(twice.FindAll(DoctrinaClaimTypes.Permission));
    }

    [Fact]
    public async Task AnoniemeGebruikerKrijgtGeenPermissies()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await new PermissionClaimsTransformation(Registry).TransformAsync(anonymous);

        Assert.Empty(result.FindAll(DoctrinaClaimTypes.Permission));
    }

    [Fact]
    public void PermissieZonderModuleprefixLaatHetOpstartenMislukken()
    {
        var module = new TestModule("Sample") { DeclaredPermissions = { new Permission("Beheer", "Zonder prefix") } };

        var failure = Assert.Throws<ModuleConfigurationException>(() => ModuleCatalog.Create([module], ["Sample"]));

        Assert.Contains("Permissie 'Beheer' van module 'Sample' moet beginnen met 'Sample.'.", failure.Errors);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test", DoctrinaClaimTypes.Name, DoctrinaClaimTypes.Roles));
}
