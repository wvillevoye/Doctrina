using Doctrina.Infrastructure.Api;
using Doctrina.Modules.Lti.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Lti;

/// <summary>The browser endpoints of the module under <c>/lti</c> (design D2 and D5 of add-lti-platform).</summary>
internal static class LtiEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        // The JWKS must be public: tools fetch it without a session.
        app.MapGet("jwks.json", async (KeyService keys, CancellationToken ct) => Results.Json(await keys.JwksAsync(ct))).AllowAnonymous();

        // Login initiation: the user is signed in (the group requires it), then goes to the login URL of the tool.
        app.MapGet("launch/{item:guid}", async (Guid item, HttpContext http, LaunchService launch, IOptions<LtiOptions> options, CancellationToken ct) =>
            (await launch.StartAsync(http.User, item, Issuer(options.Value, http.Request), ct))
                .ToHttpResult(login => Results.Redirect(login.AbsoluteUri)));

        // Authentication request of the tool: answered with a page that posts the signed id_token to the tool.
        app.MapGet("authorize", async Task<IResult> (HttpContext http, LaunchService launch, IOptions<LtiOptions> options, CancellationToken ct) =>
        {
            var q = http.Request.Query;
            var request = new AuthRequest(q["scope"], q["response_type"], q["response_mode"], q["prompt"], q["client_id"],
                q["redirect_uri"], q["login_hint"], q["lti_message_hint"], q["nonce"], q["state"]);
            var result = await launch.AuthorizeAsync(http.User, request, Issuer(options.Value, http.Request), ct);
            http.Response.Headers.CacheControl = "no-store";
            return result.ToHttpResult(form => Results.Content(AutoPost(form), "text/html"));
        });
    }

    private static string AutoPost(LaunchForm form)
    {
        Func<string, string> e = System.Net.WebUtility.HtmlEncode;
        return $"""
            <!DOCTYPE html>
            <html lang="en"><head><meta charset="utf-8"><title>Launching tool</title></head>
            <body onload="document.forms[0].submit()">
            <form method="post" action="{e(form.RedirectUri)}">
            <input type="hidden" name="id_token" value="{e(form.IdToken)}">
            <input type="hidden" name="state" value="{e(form.State)}">
            <noscript><button type="submit">Continue</button></noscript>
            </form></body></html>
            """;
    }

    /// <summary>The issuer: the configured one, or the address the request came in on.</summary>
    public static string Issuer(LtiOptions options, HttpRequest request) =>
        LtiPlatform.Issuer(options, $"{request.Scheme}://{request.Host}");
}
