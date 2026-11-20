using System.Globalization;
using Doctrina.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Doctrina.Infrastructure.Api;

/// <summary>API conventions of the host (design D9): ProblemDetails, error titles, exceptions, OpenAPI, health.</summary>
public static class ApiServiceCollectionExtensions
{
    /// <summary>Supported languages for error titles and the UI; Dutch is the default.</summary>
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

    /// <summary>Language of the request from <c>Accept-Language</c> (nl or en), nl by default.</summary>
    public static IApplicationBuilder UseDoctrinaRequestLocalization(this IApplicationBuilder app) =>
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture("nl"),
            SupportedCultures = [.. SupportedCultures],
            SupportedUICultures = [.. SupportedCultures],
        });
}
