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

/// <summary>Een module die de host kent; of zij start, bepaalt <c>Modules:Enabled</c>.</summary>
/// <param name="Module">De module.</param>
public sealed record ModuleRegistration(IModule Module);

/// <summary>Modules registreren, valideren en hun bijdragen aan de host koppelen (design D1/D2).</summary>
public static class ModuleServiceCollectionExtensions
{
    /// <summary>Maakt <paramref name="module"/> bekend bij de host (de statische modulecatalogus, design D1).</summary>
    public static IServiceCollection AddDoctrinaModule(this IServiceCollection services, IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return services.AddSingleton(new ModuleRegistration(module));
    }

    /// <summary>Maakt module <typeparamref name="TModule"/> bekend bij de host.</summary>
    public static IServiceCollection AddDoctrinaModule<TModule>(this IServiceCollection services)
        where TModule : IModule, new() =>
        services.AddDoctrinaModule(new TModule());

    /// <summary>
    /// Zet het modulesysteem aan. Tijdens het bouwen van de app — nadat alle services, ook die van tests, zijn
    /// geregistreerd en de configuratie definitief is — wordt de <see cref="ModuleCatalog"/> gemaakt en gevalideerd en
    /// worden de services van de ingeschakelde modules in afhankelijkheidsvolgorde geregistreerd.
    /// </summary>
    /// <exception cref="ModuleConfigurationException">Bij <c>Build()</c> als de moduleconfiguratie ongeldig is.</exception>
    public static IHostBuilder UseDoctrinaModules(this IHostBuilder host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.UseServiceProviderFactory(context =>
        {
            // Strenge DI-validatie in Development en in de tests (design D13).
            var development = context.HostingEnvironment.IsDevelopment() || context.HostingEnvironment.IsTest();
            return new ModuleServiceProviderFactory(
                context.Configuration,
                new ServiceProviderOptions { ValidateScopes = development, ValidateOnBuild = development });
        });
    }

    /// <summary>
    /// Koppelt de endpoints van de ingeschakelde modules aan de <c>/api/v1</c>-groep; elke module krijgt een eigen
    /// subgroep met haar naam als OpenAPI-tag.
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
