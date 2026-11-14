using Doctrina.Core.Modules;
using Doctrina.Infrastructure.Api;
using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Application;
using Doctrina.Modules.Content.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Modules.Content;

/// <summary>The Content module: sections with pages, files and links in a course offering (change add-content).</summary>
public sealed class ContentModule : IModule
{
    public const string ModuleName = "Content";

    public string Name => ModuleName;

    public IReadOnlyCollection<string> DependsOn => ["Academic"];

    public IReadOnlyCollection<Permission> Permissions =>
    [
        new(ContentPermissions.Manage, "Manage the content of every course offering"),
        new(ContentPermissions.Read, "Read the published content of every course offering"),
    ];

    // No menu item: content is reached from the page of a course offering.
    public IReadOnlyCollection<NavItem> Navigation => [];

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ContentDbContext>(configuration, Name);
        services.AddErrorTitles("Doctrina.Modules.Content.Resources.ContentErrors", typeof(ContentModule).Assembly);
        services.AddOptions<ContentFileOptions>().BindConfiguration(ContentFileOptions.Section);
        services.AddSingleton<ContentFileStore>();
        services.AddScoped<ContentService>();
        services.AddScoped<ContentAccess>();
        services.AddOfferingPageSection<Components.ContentSectionList>(order: 10);
        services.AddHealthChecks().AddCheck<ContentHealthCheck>("content", tags: [ModuleHealthChecks.TagFor(Name)]);
    }

    // No API in this change (proposal: UI only).
    public void MapEndpoints(IEndpointRouteBuilder api)
    {
    }
}

/// <summary>Permissions of the Content module.</summary>
public static class ContentPermissions
{
    public const string Manage = "Content.Manage";
    public const string Read = "Content.Read";
}
