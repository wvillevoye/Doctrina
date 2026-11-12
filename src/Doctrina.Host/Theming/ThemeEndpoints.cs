using Doctrina.Infrastructure.Authentication;
using Doctrina.UI.Theming;
using Microsoft.AspNetCore.Mvc;

namespace Doctrina.Host.Theming;

/// <summary>Themakeuze van de gebruiker (spec theming "Licht en donker thema naar keuze van de gebruiker", design D3).</summary>
public static class ThemeEndpoints
{
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(365);

    /// <summary>Thema uit de cookie; zonder (geldige) cookie volgt het thema het systeem.</summary>
    public static DcTheme CurrentTheme(this HttpContext? context) =>
        DcThemes.TryParse(context?.Request.Cookies[DcThemes.CookieName]) ?? DcTheme.System;

    /// <summary><c>POST /thema</c>: slaat de keuze op en stuurt terug naar de pagina (werkt zonder scripts).</summary>
    public static IEndpointRouteBuilder MapDoctrinaThemeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        // [FromForm]: antiforgery-validatie is verplicht.
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

    // Functionele cookie: geen toestemming nodig, niet leesbaar voor scripts.
    private static CookieOptions CookieOptions(HttpContext context) => new()
    {
        Path = "/",
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
    };
}
