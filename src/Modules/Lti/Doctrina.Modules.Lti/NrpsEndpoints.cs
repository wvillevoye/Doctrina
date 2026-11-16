using Doctrina.Modules.Lti.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using static Doctrina.Modules.Lti.ServiceEndpoints;

namespace Doctrina.Modules.Lti;

/// <summary>
/// The Names and Role Provisioning Service of LTI Advantage (design D2 of add-lti-nrps): the participants of an offering under
/// <c>/lti/nrps/{context}/memberships</c>. No session: the access token of the tool is checked, then <see cref="MembershipService"/>.
/// Errors are ProblemDetails with the code; an invalid query is 400.
/// </summary>
internal static class NrpsEndpoints
{
    private const string ContainerType = "application/vnd.ims.lti-nrps.v2.membershipcontainer+json";

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("nrps/{context:guid}/memberships", (Guid context, HttpContext http, ServiceTokenValidator v, MembershipService members, IOptions<LtiOptions> o, CancellationToken ct) =>
            RunAsync(http, v, o, [LtiScopes.MembershipReadOnly], async (caller, issuer) =>
            {
                var q = http.Request.Query;
                var list = await members.ListAsync(caller.Tool, context, Param(q, "role"), Param(q, "rlid"), Limit(q), After(q), ct);
                if (list.IsFailure)
                {
                    return Problem(list.Error!, StatusCodes.Status400BadRequest);
                }

                var url = $"{issuer}/lti/nrps/{context:D}/memberships";
                NextLink(http, url, list.Value!.Members.Next);
                var offering = list.Value.Offering;
                return Results.Json(
                    new
                    {
                        id = url,
                        context = new { id = offering.Id.ToString("D"), label = offering.ExternalId, title = offering.Name },
                        members = list.Value.Members.Items.Select(MemberJson),
                    },
                    contentType: ContainerType);
            }, ct, StatusCodes.Status400BadRequest)).AllowAnonymous().DisableAntiforgery();

    private static Dictionary<string, object?> MemberJson(Member m)
    {
        var json = new Dictionary<string, object?> { ["status"] = "Active", ["user_id"] = m.UserId, ["roles"] = m.Roles };
        Set(json, "name", m.Name);
        Set(json, "given_name", m.GivenName);
        Set(json, "family_name", m.FamilyName);
        Set(json, "email", m.Email);
        return json;
    }

    private static void Set(Dictionary<string, object?> json, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            json[name] = value;
        }
    }
}
