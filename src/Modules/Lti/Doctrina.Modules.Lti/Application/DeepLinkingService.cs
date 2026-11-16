using System.Text.Json;
using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Modules.Lti.Domain;
using Doctrina.Modules.Lti.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Doctrina.Modules.Lti.Application;

/// <summary>A content item the tool chose.</summary>
internal sealed record DeepLinkItem(string Title, string Url);

/// <summary>What the response of a tool led to: the pending selection (null if the tool chose nothing) and the section.</summary>
internal sealed record DeepLinkReturn(Guid? SelectionId, Guid SectionId);

/// <summary>
/// The response of a tool to a deep linking request (design D3 and D4 of add-lti-deep-linking): checks the signed JWT and
/// keeps the chosen items as a pending selection until the teacher confirms. Nothing personal (names, hints, nonces,
/// tokens, titles or addresses of items) is logged.
/// </summary>
internal sealed class DeepLinkingService(
    LtiDbContext db, ToolService tools, JwksClient jwks, IDataProtectionProvider dataProtection, TimeProvider time)
{
    private const string Claim = "https://purl.imsglobal.org/spec/lti/claim/";
    private const string DeepLinkingClaim = "https://purl.imsglobal.org/spec/lti-dl/claim/";
    public const int MaxTokenLength = 32 * 1024;
    public const int MaxItems = 50;
    public const int MaxTitleLength = 200;

    /// <summary>How long a pending selection stays valid.</summary>
    public static readonly TimeSpan SelectionLifetime = TimeSpan.FromMinutes(15);

    private DeepLinkData Data => new(dataProtection, time);

    public async Task<Result<DeepLinkReturn>> ReturnAsync(string? jwt, string issuer, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(jwt) || jwt.Length > MaxTokenLength)
        {
            return Refused(LtiErrors.DeepLinkInvalidToken, "The response of the tool is missing or too large.");
        }

        var handler = new JsonWebTokenHandler();
        if (!handler.CanReadToken(jwt))
        {
            return Refused(LtiErrors.DeepLinkInvalidToken, "The response of the tool is not a JWT.");
        }

        // Only to find the tool; nothing in the token is trusted before the signature is checked.
        var unverified = new JsonWebToken(jwt);
        if (await tools.FindByClientIdAsync(unverified.Issuer ?? string.Empty, ct) is not { PublicKeysUrl: not null } tool)
        {
            return Refused(LtiErrors.DeepLinkInvalidToken, "The response of the tool is not valid.");
        }

        var token = await ValidateAsync(handler, jwt, unverified.Kid, tool, issuer, ct);
        if (token is null
            || token.GetPayloadValue<string?>(Claim + "message_type") != "LtiDeepLinkingResponse"
            || token.GetPayloadValue<string?>(Claim + "version") != "1.3.0"
            || token.GetPayloadValue<string?>(Claim + "deployment_id") != tool.DeploymentId
            || string.IsNullOrEmpty(token.GetPayloadValue<string?>("nonce")))
        {
            return Refused(LtiErrors.DeepLinkInvalidToken, "The response of the tool is not valid.");
        }

        if (Data.Open(token.GetPayloadValue<string?>(DeepLinkingClaim + "data")) is not var (sectionId, toolId, subject) || toolId != tool.Id)
        {
            return Refused(LtiErrors.DeepLinkInvalidData, "The data of the deep linking request is not valid or has expired.");
        }

        var items = Items(token, tool);
        if (items.Error is { } invalid)
        {
            return invalid;
        }

        var now = time.GetUtcNow();
        if (!await NonceStore.UseAsync(db, NoncePurpose.DeepLinking, tool.Id, token.GetPayloadValue<string>("nonce"), token.ValidTo, now, ct))
        {
            return Refused(LtiErrors.DeepLinkReplayed, "The response of the tool was already processed.");
        }

        if (items.Value!.Count == 0)
        {
            return new DeepLinkReturn(null, sectionId);
        }

        await db.Selections.Where(s => s.Expires < now).ExecuteDeleteAsync(ct);
        var selection = new DeepLinkSelection
        {
            ToolId = tool.Id,
            SectionId = sectionId,
            Subject = subject,
            ItemsJson = JsonSerializer.Serialize(items.Value),
            Expires = now.Add(SelectionLifetime),
        };
        db.Add(selection);
        await db.SaveChangesAsync(ct);
        return new DeepLinkReturn(selection.Id, sectionId);
    }

    /// <summary>The unexpired selection, or <c>Lti.DeepLinking.SelectionNotFound</c>.</summary>
    public async Task<Result<(DeepLinkSelection Selection, IReadOnlyList<DeepLinkItem> Items)>> FindSelectionAsync(Guid id, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var selection = await db.Selections.FirstOrDefaultAsync(s => s.Id == id && s.Expires > now, ct);
        return selection is null
            ? Error.NotFound(LtiErrors.DeepLinkSelectionNotFound, "The selection was not found or has expired.")
            : (selection, JsonSerializer.Deserialize<List<DeepLinkItem>>(selection.ItemsJson) ?? []);
    }

    /// <summary>Removes the selection once it is added to the section.</summary>
    public async Task DiscardSelectionAsync(Guid id, CancellationToken ct) =>
        await db.Selections.Where(s => s.Id == id).ExecuteDeleteAsync(ct);

    private Task<JsonWebToken?> ValidateAsync(JsonWebTokenHandler handler, string jwt, string? kid, LtiTool tool, string issuer, CancellationToken ct) =>
        ToolJwt.ValidateAsync(jwks, time, handler, jwt, kid, tool, issuer, ct);

    /// <summary>The <c>ltiResourceLink</c> items of the response, with the address on the origin of the launch URL (design D3, step 6).</summary>
    private static Result<List<DeepLinkItem>> Items(JsonWebToken token, LtiTool tool)
    {
        var items = new List<DeepLinkItem>();
        if (!token.TryGetPayloadValue<JsonElement>(DeepLinkingClaim + "content_items", out var array))
        {
            return items;
        }

        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > MaxItems)
        {
            return Refused(LtiErrors.DeepLinkInvalidItem, "The content items are not valid.");
        }

        var origin = new Uri(tool.LaunchUrl);
        foreach (var item in array.EnumerateArray())
        {
            var url = Text(item, "url");
            var title = Text(item, "title")?.Trim();
            if (Text(item, "type") != "ltiResourceLink"
                || !LtiUrl.IsValid(url)
                || !SameOrigin(origin, new Uri(url!))
                || title?.Length > MaxTitleLength)
            {
                return Refused(LtiErrors.DeepLinkInvalidItem, "A content item is not a resource link on the address of the tool.");
            }

            items.Add(new DeepLinkItem(string.IsNullOrEmpty(title) ? tool.Name : title, url!));
        }

        return items;
    }

    private static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool SameOrigin(Uri a, Uri b) => a.Scheme == b.Scheme && a.Host.Equals(b.Host, StringComparison.OrdinalIgnoreCase) && a.Port == b.Port;

    private static Error Refused(string code, string message) => LtiErrors.Invalid(code, "request", message);
}
