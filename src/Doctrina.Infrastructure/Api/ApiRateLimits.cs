using System.Threading.RateLimiting;
using Doctrina.Infrastructure.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Infrastructure.Api;

/// <summary>
/// Rate limits for the API (change add-rest-api, design D7): batch endpoints per client (<c>azp</c>), a fixed window of
/// one minute with <c>Api:BatchesPerMinute</c> (default 60) calls. Over the limit: 429 as ProblemDetails.
/// </summary>
public static class ApiRateLimits
{
    /// <summary>Policy for batch endpoints: <c>.RequireRateLimiting(ApiRateLimits.Batch)</c>.</summary>
    public const string Batch = "api-batch";

    public const string BatchesPerMinuteKey = "Api:BatchesPerMinute";

    public const string TooManyRequestsCode = "Api.TooManyRequests";

    public static IServiceCollection AddDoctrinaRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });
        // Read the limit when the options are built, so configuration added later (e.g. in tests) counts.
        services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            var perMinute = configuration.GetValue(BatchesPerMinuteKey, 60);
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Batch, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(DoctrinaClaimTypes.AuthorizedParty)?.Value ?? "(anonymous)",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = $"At most {perMinute} batches per minute per client.",
                        Extensions = { ["code"] = TooManyRequestsCode },
                    },
                });
            };
        });
        return services;
    }
}
