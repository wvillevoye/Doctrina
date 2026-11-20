using Doctrina.Infrastructure.Persistence;
using Doctrina.TestKit.Modules.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.TestKit.Modules;

/// <summary>Second test module with its own database (schema <c>notes</c>), for the scenarios with two modules.</summary>
public sealed class NotesModule() : TestModule(ModuleName)
{
    public const string ModuleName = "Notes";

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);
        services.AddModuleDbContext<NotesDbContext>(configuration, ModuleName);
    }
}
