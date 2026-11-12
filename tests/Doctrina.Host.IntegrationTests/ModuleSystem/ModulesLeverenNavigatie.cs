using System.Security.Claims;
using Bunit;
using Doctrina.Host.Components.Layout;
using Doctrina.Infrastructure.Authentication;
using Doctrina.Infrastructure.Modules;
using Doctrina.TestKit.Modules;
using Microsoft.Extensions.DependencyInjection;

// bUnit-tests voor spec module-system, requirement "Modules leveren navigatie".
namespace Doctrina.Host.IntegrationTests.ModuleSystem;

public sealed class ModulesLeverenNavigatie : IDisposable
{
    private readonly BunitContext _context = new();

    public ModulesLeverenNavigatie() =>
        _context.Services.AddSingleton(ModuleCatalog.Create([new SampleModule()], [SampleModule.ModuleName]));

    [Fact]
    public void NavigatieItemZichtbaarMetPermissie()
    {
        var auth = _context.AddAuthorization();
        auth.SetAuthorized("Beheer Doctrina");
        auth.SetClaims(new Claim(DoctrinaClaimTypes.Permission, "Sample.Manage"));

        var menu = _context.Render<NavMenu>();

        Assert.Contains(menu.FindAll("a"), a => a.TextContent == SampleModule.ManageNavTitle);
    }

    [Fact]
    public void NavigatieItemVerborgenZonderPermissie()
    {
        var auth = _context.AddAuthorization();
        auth.SetAuthorized("Sanne Student");
        auth.SetClaims(new Claim(DoctrinaClaimTypes.Permission, "Sample.Read"));

        var menu = _context.Render<NavMenu>();

        var links = menu.FindAll("a").Select(a => a.TextContent).ToList();
        Assert.DoesNotContain(SampleModule.ManageNavTitle, links);
        Assert.Contains(SampleModule.NavTitle, links);
    }

    public void Dispose() => _context.Dispose();
}
