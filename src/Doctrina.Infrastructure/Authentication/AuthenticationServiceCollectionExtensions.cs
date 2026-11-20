using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Doctrina.Infrastructure.Authentication;

/// <summary>
/// User sign-in (cookie + OpenID Connect, code flow with PKCE) and bearer authentication for <c>/api/v1</c>.
/// Works unchanged against Keycloak, Entra ID and SURFconext: only configuration differs (design D7).
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    public const string IdpHintParameter = "kc_idp_hint";

    public static IServiceCollection AddDoctrinaAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<OidcSettings>()
            .Bind(configuration.GetSection(OidcSettings.SectionName))
            .Validate(
                oidc => string.IsNullOrEmpty(oidc.Authority) || !string.IsNullOrEmpty(oidc.ClientSecret),
                $"{OidcSettings.SectionName}:ClientSecret ontbreekt. Zet het in appsettings.Secrets.json "
                + "(zie appsettings.Secrets.example.json) of in de omgevingsvariabele Authentication__Oidc__ClientSecret.")
            .ValidateOnStart();
        services.AddOptions<ApiAuthenticationSettings>().Bind(configuration.GetSection(ApiAuthenticationSettings.SectionName));

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect()
            .AddJwtBearer();

        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<OidcSettings>>((options, oidc) =>
            {
                options.Cookie.Name = "doctrina.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = oidc.Value.SessionLifetime;
                options.SlidingExpiration = false;
                options.AccessDeniedPath = "/access-denied";
                options.Events.OnSigningIn = context =>
                {
                    // End of session as a claim, so Blazor circuits have to sign in again afterwards (design D7).
                    if (context.Principal?.Identity is ClaimsIdentity identity && context.Properties.ExpiresUtc is { } expires)
                    {
                        identity.AddClaim(new Claim(DoctrinaClaimTypes.SessionExpires, expires.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)));
                    }

                    return Task.CompletedTask;
                };
            });

        services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<IOptions<OidcSettings>>((options, settings) =>
            {
                var oidc = settings.Value;
                options.Authority = oidc.Authority;
                options.ClientId = oidc.ClientId;
                options.ClientSecret = oidc.ClientSecret;
                options.RequireHttpsMetadata = oidc.RequireHttpsMetadata;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.SaveTokens = false;
                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.TokenValidationParameters.NameClaimType = DoctrinaClaimTypes.Name;
                options.TokenValidationParameters.RoleClaimType = DoctrinaClaimTypes.Roles;

                options.Scope.Clear();
                foreach (var scope in oidc.EffectiveScopes)
                {
                    options.Scope.Add(scope);
                }

                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    var hint = context.HttpContext.RequestServices.GetRequiredService<IOptions<OidcSettings>>().Value.IdpHint;
                    if (!string.IsNullOrWhiteSpace(hint))
                    {
                        context.ProtocolMessage.SetParameter(IdpHintParameter, hint);
                    }

                    return Task.CompletedTask;
                };

                options.Events.OnTokenValidated = context =>
                {
                    // Keep only the ID token (encrypted in the session cookie): needed as id_token_hint on sign-out,
                    // otherwise Keycloak shows a confirmation screen and the user does not return. Not the access tokens.
                    // Code flow: the ID token comes from the response of the token endpoint.
                    var idToken = context.TokenEndpointResponse?.IdToken ?? context.ProtocolMessage?.IdToken;
                    if (!string.IsNullOrEmpty(idToken))
                    {
                        context.Properties?.StoreTokens([new AuthenticationToken { Name = OpenIdConnectParameterNames.IdToken, Value = idToken }]);
                    }

                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToIdentityProviderForSignOut = context =>
                {
                    // client_id as an extra identifier (Keycloak/Entra) alongside id_token_hint.
                    context.ProtocolMessage.ClientId = context.Options.ClientId;
                    return Task.CompletedTask;
                };
            });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<OidcSettings>, IOptions<ApiAuthenticationSettings>>((options, oidcSettings, apiSettings) =>
            {
                var oidc = oidcSettings.Value;
                var api = apiSettings.Value;
                options.Authority = string.IsNullOrWhiteSpace(api.Authority) ? oidc.Authority : api.Authority;
                options.Audience = api.Audience;
                options.RequireHttpsMetadata = api.RequireHttpsMetadata ?? oidc.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = DoctrinaClaimTypes.Name;
                options.TokenValidationParameters.RoleClaimType = DoctrinaClaimTypes.Roles;
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(DoctrinaPolicies.ApiCaller, policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser());

        return services;
    }
}
