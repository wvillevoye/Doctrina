using Doctrina.Core.Results;
using Doctrina.Modules.Lti.Domain;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Doctrina.Modules.Lti.Application;

/// <summary>The tool behind a valid access token, and the scopes the token carries.</summary>
internal sealed record ServiceCaller(LtiTool Tool, IReadOnlySet<string> Scopes);

/// <summary>
/// Checks the access token on an LTI service endpoint (design D2 and D3 of add-lti-ags): a JWT signed with a platform key
/// (the active key or a recently retired one), issued to this platform, not expired, for a tool that still exists.
/// A missing, expired or forged token is <c>Lti.ServiceAuth.Unauthorized</c> (401); a token without the needed scope is
/// <c>Lti.ServiceAuth.Forbidden</c> (403). Nothing of the token is logged.
/// </summary>
internal sealed class ServiceTokenValidator(KeyService keys, ToolService tools, TimeProvider time)
{
    private const string BearerPrefix = "Bearer ";

    /// <param name="authorization">The <c>Authorization</c> header.</param>
    /// <param name="issuer">The issuer of this platform (audience and issuer of the token).</param>
    /// <param name="acceptedScopes">The caller needs at least one of these scopes.</param>
    /// <param name="ct">Cancels the check.</param>
    public async Task<Result<ServiceCaller>> AuthorizeAsync(string? authorization, string issuer, IReadOnlyCollection<string> acceptedScopes, CancellationToken ct)
    {
        if (authorization is null || !authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized();
        }

        var jwt = authorization[BearerPrefix.Length..].Trim();
        var handler = new JsonWebTokenHandler();
        if (jwt.Length == 0 || jwt.Length > ServiceAuthService.MaxAssertionLength || !handler.CanReadToken(jwt))
        {
            return Unauthorized();
        }

        var result = await handler.ValidateTokenAsync(jwt, new TokenValidationParameters
        {
            ValidIssuer = issuer,
            ValidAudience = issuer,
            IssuerSigningKeys = await keys.ValidationKeysAsync(ct),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireExpirationTime = true,
            LifetimeValidator = (notBefore, expires, _, _) =>
                expires is { } end && time.GetUtcNow().UtcDateTime <= end && (notBefore is not { } start || time.GetUtcNow().UtcDateTime >= start),
        });
        if (!result.IsValid || result.SecurityToken is not JsonWebToken token
            || await tools.FindByClientIdAsync(token.Subject ?? string.Empty, ct) is not { } tool)
        {
            return Unauthorized();
        }

        var scopes = (token.GetPayloadValue<string?>("scope") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        return acceptedScopes.Any(scopes.Contains)
            ? new ServiceCaller(tool, scopes)
            : Error.Forbidden(LtiErrors.ServiceForbidden, "The access token does not allow this.");
    }

    private static Error Unauthorized() => Error.Unauthorized(LtiErrors.ServiceUnauthorized, "The access token is missing or not valid.");
}
