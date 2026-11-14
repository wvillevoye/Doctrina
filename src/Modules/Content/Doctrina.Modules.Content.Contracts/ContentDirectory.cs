using System.Security.Claims;
using Doctrina.Core.Results;

namespace Doctrina.Modules.Content.Contracts;

/// <summary>A tool link in a section: which offering it is in and which tool it starts.</summary>
public sealed record ToolLinkInfo(Guid ItemId, Guid OfferingId, string Title, Guid ToolId);

/// <summary>Public contract of the Content module for other modules (design D1 of add-lti-platform).</summary>
public interface IContentDirectory
{
    /// <summary>Error code when the item doesn't exist, isn't a tool link or isn't visible to the user.</summary>
    public const string ItemNotFoundCode = "Content.Item.NotFound";

    /// <summary>Error code when the user may not read the content of the offering.</summary>
    public const string AccessForbiddenCode = "Content.Access.Forbidden";

    /// <summary>
    /// The tool link with the given id, with the read rights of <paramref name="user"/>: <c>Content.Access.Forbidden</c>
    /// without read rights, <c>Content.Item.NotFound</c> for an unknown, deleted or (for participants) unpublished item
    /// or an item that isn't a tool link. Users who manage the content also see drafts.
    /// </summary>
    Task<Result<ToolLinkInfo>> FindToolLinkAsync(ClaimsPrincipal user, Guid itemId, CancellationToken cancellationToken);
}
