// Doctrina.AuthCheck — minimale app om de dev-stack (Keycloak -> Entra) te testen.
// Gebruikt dezelfde configuratievorm als Doctrina.Host straks: "Authentication:Oidc" (gebruikers) en "Authentication:Api" (systemen).
// Werkt ongewijzigd tegen Keycloak, Entra ID direct of SURFconext — alleen appsettings verschillen.
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);
var oidc = builder.Configuration.GetSection("Authentication:Oidc");
var api = builder.Configuration.GetSection("Authentication:Api");

builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(o =>
    {
        o.Cookie.Name = "doctrina.auth";
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    })
    .AddOpenIdConnect(o =>
    {
        o.Authority = oidc["Authority"];
        o.ClientId = oidc["ClientId"];
        o.ClientSecret = oidc["ClientSecret"];
        o.RequireHttpsMetadata = oidc.GetValue("RequireHttpsMetadata", true);
        o.ResponseType = OpenIdConnectResponseType.Code;
        o.UsePkce = true;
        o.SaveTokens = true;                       // access token bewaren om /api/v1 namens de gebruiker aan te roepen
        o.MapInboundClaims = false;                // claimnamen zoals in het token: sub, name, roles, oid, ...
        o.GetClaimsFromUserInfoEndpoint = false;   // alles staat al in het ID-token (ook bij Entra)
        o.Scope.Clear();
        foreach (var s in oidc.GetSection("Scopes").Get<string[]>() ?? ["openid", "profile", "email"]) o.Scope.Add(s);
        o.TokenValidationParameters.NameClaimType = "name";
        o.TokenValidationParameters.RoleClaimType = "roles";   // Keycloak (via mapper) en Entra gebruiken beide "roles"
        o.Events.OnRedirectToIdentityProvider = ctx =>
        {
            // Keycloak-specifiek: direct doorsturen naar Entra, Keycloak-inlogscherm overslaan
            var hint = oidc["IdpHint"];
            if (!string.IsNullOrEmpty(hint)) ctx.ProtocolMessage.SetParameter("kc_idp_hint", hint);
            return Task.CompletedTask;
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
    {
        o.Authority = api["Authority"] ?? oidc["Authority"];
        o.Audience = api["Audience"];
        o.RequireHttpsMetadata = api.GetValue("RequireHttpsMetadata", oidc.GetValue("RequireHttpsMetadata", true));
        o.MapInboundClaims = false;
        o.TokenValidationParameters.NameClaimType = "name";
        o.TokenValidationParameters.RoleClaimType = "roles";
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole("admin"))
    .AddPolicy("Api.Persons.Read", p => p
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireRole("doctrina.persons.read", "admin"));   // systeem met permissie, of gebruiker met rol admin

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", (HttpContext ctx) =>
{
    var u = ctx.User;
    if (u.Identity?.IsAuthenticated != true)
        return Results.Content(Page("Niet ingelogd", "<p><a href=\"/login\">Inloggen</a></p>"), "text/html");
    var rows = string.Concat(u.Claims.Select(c => $"<tr><td>{H(c.Type)}</td><td>{H(c.Value)}</td></tr>"));
    var body = $"""
        <p>Ingelogd als <b>{H(u.Identity.Name)}</b> — rollen: {H(string.Join(", ", u.FindAll("roles").Select(c => c.Value)))}</p>
        <p>Admin? {u.IsInRole("admin")} · <a href="/admin">/admin</a> · <a href="/token">access token</a> · <a href="/logout">Uitloggen</a></p>
        <table border="1" cellpadding="4">{rows}</table>
        """;
    return Results.Content(Page("Claims", body), "text/html");
});

app.MapGet("/login", () => Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }));
app.MapGet("/logout", () => Results.SignOut(new AuthenticationProperties { RedirectUri = "/" },
    [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
app.MapGet("/admin", (HttpContext ctx) => $"Welkom beheerder {ctx.User.Identity?.Name}").RequireAuthorization("Admin");
app.MapGet("/token", async (HttpContext ctx) => await ctx.GetTokenAsync("access_token") ?? "").RequireAuthorization();

// API (/api/v1) — Bearer-token van een systeem (client credentials) of namens een gebruiker
app.MapGet("/api/v1/whoami", (HttpContext ctx) => new
{
    sub = ctx.User.FindFirst("sub")?.Value,
    client = ctx.User.FindFirst("azp")?.Value,
    name = ctx.User.Identity?.Name,
    roles = ctx.User.FindAll("roles").Select(c => c.Value),
})
.RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme });

app.MapGet("/api/v1/persons", () => new[] { new { id = "00000000-0000-0000-0000-000000000001", externalId = "i6123456", givenName = "Femke" } })
   .RequireAuthorization("Api.Persons.Read");

app.Run();

static string H(string? s) => HtmlEncoder.Default.Encode(s ?? "");
static string Page(string title, string body) =>
    $"<!doctype html><meta charset=utf-8><title>{H(title)}</title><body style=\"font-family:sans-serif;max-width:60rem;margin:2rem auto\"><h1>{H(title)}</h1>{body}</body>";
