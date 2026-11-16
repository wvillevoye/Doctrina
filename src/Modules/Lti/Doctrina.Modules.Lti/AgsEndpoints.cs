using System.Text.Json;
using Doctrina.Core.Results;
using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using static Doctrina.Modules.Lti.ServiceEndpoints;

namespace Doctrina.Modules.Lti;

/// <summary>
/// The Assignment and Grade Services of LTI Advantage under <c>/lti/ags/{context}</c> (design D3 of add-lti-ags): line items,
/// scores and results. No session: every endpoint checks the access token of the tool itself (scope per endpoint) and the
/// rules of <see cref="GradeService"/>. Errors are ProblemDetails with the code; validation errors are 422 as AGS expects.
/// </summary>
internal static class AgsEndpoints
{
    private const string LineItemType = "application/vnd.ims.lis.v2.lineitem+json";
    private const string LineItemContainerType = "application/vnd.ims.lis.v2.lineitemcontainer+json";
    private const string ResultContainerType = "application/vnd.ims.lis.v2.resultcontainer+json";
    private const int MaxBodyBytes = 16 * 1024;

    private static readonly string[] Read = [LtiScopes.LineItem, LtiScopes.LineItemReadOnly];

    private sealed record LineItemBody(string? Label, decimal? ScoreMaximum, string? ResourceId, string? Tag, string? ResourceLinkId, DateTimeOffset? StartDateTime, DateTimeOffset? EndDateTime);

    private sealed record ScoreBody(string? UserId, decimal? ScoreGiven, decimal? ScoreMaximum, string? Comment, string? ActivityProgress, string? GradingProgress, DateTimeOffset? Timestamp);

    public static void Map(IEndpointRouteBuilder app)
    {
        var ags = app.MapGroup("ags/{context:guid}").AllowAnonymous().DisableAntiforgery();

        ags.MapGet("lineitems", (Guid context, HttpContext http, ServiceTokenValidator v, GradeService grades, IOptions<LtiOptions> o, CancellationToken ct) =>
            RunAsync(http, v, o, Read, async (caller, issuer) =>
            {
                var q = http.Request.Query;
                var page = await grades.ListLineItemsAsync(caller.Tool, context, Param(q, "resource_link_id"), Param(q, "resource_id"), Param(q, "tag"), Limit(q), After(q), ct);
                return page.IsFailure
                    ? Problem(page.Error!)
                    : Container(http, LineItemContainerType, page.Value!, l => LineItemJson(issuer, l), $"{issuer}/lti/ags/{context:D}/lineitems");
            }, ct));

        ags.MapPost("lineitems", (Guid context, HttpContext http, ServiceTokenValidator v, GradeService grades, IOptions<LtiOptions> o, CancellationToken ct) =>
            RunAsync(http, v, o, [LtiScopes.LineItem], async (caller, issuer) =>
            {
                if (await BodyAsync<LineItemBody>(http, ct) is not { } body)
                {
                    return BadBody();
                }

                var created = await grades.CreateLineItemAsync(caller.Tool, context, ToInput(body), ct);
                return created.IsFailure
                    ? Problem(created.Error!)
                    : Results.Json(LineItemJson(issuer, created.Value!), statusCode: StatusCodes.Status201Created, contentType: LineItemType);
            }, ct));

        ags.MapGet("lineitems/{id:guid}", (Guid context, Guid id, HttpContext http, ServiceTokenValidator v, GradeService grades, IOptions<LtiOptions> o, CancellationToken ct) =>
            RunAsync(http, v, o, Read, async (caller, issuer) =>
            {
                var found = await grades.FindLineItemAsync(caller.Tool, context, id, ct);
                return found.IsFailure ? Problem(found.Error!) : Results.Json(LineItemJson(issuer, found.Value!), contentType: LineItemType);
            }, ct));

        ags.MapPut("lineitems/{id:guid}", (Guid context, Guid id, HttpContext http, ServiceTokenValidator v, GradeService grades, IOptions<LtiOptions> o, CancellationToken ct) =>
            RunAsync(http, v, o, [LtiScopes.LineItem], async (caller, issuer) =>
            {
                if (await BodyAsync<LineItemBody>(http, ct) is not { } body)
                {
                    return BadBody();
                }

                var updated = await grades.UpdateLineItemAsync(caller.Tool, context, id, ToInput(body), ct);
                if (updated.IsFailure)
                {
                    return Problem(updated.Error!);
                }

                var found = await grades.FindLineItemAsync(caller.Tool, context, id, ct);
                return Results.Json(LineItemJson(issuer, found.Value!), contentType: LineItemType);
            }, ct));

        ags.MapDelete("lineitems/{id:guid}", (Guid context, Guid id, HttpContext http, ServiceTokenValidator v, GradeService grades, IOptions<LtiOptions> o, CancellationToken ct) =>
            RunAsync(http, v, o, [LtiScopes.LineItem], async (caller, _) =>
            {
                var deleted = await grades.DeleteLineItemAsync(caller.Tool, context, id, ct);
                return deleted.IsFailure ? Problem(deleted.Error!) : Results.NoContent();
            }, ct));

        ags.MapPost("lineitems/{id:guid}/scores", (Guid context, Guid id, HttpContext http, ServiceTokenValidator v, GradeService grades, IOptions<LtiOptions> o, CancellationToken ct) =>
            RunAsync(http, v, o, [LtiScopes.Score], async (caller, _) =>
            {
                if (await BodyAsync<ScoreBody>(http, ct) is not { } body)
                {
                    return BadBody();
                }

                var posted = await grades.PostScoreAsync(caller.Tool, context, id,
                    new ScoreInput(body.UserId, body.ScoreGiven, body.ScoreMaximum, body.Comment, body.ActivityProgress, body.GradingProgress, body.Timestamp), ct);
                return posted.IsFailure ? Problem(posted.Error!) : Results.NoContent();
            }, ct));

        ags.MapGet("lineitems/{id:guid}/results", (Guid context, Guid id, HttpContext http, ServiceTokenValidator v, GradeService grades, IOptions<LtiOptions> o, CancellationToken ct) =>
            RunAsync(http, v, o, [LtiScopes.ResultReadOnly], async (caller, issuer) =>
            {
                var q = http.Request.Query;
                var page = await grades.ResultsAsync(caller.Tool, context, id, Param(q, "user_id"), Limit(q), After(q), ct);
                var lineItem = $"{issuer}/lti/ags/{context:D}/lineitems/{id:D}";
                return page.IsFailure
                    ? Problem(page.Error!)
                    : Container(http, ResultContainerType, page.Value!, s => ResultJson(lineItem, s), $"{lineItem}/results");
            }, ct));
    }

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult BadBody() =>
        Problem(Error.Validation(LtiErrors.AgsInvalidLineItem, "The request body is missing, too large or not valid JSON."));

    private static async Task<T?> BodyAsync<T>(HttpContext http, CancellationToken ct) where T : class
    {
        if (http.Request.ContentLength is null or > MaxBodyBytes)
        {
            return null;
        }

        try
        {
            return await http.Request.ReadFromJsonAsync<T>(ct);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static LineItemInput ToInput(LineItemBody b) => new(b.Label, b.ScoreMaximum, b.ResourceId, b.Tag, b.ResourceLinkId, b.StartDateTime, b.EndDateTime);

    private static Dictionary<string, object?> LineItemJson(string issuer, LineItem l)
    {
        var json = new Dictionary<string, object?>
        {
            ["id"] = $"{issuer}/lti/ags/{l.OfferingId:D}/lineitems/{l.Id:D}",
            ["scoreMaximum"] = l.ScoreMaximum,
            ["label"] = l.Label,
        };
        Set(json, "tag", l.Tag);
        Set(json, "resourceId", l.ResourceId);
        Set(json, "resourceLinkId", l.ResourceLinkId?.ToString("D"));
        Set(json, "startDateTime", l.StartDateTime);
        Set(json, "endDateTime", l.EndDateTime);
        return json;
    }

    private static Dictionary<string, object?> ResultJson(string lineItem, Score s)
    {
        var json = new Dictionary<string, object?>
        {
            ["id"] = $"{lineItem}/results/{s.Id:D}",
            ["scoreOf"] = lineItem,
            ["userId"] = s.PersonId.ToString("N"),
            ["resultScore"] = s.ScoreGiven,
            ["resultMaximum"] = s.ScoreMaximum,
        };
        Set(json, "comment", s.Comment);
        return json;
    }

    private static void Set(Dictionary<string, object?> json, string name, object? value)
    {
        if (value is not null)
        {
            json[name] = value;
        }
    }

    /// <summary>A JSON array with a <c>Link</c> header (<c>rel="next"</c>) when there is another page.</summary>
    private static IResult Container<T>(HttpContext http, string contentType, Page<T> page, Func<T, Dictionary<string, object?>> json, string path)
    {
        ServiceEndpoints.NextLink(http, path, page.Next);
        return Results.Json(page.Items.Select(json), contentType: contentType);
    }
}
