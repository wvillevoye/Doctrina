using System.Text.Json;
using Doctrina.Core.Results;
using Doctrina.Modules.Lti.Domain;
using Doctrina.Modules.Lti.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Doctrina.Modules.Lti.Application;

/// <summary>The form of a token request (OAuth 2.0 client credentials with a <c>client_assertion</c>).</summary>
internal sealed record TokenRequest(string? GrantType, string? AssertionType, string? Assertion, string? Scope);

/// <summary>An issued access token.</summary>
internal sealed record IssuedToken(string AccessToken, int ExpiresIn, string Scope);

/// <summary>The scopes of the LTI services Doctrina offers a tool (design D1 and D2 of add-lti-ags).</summary>
internal static class LtiScopes
{
    private const string Ags = "https://purl.imsglobal.org/spec/lti-ags/scope/";

    public const string LineItem = Ags + "lineitem";
    public const string LineItemReadOnly = Ags + "lineitem.readonly";
    public const string ResultReadOnly = Ags + "result.readonly";
    public const string Score = Ags + "score";
    public const string MembershipReadOnly = "https://purl.imsglobal.org/spec/lti-nrps/scope/contextmembership.readonly";

    /// <summary>The scopes a tool may be given: the four AGS scopes with grade services, the NRPS scope with membership services.</summary>
    public static IReadOnlyList<string> AllowedFor(LtiTool tool) =>
        [.. tool.GradeServices ? [LineItem, LineItemReadOnly, ResultReadOnly, Score] : Array.Empty<string>(), .. tool.MembershipServices ? [MembershipReadOnly] : Array.Empty<string>()];
}

/// <summary>
/// The token endpoint (design D1 and D2 of add-lti-ags): a tool signs a <c>client_assertion</c> with its own key and gets a
/// short-lived access token, a JWT signed with the platform key. Everything that is wrong with the assertion is reported as
/// <c>Lti.ServiceAuth.InvalidClient</c> without saying which check failed. Nothing of the assertion or token is logged.
/// </summary>
internal sealed class ServiceAuthService(ToolService tools, JwksClient jwks, KeyService keys, LtiDbContext db, TimeProvider time)
{
    public const string AssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
    public const int MaxAssertionLength = 8 * 1024;
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan MaxAssertionLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<IssuedToken>> IssueAsync(TokenRequest request, string issuer, CancellationToken ct)
    {
        if (request.GrantType != "client_credentials")
        {
            return Error.Validation(LtiErrors.ServiceUnsupportedGrant, "Use grant_type=client_credentials.");
        }

        if (request.AssertionType != AssertionType || string.IsNullOrWhiteSpace(request.Assertion) || request.Assertion.Length > MaxAssertionLength)
        {
            return Error.Validation(LtiErrors.ServiceInvalidRequest, "Send a client_assertion of type jwt-bearer.");
        }

        var handler = new JsonWebTokenHandler();
        if (!handler.CanReadToken(request.Assertion))
        {
            return InvalidClient();
        }

        // Only to find the tool; nothing in the assertion is trusted before the signature is checked.
        var unverified = new JsonWebToken(request.Assertion);
        if (await tools.FindByClientIdAsync(unverified.Issuer ?? string.Empty, ct) is not { PublicKeysUrl: not null } tool)
        {
            return InvalidClient();
        }

        var assertion = await ToolJwt.ValidateAsync(jwks, time, handler, request.Assertion, unverified.Kid, tool, TokenEndpoint(issuer), ct);
        var jti = assertion?.GetPayloadValue<string?>("jti");
        if (assertion is null
            || assertion.Subject != tool.ClientId
            || string.IsNullOrEmpty(jti)
            || assertion.ValidTo - assertion.IssuedAt > MaxAssertionLifetime + TimeSpan.FromMinutes(1))
        {
            return InvalidClient();
        }

        if (!await NonceStore.UseAsync(db, NoncePurpose.ClientAssertion, tool.Id, jti, assertion.ValidTo, time.GetUtcNow(), ct))
        {
            return InvalidClient();
        }

        var allowed = LtiScopes.AllowedFor(tool);
        var granted = (request.Scope ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().Where(allowed.Contains).ToList();
        if (granted.Count == 0)
        {
            return Error.Validation(LtiErrors.ServiceInvalidScope, "None of the requested scopes is allowed for this tool.");
        }

        var now = time.GetUtcNow();
        var scope = string.Join(' ', granted);
        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["iss"] = issuer,
            ["sub"] = tool.ClientId,
            ["aud"] = issuer,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.Add(TokenLifetime).ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["scope"] = scope,
        });
        var token = handler.CreateToken(payload, new SigningCredentials(await keys.ActiveAsync(ct), SecurityAlgorithms.RsaSha256));
        return new IssuedToken(token, (int)TokenLifetime.TotalSeconds, scope);
    }

    /// <summary>The address of the token endpoint, which is the audience of a client assertion.</summary>
    public static string TokenEndpoint(string issuer) => issuer.TrimEnd('/') + "/lti/token";

    private static Error InvalidClient() => Error.Validation(LtiErrors.ServiceInvalidClient, "The tool could not be authenticated.");
}
