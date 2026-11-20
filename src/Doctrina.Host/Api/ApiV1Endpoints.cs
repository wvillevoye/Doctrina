using System.Security.Claims;
using Doctrina.Infrastructure.Authentication;

namespace Doctrina.Host.Api;

/// <summary>
/// The <c>/api/v1</c> group: bearer tokens only, never the session cookie, no redirects.
/// Modules later hang their endpoints under it (change add-modular-foundation, task group 3).
/// </summary>
internal static class ApiV1Endpoints
{
    public const string Prefix = "/api/v1";

    public static RouteGroupBuilder MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup(Prefix).RequireAuthorization(DoctrinaPolicies.ApiCaller);

        // Who is calling? Handy for integration partners to check their token.
        api.MapGet("/me", (ClaimsPrincipal caller) => new CurrentCallerResponse(
            Subject: caller.FindFirst(DoctrinaClaimTypes.Subject)?.Value,
            Client: caller.FindFirst(DoctrinaClaimTypes.AuthorizedParty)?.Value,
            Name: caller.Identity?.Name,
            Roles: [.. caller.FindAll(DoctrinaClaimTypes.Roles).Select(c => c.Value)]));

        return api;
    }
}

/// <summary>
/// The <c>/ooapi/v5</c> group for modules that offer OOAPI (change add-ooapi-interop): the same as <c>/api/v1</c>,
/// bearer tokens only, never the session cookie, no redirects.
/// </summary>
internal static class OoapiV5Endpoints
{
    public const string Prefix = "/ooapi/v5";

    public static RouteGroupBuilder MapOoapiV5(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGroup(Prefix).RequireAuthorization(DoctrinaPolicies.ApiCaller);
}

/// <summary>Response of <c>GET /api/v1/me</c>.</summary>
internal sealed record CurrentCallerResponse(string? Subject, string? Client, string? Name, IReadOnlyList<string> Roles);
