using System.Security.Claims;
using Doctrina.Infrastructure.Api;
using Doctrina.Infrastructure.Authentication;
using Doctrina.Infrastructure.Authorization;
using Doctrina.Modules.Interop.OOAPI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Doctrina.Modules.Interop.OOAPI.Api;

/// <summary>Starting a read of an OOAPI source on request (design D9 of add-ooapi-consumer); needs <c>Ooapi.Sync</c>.</summary>
internal static class SyncEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var sources = api.MapGroup("ooapi-sources").WithTags("ooapi-sources").RequirePermission(OoapiPermissions.Sync);

        sources.MapPost("{name}/syncs", StartAsync)
            .WithSummary("Read an OOAPI source now")
            .WithDescription("Optional body: { \"dryRun\": true } to only see what would happen. Answers 202 with the import job (see /api/v1/imports/{id}), 404 for an unknown or disabled source, 409 while the source is being read.");
    }

    private static async Task<IResult> StartAsync(string name, [FromBody] SyncRequest? body, OoapiSync sync, ClaimsPrincipal user, CancellationToken ct)
    {
        var client = user.FindFirst(DoctrinaClaimTypes.AuthorizedParty)?.Value ?? user.Identity?.Name ?? "(unknown)";
        var result = await sync.StartAsync(name, body?.DryRun == true, client, ct);
        return result.ToHttpResult(jobId => Results.Accepted($"/api/v1/imports/{jobId:D}", new { jobId, source = name, status = "queued" }));
    }

    internal sealed record SyncRequest(bool? DryRun);
}
