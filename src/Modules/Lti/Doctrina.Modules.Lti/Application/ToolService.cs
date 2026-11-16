using System.Security.Cryptography;
using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Modules.Lti.Contracts;
using Doctrina.Modules.Lti.Domain;
using Doctrina.Modules.Lti.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Lti.Application;

/// <summary>Error codes of the Lti module (titles in Resources/LtiErrors.resx).</summary>
internal static class LtiErrors
{
    public const string ToolInvalid = "Lti.Tool.Invalid";
    public const string ToolNameRequired = "Lti.Tool.NameRequired";
    public const string ToolInvalidUrl = "Lti.Tool.InvalidUrl";
    public const string ToolRedirectUriRequired = "Lti.Tool.RedirectUriRequired";
    public const string ToolNotFound = "Lti.Tool.NotFound";
    public const string DeepLinkNotSupported = "Lti.DeepLinking.NotSupported";
    public const string DeepLinkInvalidToken = "Lti.DeepLinking.InvalidToken";
    public const string DeepLinkInvalidData = "Lti.DeepLinking.InvalidData";
    public const string DeepLinkInvalidItem = "Lti.DeepLinking.InvalidItem";
    public const string DeepLinkReplayed = "Lti.DeepLinking.Replayed";
    public const string DeepLinkSelectionNotFound = "Lti.DeepLinking.SelectionNotFound";
    public const string DeepLinkUserMismatch = "Lti.DeepLinking.UserMismatch";
    public const string ToolDeepLinkingUrlRequired = "Lti.Tool.DeepLinkingUrlRequired";
    public const string ToolPublicKeysUrlRequired = "Lti.Tool.PublicKeysUrlRequired";
    public const string LaunchUnknownClient = "Lti.Launch.UnknownClient";
    public const string LaunchInvalidRedirectUri = "Lti.Launch.InvalidRedirectUri";
    public const string LaunchInvalidHint = "Lti.Launch.InvalidHint";
    public const string LaunchUserMismatch = "Lti.Launch.UserMismatch";
    public const string LaunchInvalidRequest = "Lti.Launch.InvalidRequest";
    public const string LaunchForbidden = "Lti.Launch.Forbidden";

    public static readonly string[] All =
    [
        ToolInvalid, ToolNameRequired, ToolInvalidUrl, ToolRedirectUriRequired, ToolNotFound, ToolDeepLinkingUrlRequired, ToolPublicKeysUrlRequired,
        DeepLinkNotSupported, DeepLinkInvalidToken, DeepLinkInvalidData, DeepLinkInvalidItem, DeepLinkReplayed, DeepLinkSelectionNotFound, DeepLinkUserMismatch,
        LaunchUnknownClient, LaunchInvalidRedirectUri, LaunchInvalidHint, LaunchUserMismatch, LaunchInvalidRequest, LaunchForbidden,
    ];

    public static Error Invalid(string code, string field, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>An address of a tool: absolute, <c>https</c>, or <c>http</c> for localhost (design D3).</summary>
internal static class LtiUrl
{
    public static bool IsValid(string? address) =>
        address is not null
        && address.Length <= LtiDbContext.UrlMaxLength
        && Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps
            || (uri.Scheme == Uri.UriSchemeHttp && uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)));
}

/// <summary>What a beheerder fills in for a tool; the redirect URIs are one address per line.</summary>
internal sealed record ToolInput(string? Name, string? LoginUrl, string? LaunchUrl, string? RedirectUris, bool SendName = false, bool SendEmail = false, string? DeepLinkingUrl = null, string? PublicKeysUrl = null)
{
    /// <summary>The redirect URIs, trimmed, without empty lines and duplicates.</summary>
    public IReadOnlyList<string> RedirectUriList() =>
        [.. (RedirectUris ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal)];
}

/// <summary>
/// Registering, changing and deleting tools (design D3 of add-lti-platform) and the public catalog for other modules.
/// Every method saves once and returns a <see cref="Result"/>.
/// </summary>
internal sealed class ToolService(LtiDbContext db) : ILtiToolCatalog
{
    public const string DoctrinaSource = "Doctrina";

    public async Task<IReadOnlyList<LtiTool>> ToolsAsync(CancellationToken ct) =>
        await db.Tools.AsNoTracking().Where(t => t.Status == EntityStatus.Active).OrderBy(t => t.Name).ToListAsync(ct);

    public Task<LtiTool?> FindAsync(Guid id, CancellationToken ct) =>
        db.Tools.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.Status == EntityStatus.Active, ct);

    public Task<LtiTool?> FindByClientIdAsync(string clientId, CancellationToken ct) =>
        db.Tools.AsNoTracking().FirstOrDefaultAsync(t => t.ClientId == clientId && t.Status == EntityStatus.Active, ct);

    async Task<IReadOnlyList<LtiToolSummary>> ILtiToolCatalog.ListAsync(CancellationToken cancellationToken) =>
        [.. (await ToolsAsync(cancellationToken)).Select(t => new LtiToolSummary(t.Id, t.Name, t.DeepLinkingUrl is not null))];

    public Task<bool> ExistsAsync(Guid toolId, CancellationToken cancellationToken) =>
        db.Tools.AnyAsync(t => t.Id == toolId && t.Status == EntityStatus.Active, cancellationToken);

    public async Task<Result<LtiTool>> CreateAsync(ToolInput input, CancellationToken ct)
    {
        if (Check(input) is { } invalid)
        {
            return invalid;
        }

        var tool = new LtiTool
        {
            ExternalId = string.Empty,
            Source = DoctrinaSource,
            Name = input.Name!.Trim(),
            ClientId = NewIdentifier(),
            DeploymentId = NewIdentifier(),
            LoginUrl = input.LoginUrl!.Trim(),
            LaunchUrl = input.LaunchUrl!.Trim(),
            RedirectUris = string.Join('\n', input.RedirectUriList()),
            SendName = input.SendName,
            SendEmail = input.SendEmail,
            DeepLinkingUrl = Blank(input.DeepLinkingUrl),
            PublicKeysUrl = Blank(input.PublicKeysUrl),
        };
        db.Add(tool);
        // EF Core generates the sequential Id on Add; the Doctrina-owned ExternalId is that Id (huisregel 6).
        tool.ExternalId = tool.Id.ToString("D");
        await db.SaveChangesAsync(ct);
        return tool;
    }

    /// <summary>Changes a tool; its client id and deployment id stay the same.</summary>
    public async Task<Result> UpdateAsync(Guid id, ToolInput input, CancellationToken ct)
    {
        var tool = await db.Tools.FirstOrDefaultAsync(t => t.Id == id && t.Status == EntityStatus.Active, ct);
        if (tool is null)
        {
            return NotFound(id);
        }

        if (Check(input) is { } invalid)
        {
            return invalid;
        }

        tool.Name = input.Name!.Trim();
        tool.LoginUrl = input.LoginUrl!.Trim();
        tool.LaunchUrl = input.LaunchUrl!.Trim();
        tool.RedirectUris = string.Join('\n', input.RedirectUriList());
        tool.SendName = input.SendName;
        tool.SendEmail = input.SendEmail;
        tool.DeepLinkingUrl = Blank(input.DeepLinkingUrl);
        tool.PublicKeysUrl = Blank(input.PublicKeysUrl);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
    {
        var tool = await db.Tools.FirstOrDefaultAsync(t => t.Id == id && t.Status == EntityStatus.Active, ct);
        if (tool is null)
        {
            return NotFound(id);
        }

        tool.Status = EntityStatus.ToBeDeleted;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public static Error NotFound(Guid id) => Error.NotFound(LtiErrors.ToolNotFound, $"Tool {id:D} was not found.");

    private static Error? Check(ToolInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > LtiDbContext.NameMaxLength)
        {
            return LtiErrors.Invalid(LtiErrors.ToolNameRequired, "name", "Enter a name of at most 200 characters.");
        }

        var redirects = input.RedirectUriList();
        if (redirects.Count == 0)
        {
            return LtiErrors.Invalid(LtiErrors.ToolRedirectUriRequired, "redirectUris", "Enter at least one redirect URI.");
        }

        const string message = "Enter an address that starts with https:// (http:// is only allowed for localhost).";
        if (!LtiUrl.IsValid(input.LoginUrl?.Trim()))
        {
            return LtiErrors.Invalid(LtiErrors.ToolInvalidUrl, "loginUrl", message);
        }

        if (!LtiUrl.IsValid(input.LaunchUrl?.Trim()))
        {
            return LtiErrors.Invalid(LtiErrors.ToolInvalidUrl, "launchUrl", message);
        }

        if (redirects.Any(r => !LtiUrl.IsValid(r)) || string.Join('\n', redirects).Length > LtiDbContext.RedirectUrisMaxLength)
        {
            return LtiErrors.Invalid(LtiErrors.ToolInvalidUrl, "redirectUris", message);
        }

        var (deepLinking, keys) = (Blank(input.DeepLinkingUrl), Blank(input.PublicKeysUrl));
        if (deepLinking is null != keys is null)
        {
            return deepLinking is null
                ? LtiErrors.Invalid(LtiErrors.ToolDeepLinkingUrlRequired, "deepLinkingUrl", "Enter the deep linking URL as well, or clear the public keys URL.")
                : LtiErrors.Invalid(LtiErrors.ToolPublicKeysUrlRequired, "publicKeysUrl", "Enter the public keys URL as well, or clear the deep linking URL.");
        }

        if (deepLinking is not null && !LtiUrl.IsValid(deepLinking))
        {
            return LtiErrors.Invalid(LtiErrors.ToolInvalidUrl, "deepLinkingUrl", message);
        }

        if (keys is not null && !LtiUrl.IsValid(keys))
        {
            return LtiErrors.Invalid(LtiErrors.ToolInvalidUrl, "publicKeysUrl", message);
        }

        return null;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>An opaque identifier: 24 random bytes, base64url (32 characters).</summary>
    private static string NewIdentifier() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
}
