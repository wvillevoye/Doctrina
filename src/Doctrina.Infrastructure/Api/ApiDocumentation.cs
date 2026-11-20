using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Doctrina.Infrastructure.Api;

/// <summary>
/// OpenAPI document (<c>/openapi/v1.json</c>) and Scalar (<c>/scalar</c>) for the <c>/api/v1</c> endpoints, with the
/// bearer security scheme (design D9). On in Development; elsewhere only if <c>Api:Documentation:Enabled</c> is true.
/// </summary>
public static class ApiDocumentation
{
    public const string EnabledKey = "Api:Documentation:Enabled";
    public const string DocumentName = "v1";
    public const string SchemeName = "Bearer";

    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        return bool.TryParse(configuration[EnabledKey], out var enabled) ? enabled : environment.IsDevelopment();
    }

    public static IServiceCollection AddDoctrinaOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(DocumentName, options =>
        {
            options.ShouldInclude = description => description.RelativePath?.StartsWith("api/v1", StringComparison.OrdinalIgnoreCase) == true;
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Doctrina API";
                document.Info.Version = "v1";
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
                document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Access token van de identity provider (gebruiker of client credentials) met audience doctrina-api.",
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                var anonymous = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any();
                if (!anonymous)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [],
                    });
                }

                return Task.CompletedTask;
            });
        });

    /// <summary>Maps the OpenAPI document and Scalar, if the documentation is enabled.</summary>
    public static IEndpointRouteBuilder MapDoctrinaApiDocumentation(this IEndpointRouteBuilder endpoints, IConfiguration configuration, IHostEnvironment environment)
    {
        if (!IsEnabled(configuration, environment))
        {
            return endpoints;
        }

        endpoints.MapOpenApi().AllowAnonymous();
        endpoints.MapScalarApiReference(options => options.WithTitle("Doctrina API")).AllowAnonymous();
        return endpoints;
    }
}
