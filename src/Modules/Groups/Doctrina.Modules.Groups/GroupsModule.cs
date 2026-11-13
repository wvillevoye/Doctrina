using Doctrina.Core.Modules;
using Doctrina.Infrastructure.Api;
using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Groups.Application;
using Doctrina.Modules.Groups.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Modules.Groups;

/// <summary>The Groups module: group sets and groups in a course offering (change add-groups).</summary>
public sealed class GroupsModule : IModule
{
    public const string ModuleName = "Groups";

    public string Name => ModuleName;

    public IReadOnlyCollection<string> DependsOn => ["Academic"];

    public IReadOnlyCollection<Permission> Permissions =>
        [new(GroupsPermissions.Manage, "Manage the groups of every course offering")];

    // No menu item: groups are reached from the page of a course offering.
    public IReadOnlyCollection<NavItem> Navigation => [];

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<GroupsDbContext>(configuration, Name);
        services.AddErrorTitles("Doctrina.Modules.Groups.Resources.GroupsErrors", typeof(GroupsModule).Assembly);
        services.AddScoped<GroupService>();
        services.AddScoped<GroupsAccess>();
        services.AddOfferingPageSection<Components.MyGroupsSection>(order: 20);
        services.AddHealthChecks().AddCheck<GroupsHealthCheck>("groups", tags: [ModuleHealthChecks.TagFor(Name)]);
    }

    // No REST endpoints yet: they come with add-api-and-import.
    public void MapEndpoints(IEndpointRouteBuilder api)
    {
    }
}

/// <summary>Permissions of the Groups module.</summary>
public static class GroupsPermissions
{
    public const string Manage = "Groups.Manage";
}
