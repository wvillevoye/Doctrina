using Doctrina.Core.Modules;
using Doctrina.Infrastructure.Api;
using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Modules.Lti;

/// <summary>The Lti module: Doctrina as an LTI 1.3 platform, core only (change add-lti-platform).</summary>
public sealed class LtiModule : IModule
{
    public const string ModuleName = "Lti";

    public string Name => ModuleName;

    public IReadOnlyCollection<string> DependsOn => ["Academic", "Content"];

    public IReadOnlyCollection<Permission> Permissions =>
    [
        new(LtiPermissions.Manage, "Register LTI tools and rotate the signing key"),
    ];

    public IReadOnlyCollection<NavItem> Navigation =>
    [
        new("LTI tools", "/lti/tools", LtiPermissions.Manage, Order: 95),
    ];

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<LtiDbContext>(configuration, Name);
        services.AddOptions<LtiOptions>().BindConfiguration(LtiOptions.Section);
        services.AddScoped<KeyService>();
        services.AddScoped<ToolService>();
        services.AddScoped<LaunchService>();
        services.AddScoped<DeepLinkingService>();
        services.AddMemoryCache();
        services.AddHttpClient<JwksClient>(client => client.Timeout = TimeSpan.FromSeconds(5))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<Contracts.ILtiToolCatalog>(sp => sp.GetRequiredService<ToolService>());
        services.AddErrorTitles("Doctrina.Modules.Lti.Resources.LtiErrors", typeof(LtiModule).Assembly);
        services.AddHealthChecks().AddCheck<LtiHealthCheck>("lti", tags: [ModuleHealthChecks.TagFor(Name)]);
    }

    // No REST API in this change; the launch flow is browser traffic (MapBrowserEndpoints).
    public void MapEndpoints(IEndpointRouteBuilder api)
    {
    }

    public void MapBrowserEndpoints(IEndpointRouteBuilder app) => LtiEndpoints.Map(app);
}

/// <summary>Permissions of the Lti module.</summary>
public static class LtiPermissions
{
    public const string Manage = "Lti.Manage";
}
