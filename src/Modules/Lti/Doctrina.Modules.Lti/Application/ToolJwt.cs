using Doctrina.Modules.Lti.Domain;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Doctrina.Modules.Lti.Application;

/// <summary>Checks a JWT a tool signed with the keys of its <c>PublicKeysUrl</c> (deep linking responses, client assertions).</summary>
internal static class ToolJwt
{
    /// <summary>
    /// Signature (RS256 against the JWKS of the tool), issuer (the client id), the given audience and lifetime (60 seconds of
    /// slack). An unknown <c>kid</c> triggers one refresh of the keys. <see langword="null"/> if anything is wrong.
    /// </summary>
    public static async Task<JsonWebToken?> ValidateAsync(
        JwksClient jwks, TimeProvider time, JsonWebTokenHandler handler, string jwt, string? kid, LtiTool tool, string audience, CancellationToken ct)
    {
        var keys = await jwks.GetAsync(tool, refresh: false, ct);
        if (keys is not null && (kid is null || !keys.Keys.Any(k => k.Kid == kid)))
        {
            keys = await jwks.GetAsync(tool, refresh: true, ct) ?? keys;
        }

        if (keys is null)
        {
            return null;
        }

        var result = await handler.ValidateTokenAsync(jwt, new TokenValidationParameters
        {
            ValidIssuer = tool.ClientId,
            ValidAudience = audience,
            IssuerSigningKeys = keys.GetSigningKeys(),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireExpirationTime = true,
            LifetimeValidator = (notBefore, expires, _, _) =>
                expires is { } end && time.GetUtcNow().UtcDateTime <= end.AddSeconds(60) && (notBefore is not { } start || time.GetUtcNow().UtcDateTime >= start.AddSeconds(-60)),
        });
        return result.IsValid ? (JsonWebToken)result.SecurityToken : null;
    }
}
