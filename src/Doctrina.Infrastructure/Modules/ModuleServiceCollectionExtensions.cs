using Doctrina.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Doctrina.Infrastructure.Authorization;
using Doctrina.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Doctrina.Infrastructure.Modules;

/// <summary>A module the host knows; whether it starts is determined by <c>Modules:Enabled</c>.</summary>
/// <param name="Module">The module.</param>
public sealed record ModuleRegistration(IModule Module);

/// <summary>Register modules, validate them and wire their contributions to the host (design D1/D2).</summary>
public static class ModuleServiceCollectionExtensions
{
    /// <summary>Makes <paramref name="module"/> known to the host (the static module catalog, design D1).</summary>
    public static IServiceCollection AddDoctrinaModule(this IServiceCollection services, IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return services.AddSingleton(new ModuleRegistration(module));
    }

    /// <summary>Makes module <typeparamref name="TModule"/> known to the host.</summary>
    public static IServiceCollection AddDoctrinaModule<TModule>(this IServiceCollection services)
        where TModule : IModule, new() =>
        services.AddDoctrinaModule(new TModule());

    /// <summary>
    /// Turns on the module system. While the app is being built — after all services, including those of tests, have been
    /// registered and the configuration is final — the <see cref="ModuleCatalog"/> is created and validated and
    /// the services of the enabled modules are registered in dependency order.
    /// </summary>
    /// <exception cref="ModuleConfigurationException">On <c>Build()</c> if the module configuration is invalid.</exception>
    public static IHostBuilder UseDoctrinaModules(this IHostBuilder host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.UseServiceProviderFactory(context =>
        {
            // Strict DI validation in Development and in the tests (design D13).
            var development = context.HostingEnvironment.IsDevelopment() || context.HostingEnvironment.IsTest();
            return new ModuleServiceProviderFactory(
                context.Configuration,
                new ServiceProviderOptions { ValidateScopes = development, ValidateOnBuild = development });
        });
    }

    /// <summary>
    /// Maps the endpoints of the enabled modules to the <c>/api/v1</c> group; each module gets its own
    /// subgroup with its name as OpenAPI tag.
    /// </summary>
    public static IEndpointRouteBuilder MapDoctrinaModules(this IEndpointRouteBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);
        var catalog = api.ServiceProvider.GetRequiredService<ModuleCatalog>();
        foreach (var module in catalog.Enabled)
        {
            module.MapEndpoints(api.MapGroup(string.Empty).WithTags(module.Name));
        }

        return api;
    }

    /// <summary>Maps the endpoints of the enabled modules to the <c>/ooapi/v5</c> group (bearer-only, like <c>/api/v1</c>).</summary>
    public static IEndpointRouteBuilder MapDoctrinaOoapiModules(this IEndpointRouteBuilder ooapi)
    {
        ArgumentNullException.ThrowIfNull(ooapi);
        var catalog = ooapi.ServiceProvider.GetRequiredService<ModuleCatalog>();
        foreach (var module in catalog.Enabled)
        {
            module.MapOoapiEndpoints(ooapi.MapGroup(string.Empty).WithTags(module.Name));
        }

        return ooapi;
    }

    /// <summary>
    /// Maps the browser endpoints of the enabled modules, each under <c>/{module name}</c> and by default requiring
    /// sign-in (spec module-system).
    /// </summary>
    public static IEndpointRouteBuilder MapDoctrinaBrowserEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var catalog = app.ServiceProvider.GetRequiredService<ModuleCatalog>();
        foreach (var module in catalog.Enabled)
        {
            module.MapBrowserEndpoints(app.MapGroup("/" + module.Name.ToLowerInvariant()).RequireAuthorization());
        }

        return app;
    }

    private sealed class ModuleServiceProviderFactory(IConfiguration configuration, ServiceProviderOptions options)
        : IServiceProviderFactory<IServiceCollection>
    {
        public IServiceCollection CreateBuilder(IServiceCollection services)
        {
            var known = services
                .Where(d => d.ServiceType == typeof(ModuleRegistration))
                .Select(d => ((ModuleRegistration)d.ImplementationInstance!).Module)
                .ToList();

            var catalog = ModuleCatalog.Create(
                known,
                ModuleCatalog.ReadEnabledNames(configuration),
                PermissionRegistry.ReadMapping(configuration));
            foreach (var module in catalog.Enabled)
            {
                module.ConfigureServices(services, configuration);
            }

            services.AddSingleton(catalog);
            services.AddSingleton(catalog.Permissions);
            return services;
        }

        public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder) =>
            containerBuilder.BuildServiceProvider(options);
    }
}
