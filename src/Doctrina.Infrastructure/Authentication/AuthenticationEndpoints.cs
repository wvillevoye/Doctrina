using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Doctrina.Infrastructure.Authentication;

/// <summary>Endpoints <c>GET /inloggen</c> en <c>POST /uitloggen</c>.</summary>
public static class AuthenticationEndpoints
{
    public const string SignInPath = "/inloggen";
    public const string SignOutPath = "/uitloggen";

    /// <summary>Notice shown when the identity provider can't be reached (page in the host).</summary>
    public const string UnavailablePath = "/inloggen-onbereikbaar";

    public static IEndpointRouteBuilder MapDoctrinaAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(SignInPath, (string? returnUrl) =>
                Results.Challenge(
                    new AuthenticationProperties { RedirectUri = LocalPathOrRoot(returnUrl) },
                    [OpenIdConnectDefaults.AuthenticationScheme]))
            .AllowAnonymous()
            .ExcludeFromDescription();

        // Formulier-POST: [FromForm] zorgt dat antiforgery-validatie verplicht is.
        endpoints.MapPost(SignOutPath, ([FromForm] string? returnUrl) =>
                Results.SignOut(
                    new AuthenticationProperties { RedirectUri = LocalPathOrRoot(returnUrl) },
                    [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]))
            .ExcludeFromDescription();

        return endpoints;
    }

    /// <summary>Alleen lokale paden (geen open redirect naar andere sites).</summary>
    public static string LocalPathOrRoot(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            ? returnUrl
            : "/";
}
