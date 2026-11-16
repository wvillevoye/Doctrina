using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Contracts;
using Doctrina.Modules.Lti.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Doctrina.Modules.Lti.Application;

/// <summary>The authentication request of a tool (the query of <c>/lti/authorize</c>).</summary>
internal sealed record AuthRequest(
    string? Scope, string? ResponseType, string? ResponseMode, string? Prompt, string? ClientId,
    string? RedirectUri, string? LoginHint, string? MessageHint, string? Nonce, string? State);

/// <summary>What the browser posts to the tool: the signed <c>id_token</c> and the <c>state</c> of the tool.</summary>
internal sealed record LaunchForm(string RedirectUri, string IdToken, string State);

/// <summary>
/// The launch flow of an LTI resource link (design D5 of add-lti-platform): login initiation in
/// <see cref="StartAsync"/>, the authentication request of the tool in <see cref="AuthorizeAsync"/>. Nothing personal
/// (names, hints, tokens) is logged.
/// </summary>
internal sealed class LaunchService(
    IContentDirectory content,
    ToolService tools,
    IAcademicDirectory directory,
    Infrastructure.LtiDbContext db,
    KeyService keys,
    IDataProtectionProvider dataProtection,
    TimeProvider time)
{
    public const string HintPurpose = "Lti.MessageHint";

    /// <summary>How long an <c>lti_message_hint</c> is valid (design D8).</summary>
    public static readonly TimeSpan HintLifetime = TimeSpan.FromMinutes(5);

    /// <summary>How long an <c>id_token</c> is valid (spec: at most 5 minutes).</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);

    private const char ResourceHint = 'r';
    private const char DeepLinkingHint = 'd';

    private IDataProtector HintProtector => dataProtection.CreateProtector(HintPurpose);

    /// <summary>
    /// Login initiation for a tool link: checks that the user may read it and returns the redirect to the login URL of
    /// the tool with <c>iss</c>, <c>login_hint</c>, <c>target_link_uri</c>, <c>lti_message_hint</c>, <c>client_id</c> and
    /// <c>lti_deployment_id</c>.
    /// </summary>
    public async Task<Result<Uri>> StartAsync(ClaimsPrincipal user, Guid itemId, string issuer, CancellationToken ct)
    {
        var link = await content.FindToolLinkAsync(user, itemId, ct);
        if (link.Error is { } denied)
        {
            return denied;
        }

        var tool = await tools.FindAsync(link.Value!.ToolId, ct);
        if (tool is null)
        {
            return ToolService.NotFound(link.Value.ToolId);
        }

        return LoginUrl(tool, link.Value.LaunchUrl ?? tool.LaunchUrl, issuer, ResourceHint, itemId, await SubjectAsync(user, ct));
    }

    /// <summary>
    /// Login initiation for a deep linking request (design D2 of add-lti-deep-linking): the user must be allowed to manage
    /// the content of the section; the tool must support deep linking.
    /// </summary>
    public async Task<Result<Uri>> StartDeepLinkingAsync(ClaimsPrincipal user, Guid sectionId, Guid toolId, string issuer, CancellationToken ct)
    {
        var section = await content.FindSectionForManagingAsync(user, sectionId, ct);
        if (section.Error is { } denied)
        {
            return denied;
        }

        if (await tools.FindAsync(toolId, ct) is not { } tool)
        {
            return ToolService.NotFound(toolId);
        }

        return tool.DeepLinkingUrl is null
            ? Error.NotFound(LtiErrors.DeepLinkNotSupported, $"Tool {toolId:D} does not support deep linking.")
            : LoginUrl(tool, tool.DeepLinkingUrl, issuer, DeepLinkingHint, sectionId, await SubjectAsync(user, ct));
    }

    private Uri LoginUrl(LtiTool tool, string targetLinkUri, string issuer, char kind, Guid target, string subject)
    {
        var expires = time.GetUtcNow().Add(HintLifetime).ToUnixTimeSeconds();
        var hint = HintProtector.Protect(string.Create(CultureInfo.InvariantCulture, $"{kind}.{target:N}.{tool.Id:N}.{subject}.{expires}"));
        return new Uri(QueryHelpers.AddQueryString(tool.LoginUrl, new Dictionary<string, string?>
        {
            ["iss"] = issuer,
            ["login_hint"] = subject,
            ["target_link_uri"] = targetLinkUri,
            ["lti_message_hint"] = hint,
            ["client_id"] = tool.ClientId,
            ["lti_deployment_id"] = tool.DeploymentId,
        }));
    }

    /// <summary>
    /// The authentication request of the tool: validates it in the order of design D5 (never redirecting for an invalid
    /// request) and returns the signed <c>id_token</c> for the form that is posted to the tool.
    /// </summary>
    public async Task<Result<LaunchForm>> AuthorizeAsync(ClaimsPrincipal user, AuthRequest request, string issuer, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.ClientId) || await tools.FindByClientIdAsync(request.ClientId, ct) is not { } tool)
        {
            return Bad(LtiErrors.LaunchUnknownClient, "client_id", "The client_id is not known.");
        }

        // Before anything that could redirect: only a registered address ever receives a launch.
        if (string.IsNullOrEmpty(request.RedirectUri) || !tool.RedirectUris.Split('\n').Contains(request.RedirectUri, StringComparer.Ordinal))
        {
            return Bad(LtiErrors.LaunchInvalidRedirectUri, "redirect_uri", "The redirect_uri is not registered for this tool.");
        }

        if (request.Scope != "openid" || request.ResponseType != "id_token" || request.ResponseMode != "form_post" || request.Prompt != "none")
        {
            return Bad(LtiErrors.LaunchInvalidRequest, "request", "Use scope=openid, response_type=id_token, response_mode=form_post and prompt=none.");
        }

        if (string.IsNullOrEmpty(request.Nonce) || string.IsNullOrEmpty(request.State) || string.IsNullOrEmpty(request.LoginHint) || string.IsNullOrEmpty(request.MessageHint))
        {
            return Bad(LtiErrors.LaunchInvalidRequest, "request", "The nonce, state, login_hint and lti_message_hint are required.");
        }

        if (OpenHint(request.MessageHint) is not var (kind, target, hintTool, subject))
        {
            return Bad(LtiErrors.LaunchInvalidHint, "lti_message_hint", "The lti_message_hint is not valid or has expired.");
        }

        if (subject != await SubjectAsync(user, ct) || request.LoginHint != subject)
        {
            return Error.Forbidden(LtiErrors.LaunchUserMismatch, "The request belongs to another user.");
        }

        Guid offeringId;
        ToolLinkInfo? link = null;
        if (kind == DeepLinkingHint)
        {
            // The user may have lost the right to manage the section since the login initiation.
            var section = await content.FindSectionForManagingAsync(user, target, ct);
            if (section.IsFailure || hintTool != tool.Id || tool.DeepLinkingUrl is null)
            {
                return Error.Forbidden(LtiErrors.LaunchForbidden, "The section isn't available to you.");
            }

            offeringId = section.Value!.OfferingId;
        }
        else
        {
            // The item may have been hidden or deleted since the login initiation.
            var found = await content.FindToolLinkAsync(user, target, ct);
            if (found.IsFailure || found.Value!.ToolId != tool.Id)
            {
                return Error.Forbidden(LtiErrors.LaunchForbidden, "The tool link isn't available to you.");
            }

            link = found.Value;
            offeringId = link.OfferingId;
        }

        var offering = await directory.FindOfferingAsync(offeringId, ct);
        if (offering.Error is { } missing)
        {
            return missing;
        }

        var roles = await directory.CurrentRolesAsync(user, offeringId, ct);
        var person = tool.SendName || tool.SendEmail
            ? await FindPersonAsync(user, ct)
            : null;
        var now = time.GetUtcNow();
        var (name, given, family, mail) = (tool.SendName ? person?.Name : null, tool.SendName ? person?.GivenName : null, tool.SendName ? person?.FamilyName : null, tool.SendEmail ? person?.Mail : null);
        var lineItemId = link is not null && tool.GradeServices
            ? await db.LineItems.Where(l => l.ToolId == tool.Id && l.ResourceLinkId == link.ItemId).OrderBy(l => l.Id).Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct)
            : null;
        var payload = link is null
            ? IdToken.DeepLinkingPayload(issuer, tool, subject, request.Nonce, now, TokenLifetime, new DeepLinkData(dataProtection, time).Protect(target, tool.Id, subject), offering.Value!, roles, name, given, family, mail)
            : IdToken.Payload(issuer, tool, subject, request.Nonce, now, TokenLifetime, link, offering.Value!, roles, name, given, family, mail, lineItemId);
        var key = await keys.ActiveAsync(ct);
        var token = new JsonWebTokenHandler().CreateToken(payload, new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        return new LaunchForm(request.RedirectUri, token, request.State);
    }

    /// <summary>
    /// An opaque identifier of the signed-in user: the person in Academic, or for users without a person (e.g. an
    /// administrator) a hash of their <c>sub</c>. Never an e-mail address or an external id.
    /// </summary>
    public async Task<string> SubjectAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        if (await directory.CurrentPersonIdAsync(user, ct) is { } person)
        {
            return person.ToString("N");
        }

        var sub = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sub)))[..32];
    }

    private async Task<SharedPerson?> FindPersonAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        if (await directory.CurrentPersonIdAsync(user, ct) is { } id && await directory.FindPersonAsync(id, ct) is { } person)
        {
            return new SharedPerson(person.Name, person.GivenName, person.FamilyName, person.Mail ?? user.FindFirst("email")?.Value);
        }

        // No person in Academic: what the identity provider says about the user.
        return new SharedPerson(user.FindFirst("name")?.Value, user.FindFirst("given_name")?.Value, user.FindFirst("family_name")?.Value, user.FindFirst("email")?.Value);
    }

    /// <summary>
    /// The kind (resource link or deep linking), target (item or section), tool and user of a valid, unexpired
    /// <c>lti_message_hint</c>; <see langword="null"/> if it is not one of ours or has expired.
    /// </summary>
    private (char Kind, Guid Target, Guid ToolId, string Subject)? OpenHint(string hint)
    {
        try
        {
            var parts = HintProtector.Unprotect(hint).Split('.');
            return parts.Length == 5
                && parts[0] is [ResourceHint or DeepLinkingHint]
                && Guid.TryParseExact(parts[1], "N", out var target)
                && Guid.TryParseExact(parts[2], "N", out var toolId)
                && long.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
                && time.GetUtcNow().ToUnixTimeSeconds() < expires
                    ? (parts[0][0], target, toolId, parts[3])
                    : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static Error Bad(string code, string field, string message) => LtiErrors.Invalid(code, field, message);

    private sealed record SharedPerson(string? Name, string? GivenName, string? FamilyName, string? Mail);
}
