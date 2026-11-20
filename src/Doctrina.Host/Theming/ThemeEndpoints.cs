using Doctrina.Infrastructure.Authentication;
using Doctrina.UI.Theming;
using Microsoft.AspNetCore.Mvc;

namespace Doctrina.Host.Theming;

/// <summary>Theme choice of the user (spec theming "Licht en donker thema naar keuze van de gebruiker", design D3).</summary>
public static class ThemeEndpoints
{
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(365);

    /// <summary>Theme from the cookie; without a (valid) cookie the theme follows the system.</summary>
    public static DcTheme CurrentTheme(this HttpContext? context) =>
        DcThemes.TryParse(context?.Request.Cookies[DcThemes.CookieName]) ?? DcTheme.System;

    /// <summary><c>POST /theme</c>: saves the choice and redirects back to the page (works without scripts).</summary>
    public static IEndpointRouteBuilder MapDoctrinaThemeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        // [FromForm]: antiforgery validation is mandatory.
        endpoints.MapPost(DcThemes.EndpointPath, ([FromForm] string? theme, [FromForm] string? returnUrl, HttpContext context) =>
            {
                if (DcThemes.TryParse(theme) is not { } choice)
                {
                    return Results.BadRequest();
                }

                if (choice == DcTheme.System)
                {
                    context.Response.Cookies.Delete(DcThemes.CookieName, CookieOptions(context));
                }
                else
                {
                    var options = CookieOptions(context);
                    options.MaxAge = CookieLifetime;
                    context.Response.Cookies.Append(DcThemes.CookieName, choice.ToValue(), options);
                }

                return Results.LocalRedirect(AuthenticationEndpoints.LocalPathOrRoot(returnUrl));
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }

    // Functional cookie: no consent needed, not readable by scripts.
    private static CookieOptions CookieOptions(HttpContext context) => new()
    {
        Path = "/",
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
    };
}
