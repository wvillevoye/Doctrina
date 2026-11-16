using Doctrina.Core.Results;
using Doctrina.Modules.Lti.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Lti;

/// <summary>What the endpoints of the LTI services (AGS and NRPS) have in common: token check, errors, query parameters, paging.</summary>
internal static class ServiceEndpoints
{
    /// <summary>Checks the token (any of <paramref name="scopes"/>) and runs the endpoint with the tool and the issuer.</summary>
    public static async Task<IResult> RunAsync(
        HttpContext http, ServiceTokenValidator validator, IOptions<LtiOptions> options, string[] scopes, Func<ServiceCaller, string, Task<IResult>> run, CancellationToken ct,
        int validationStatus = StatusCodes.Status422UnprocessableEntity)
    {
        http.Response.Headers.CacheControl = "no-store";
        var issuer = LtiEndpoints.Issuer(options.Value, http.Request);
        var auth = await validator.AuthorizeAsync(http.Request.Headers.Authorization.ToString(), issuer, scopes, ct);
        if (auth.Error is { } denied)
        {
            if (denied.Type == ErrorType.Unauthorized)
            {
                http.Response.Headers.WWWAuthenticate = "Bearer";
            }

            return Problem(denied, validationStatus);
        }

        return await run(auth.Value!, issuer);
    }

    /// <summary>ProblemDetails with the stable error code; AGS answers a validation error with 422, NRPS with 400.</summary>
    public static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Problem(Error error, int validationStatus = StatusCodes.Status422UnprocessableEntity)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => validationStatus,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.Details is not null)
        {
            extensions["errors"] = error.Details;
        }

        return TypedResults.Problem(title: error.Message, statusCode: status, extensions: extensions);
    }

    public static string? Param(IQueryCollection query, string name) => query.TryGetValue(name, out var v) && !string.IsNullOrEmpty(v) ? v.ToString() : null;

    public static int? Limit(IQueryCollection query) => Param(query, "limit") is { } v ? (int.TryParse(v, out var n) ? n : 0) : null;

    public static Guid? After(IQueryCollection query) => Param(query, "after") is { } v && Guid.TryParse(v, out var g) ? g : null;

    /// <summary>Sets the <c>Link</c> header (<c>rel="next"</c>) for the page after <paramref name="next"/>, keeping the other query parameters.</summary>
    public static void NextLink(HttpContext http, string path, Guid? next)
    {
        if (next is { } id)
        {
            var query = http.Request.Query.Where(p => p.Key != "after").ToDictionary(p => p.Key, p => (string?)p.Value.ToString());
            query["after"] = id.ToString("D");
            http.Response.Headers.Link = $"<{QueryHelpers.AddQueryString(path, query)}>; rel=\"next\"";
        }
    }
}
