using System.Security.Claims;
using Doctrina.Core.Results;

namespace Doctrina.Modules.Content.Contracts;

/// <summary>A tool link in a section: which offering it is in and which tool it starts.</summary>
public sealed record ToolLinkInfo(Guid ItemId, Guid OfferingId, string Title, Guid ToolId, string? LaunchUrl = null);

/// <summary>A section the user may manage (design D2 of add-lti-deep-linking).</summary>
public sealed record SectionInfo(Guid SectionId, Guid OfferingId);

/// <summary>A tool link to add: its title and the launch address the tool chose.</summary>
public sealed record NewToolLink(string Title, string LaunchUrl);

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

    /// <summary>
    /// The section, if <paramref name="user"/> may manage the content of its offering: <c>Content.Section.NotFound</c> for an
    /// unknown or deleted section, <c>Content.Access.Forbidden</c> without manage rights.
    /// </summary>
    Task<Result<SectionInfo>> FindSectionForManagingAsync(ClaimsPrincipal user, Guid sectionId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the links as draft tool links of <paramref name="toolId"/> at the end of the section, with the same checks as
    /// <see cref="FindSectionForManagingAsync"/>; nothing is added if one of them is invalid.
    /// </summary>
    Task<Result> AddToolLinksAsync(ClaimsPrincipal user, Guid sectionId, Guid toolId, IReadOnlyList<NewToolLink> links, CancellationToken cancellationToken);

    /// <summary>
    /// The active tool links of <paramref name="toolId"/> in the offering, drafts included, ordered by title; empty if the
    /// tool has none there. For a tool that calls Doctrina itself (change add-lti-ags), so there is no user to check.
    /// </summary>
    Task<IReadOnlyList<ToolLinkInfo>> ToolLinksOfToolAsync(Guid offeringId, Guid toolId, CancellationToken cancellationToken);

    /// <summary>Whether the user may manage the content of the offering (for pages of other modules that follow the same rule).</summary>
    Task<bool> CanManageAsync(ClaimsPrincipal user, Guid offeringId, CancellationToken cancellationToken);
}
