using System.Security.Claims;
using Doctrina.Infrastructure.Authentication;

namespace Doctrina.Host.Api;

/// <summary>
/// De <c>/api/v1</c>-groep: alleen bearer-tokens, nooit de sessiecookie, geen redirects.
/// Modules hangen hier later hun endpoints onder (change add-modular-foundation, taakgroep 3).
/// </summary>
internal static class ApiV1Endpoints
{
    public const string Prefix = "/api/v1";

    public static RouteGroupBuilder MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup(Prefix).RequireAuthorization(DoctrinaPolicies.ApiCaller);

        // Wie roept aan? Handig voor koppelpartners om hun token te controleren.
        api.MapGet("/me", (ClaimsPrincipal caller) => new CurrentCallerResponse(
            Subject: caller.FindFirst(DoctrinaClaimTypes.Subject)?.Value,
            Client: caller.FindFirst(DoctrinaClaimTypes.AuthorizedParty)?.Value,
            Name: caller.Identity?.Name,
            Roles: [.. caller.FindAll(DoctrinaClaimTypes.Roles).Select(c => c.Value)]));

        return api;
    }
}

/// <summary>Antwoord van <c>GET /api/v1/me</c>.</summary>
internal sealed record CurrentCallerResponse(string? Subject, string? Client, string? Name, IReadOnlyList<string> Roles);
