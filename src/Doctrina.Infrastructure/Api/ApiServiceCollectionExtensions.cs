using System.Globalization;
using Doctrina.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Doctrina.Infrastructure.Api;

/// <summary>API-conventies van de host (design D9): ProblemDetails, fouttitels, exceptions, OpenAPI, health.</summary>
public static class ApiServiceCollectionExtensions
{
    /// <summary>Ondersteunde talen voor fouttitels en de UI; Nederlands is standaard.</summary>
    public static IReadOnlyList<CultureInfo> SupportedCultures { get; } = [new("nl"), new("en")];

    public static IServiceCollection AddDoctrinaApi(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = DoctrinaProblemDetails.Customize);
        services.AddExceptionHandler<UnexpectedExceptionHandler>();
        services.TryAddSingleton<ErrorTitles>();
        services.AddErrorTitles("Doctrina.Infrastructure.Api.DoctrinaErrors", typeof(ApiServiceCollectionExtensions).Assembly);
        services.AddDoctrinaOpenApi();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>(DatabaseHealthCheck.Name);
        return services;
    }

    /// <summary>Taal van het verzoek uit <c>Accept-Language</c> (nl of en), standaard nl.</summary>
    public static IApplicationBuilder UseDoctrinaRequestLocalization(this IApplicationBuilder app) =>
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture("nl"),
            SupportedCultures = [.. SupportedCultures],
            SupportedUICultures = [.. SupportedCultures],
        });
}
