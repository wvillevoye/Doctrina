using System.Security.Claims;
using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Infrastructure.Authentication;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Contracts;
using Doctrina.Modules.Content.Domain;
using Doctrina.Modules.Content.Infrastructure;
using Doctrina.Modules.Lti.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Content.Application;

/// <summary>Error codes of the Content module (titles in Resources/ContentErrors.resx).</summary>
internal static class ContentErrors
{
    public const string SectionInvalid = "Content.Section.Invalid";
    public const string SectionNotFound = "Content.Section.NotFound";
    public const string ItemInvalid = "Content.Item.Invalid";
    public const string ItemNotFound = "Content.Item.NotFound";
    public const string FileTooLarge = "Content.File.TooLarge";
    public const string FileMissing = "Content.File.Missing";
    public const string UnknownTool = "Content.Item.UnknownTool";
    public const string CopyInvalidSource = "Content.Copy.InvalidSource";
    public const string CopyTargetNotEmpty = "Content.Copy.TargetNotEmpty";
    public const string IntroductionInvalid = "Content.Introduction.Invalid";
    public const string TemplateInvalid = "Content.Template.Invalid";
    public const string TemplateNotFound = "Content.Template.NotFound";
    public const string TemplateDefaultProtected = "Content.Template.DefaultProtected";
    public const string AccessForbidden = IContentDirectory.AccessForbiddenCode;

    public static readonly string[] All =
    [
        SectionInvalid, SectionNotFound, ItemInvalid, ItemNotFound, FileTooLarge, FileMissing, UnknownTool, CopyInvalidSource, CopyTargetNotEmpty,
        IntroductionInvalid, TemplateInvalid, TemplateNotFound, TemplateDefaultProtected, AccessForbidden,
    ];

    public static Error Invalid(string code, string field, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>What a copy made: sections and items created, and items that could not be copied (design D5 of add-course-templates).</summary>
internal sealed record CopyResult(int Sections, int Items, int Skipped);

/// <summary>
/// All changes to sections and items (design D3 of add-content). Every method saves once and returns a
/// <see cref="Result"/>. Reading goes through <see cref="SectionsAsync"/> and <see cref="FindItemAsync"/>, so the
/// visibility rule lives in one place.
/// </summary>
internal sealed class ContentService(ContentDbContext db, IAcademicDirectory directory, ContentFileStore files, ILtiToolCatalog? tools = null)
{
    private readonly ContentAccess _access = new(directory);

    public const string DoctrinaSource = "Doctrina";
    public const string LinkMessage = "Enter a web address that starts with http:// or https://.";

    /// <summary>
    /// Active sections of the offering with their active items, in order. Without drafts: only published sections,
    /// with only their published items.
    /// </summary>
    public async Task<IReadOnlyList<ContentSection>> SectionsAsync(Guid offeringId, bool includeDrafts, CancellationToken ct)
    {
        var sections = await db.Sections.AsNoTracking()
            .Where(s => s.OfferingId == offeringId && s.Status == EntityStatus.Active && (includeDrafts || s.Published))
            .Include(s => s.Items.Where(i => i.Status == EntityStatus.Active && (includeDrafts || i.Published)))
            .OrderBy(s => s.Position)
            .ToListAsync(ct);
        foreach (var section in sections)
        {
            section.Items.Sort((a, b) => a.Position.CompareTo(b.Position));
        }

        return sections;
    }

    /// <summary>The active item with its section, or <see langword="null"/>; without drafts only when it is visible.</summary>
    public async Task<ContentItem?> FindItemAsync(Guid offeringId, Guid itemId, bool includeDrafts, CancellationToken ct)
    {
        var item = await db.Items.AsNoTracking().Include(i => i.Section)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.Status == EntityStatus.Active && i.Section.OfferingId == offeringId, ct);
        return item is not null && (includeDrafts || item.IsVisible) ? item : null;
    }

    public async Task<Result<ContentSection>> CreateSectionAsync(Guid offeringId, string? title, CancellationToken ct)
    {
        if ((await directory.FindOfferingAsync(offeringId, ct)).Error is { } notFound)
        {
            return notFound;
        }

        if (CheckTitle(ContentErrors.SectionInvalid, title) is { } invalid)
        {
            return invalid;
        }

        var last = await db.Sections.Where(s => s.OfferingId == offeringId).MaxAsync(s => (int?)s.Position, ct) ?? 0;
        var section = New(new ContentSection { ExternalId = string.Empty, Source = DoctrinaSource, Title = title!.Trim() });
        section.OfferingId = offeringId;
        section.Position = last + 1;
        await db.SaveChangesAsync(ct);
        return section;
    }

    public async Task<Result> RenameSectionAsync(Guid sectionId, string? title, CancellationToken ct)
    {
        var section = await ActiveSectionAsync(sectionId, ct);
        if (section is null)
        {
            return SectionNotFound(sectionId);
        }

        if (CheckTitle(ContentErrors.SectionInvalid, title) is { } invalid)
        {
            return invalid;
        }

        section.Title = title!.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetSectionPublishedAsync(Guid sectionId, bool published, CancellationToken ct)
    {
        var section = await ActiveSectionAsync(sectionId, ct);
        if (section is null)
        {
            return SectionNotFound(sectionId);
        }

        section.Published = published;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Deletes the section and its items; their files go from disk after saving (design D5).</summary>
    public async Task<Result> DeleteSectionAsync(Guid sectionId, CancellationToken ct)
    {
        var section = await ActiveSectionAsync(sectionId, ct);
        if (section is null)
        {
            return SectionNotFound(sectionId);
        }

        section.Status = EntityStatus.ToBeDeleted;
        foreach (var item in section.Items)
        {
            item.Status = EntityStatus.ToBeDeleted;
        }

        await db.SaveChangesAsync(ct);
        foreach (var item in section.Items.Where(i => i.Kind == ContentItemKind.File))
        {
            files.Delete(section.OfferingId, item.Id);
        }

        return Result.Success();
    }

    /// <summary>Moves the section one place up (-1) or down (+1) among the active sections; at the edge nothing changes.</summary>
    public async Task<Result> MoveSectionAsync(Guid sectionId, int direction, CancellationToken ct)
    {
        var section = await ActiveSectionAsync(sectionId, ct);
        if (section is null)
        {
            return SectionNotFound(sectionId);
        }

        var siblings = await db.Sections
            .Where(s => s.OfferingId == section.OfferingId && s.Status == EntityStatus.Active)
            .OrderBy(s => s.Position)
            .ToListAsync(ct);
        var index = siblings.FindIndex(s => s.Id == sectionId) + Math.Sign(direction);
        if (index >= 0 && index < siblings.Count)
        {
            (section.Position, siblings[index].Position) = (siblings[index].Position, section.Position);
            await db.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    public Task<Result<ContentItem>> AddPageAsync(Guid sectionId, string? title, CancellationToken ct) =>
        AddItemAsync(sectionId, ContentItemKind.Page, title, item => item.Body = string.Empty, ct);

    public Task<Result<ContentItem>> AddLinkAsync(Guid sectionId, string? title, string? url, CancellationToken ct) =>
        CheckUrl(url) is { } invalid
            ? Task.FromResult<Result<ContentItem>>(invalid)
            : AddItemAsync(sectionId, ContentItemKind.Link, title, item => item.Url = url!.Trim(), ct);

    /// <summary>Adds a link to a registered LTI tool; without the Lti module no tool exists (design D4 of add-lti-platform).</summary>
    public async Task<Result<ContentItem>> AddToolLinkAsync(Guid sectionId, string? title, Guid toolId, CancellationToken ct)
    {
        if (tools is null || !await tools.ExistsAsync(toolId, ct))
        {
            return ContentErrors.Invalid(ContentErrors.UnknownTool, "tool", "Choose a tool.");
        }

        return await AddItemAsync(sectionId, ContentItemKind.ToolLink, title, item => item.ToolId = toolId, ct);
    }

    /// <summary>
    /// Adds draft tool links with the launch address the tool chose, at the end of the section, in one save
    /// (design D5 of add-lti-deep-linking). Nothing is added if a title or address is invalid.
    /// </summary>
    public async Task<Result> AddToolLinksAsync(Guid sectionId, Guid toolId, IReadOnlyList<NewToolLink> links, CancellationToken ct)
    {
        if (tools is null || !await tools.ExistsAsync(toolId, ct))
        {
            return ContentErrors.Invalid(ContentErrors.UnknownTool, "tool", "Choose a tool.");
        }

        var section = await ActiveSectionAsync(sectionId, ct);
        if (section is null)
        {
            return SectionNotFound(sectionId);
        }

        foreach (var link in links)
        {
            if ((CheckTitle(ContentErrors.ItemInvalid, link.Title) ?? CheckUrl(link.LaunchUrl)) is { } invalid)
            {
                return invalid;
            }
        }

        var position = 0;
        foreach (var link in links)
        {
            var item = NewItem(section, ContentItemKind.ToolLink, link.Title.Trim());
            (item.ToolId, item.Url) = (toolId, link.LaunchUrl.Trim());
            position = item.Position = Math.Max(item.Position, position + 1); // NewItem reads the last position from the database, not from the unsaved items
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Adds a file: first the bytes to disk, then the item; a failed save removes the file again (design D5). The
    /// title is the file name unless one is given.
    /// </summary>
    public async Task<Result<ContentItem>> AddFileAsync(Guid sectionId, string? title, string? fileName, string? contentType, Stream content, CancellationToken ct)
    {
        var section = await ActiveSectionAsync(sectionId, ct);
        if (section is null)
        {
            return SectionNotFound(sectionId);
        }

        // Only the last part of the name, whatever separators the browser sent; the name on disk is the item id.
        var name = (fileName ?? string.Empty).Split('/', '\\')[^1].Trim();
        if (name.Length == 0)
        {
            return ContentErrors.Invalid(ContentErrors.ItemInvalid, "file", "Choose a file.");
        }

        name = name.Length > ContentDbContext.FileNameMaxLength ? name[..ContentDbContext.FileNameMaxLength] : name;
        title = string.IsNullOrWhiteSpace(title) ? name : title;
        if (CheckTitle(ContentErrors.ItemInvalid, title.Length > ContentDbContext.TitleMaxLength ? title[..ContentDbContext.TitleMaxLength] : title) is { } invalid)
        {
            return invalid;
        }

        var item = NewItem(section, ContentItemKind.File, title.Length > ContentDbContext.TitleMaxLength ? title[..ContentDbContext.TitleMaxLength] : title.Trim());
        var size = await files.SaveAsync(section.OfferingId, item.Id, content, ct);
        if (size is null)
        {
            return TooLarge(files.MaxSizeMb);
        }

        item.FileName = name;
        item.ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        item.Size = size;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            files.Delete(section.OfferingId, item.Id);
            throw;
        }

        return item;
    }

    /// <summary>
    /// Copies the sections and items of another offering of the same course into an offering without sections, all as
    /// drafts (design D3 to D6 of add-course-templates). Files are copied first and removed again when saving fails.
    /// </summary>
    public async Task<Result<CopyResult>> CopyFromAsync(ClaimsPrincipal user, Guid targetOfferingId, Guid sourceOfferingId, CancellationToken ct)
    {
        if (!await _access.CanManageAsync(user, targetOfferingId, ct))
        {
            return Forbidden();
        }

        if (!(await directory.SiblingOfferingsAsync(targetOfferingId, ct)).Any(o => o.Id == sourceOfferingId))
        {
            return ContentErrors.Invalid(ContentErrors.CopyInvalidSource, "source", "Choose another offering of this course.");
        }

        if (!await _access.CanManageAsync(user, sourceOfferingId, ct))
        {
            return Forbidden();
        }

        if (await db.Sections.AnyAsync(s => s.OfferingId == targetOfferingId && s.Status == EntityStatus.Active, ct))
        {
            return Error.Conflict(ContentErrors.CopyTargetNotEmpty, "This offering already has content.");
        }

        // Deleted sections keep their position, so numbering continues after them.
        var position = await db.Sections.Where(s => s.OfferingId == targetOfferingId).MaxAsync(s => (int?)s.Position, ct) ?? 0;
        var (sections, items, skipped) = (0, 0, 0);
        var copied = new List<Guid>();
        try
        {
            foreach (var from in await SectionsAsync(sourceOfferingId, includeDrafts: true, ct))
            {
                var section = New(new ContentSection { ExternalId = string.Empty, Source = DoctrinaSource, Title = from.Title });
                section.OfferingId = targetOfferingId;
                section.Position = ++position;
                sections++;
                var itemPosition = 0;
                foreach (var source in from.Items)
                {
                    if (source.Kind == ContentItemKind.ToolLink && !(tools is not null && await tools.ExistsAsync(source.ToolId!.Value, ct)))
                    {
                        skipped++;
                        continue;
                    }

                    var item = New(new ContentItem { ExternalId = string.Empty, Source = DoctrinaSource, Title = source.Title });
                    item.SectionId = section.Id;
                    item.Kind = source.Kind;
                    (item.Body, item.Url, item.ToolId, item.FileName, item.ContentType, item.Size) =
                        (source.Body, source.Url, source.ToolId, source.FileName, source.ContentType, source.Size);
                    if (source.Kind == ContentItemKind.File)
                    {
                        if (!files.Copy(sourceOfferingId, source.Id, targetOfferingId, item.Id))
                        {
                            db.Remove(item); // not saved yet: only forgets it
                            skipped++;
                            continue;
                        }

                        copied.Add(item.Id);
                    }

                    item.Position = ++itemPosition;
                    items++;
                }
            }

            await db.SaveChangesAsync(ct);
        }
        catch
        {
            foreach (var id in copied)
            {
                files.Delete(targetOfferingId, id);
            }

            throw;
        }

        return new CopyResult(sections, items, skipped);
    }

    private static Error Forbidden() => Error.Forbidden(ContentErrors.AccessForbidden, "You may not manage the content of this course offering.");

    public static Error TooLarge(int maxSizeMb) =>
        ContentErrors.Invalid(ContentErrors.FileTooLarge, "file", $"The file is larger than {maxSizeMb} MB.");

    /// <summary>The file of a file item, or <c>Content.File.Missing</c> when it is not on disk.</summary>
    public Result<Stream> OpenFile(ContentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ContentItemKind.File && files.Open(item.Section.OfferingId, item.Id) is { } stream
            ? stream
            : Error.NotFound(ContentErrors.FileMissing, "The file is not available.");
    }

    public async Task<Result> RenameItemAsync(Guid itemId, string? title, CancellationToken ct)
    {
        var item = await ActiveItemAsync(itemId, ct);
        if (item is null)
        {
            return ItemNotFound(itemId);
        }

        if (CheckTitle(ContentErrors.ItemInvalid, title) is { } invalid)
        {
            return invalid;
        }

        item.Title = title!.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Saves the title and Markdown text of a page.</summary>
    public async Task<Result> SavePageAsync(Guid itemId, string? title, string? body, CancellationToken ct)
    {
        var item = await ActiveItemAsync(itemId, ct);
        if (item is null || item.Kind != ContentItemKind.Page)
        {
            return ItemNotFound(itemId);
        }

        if (CheckTitle(ContentErrors.ItemInvalid, title) is { } invalid)
        {
            return invalid;
        }

        item.Title = title!.Trim();
        item.Body = body ?? string.Empty;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetItemPublishedAsync(Guid itemId, bool published, CancellationToken ct)
    {
        var item = await ActiveItemAsync(itemId, ct);
        if (item is null)
        {
            return ItemNotFound(itemId);
        }

        item.Published = published;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteItemAsync(Guid itemId, CancellationToken ct)
    {
        var item = await ActiveItemAsync(itemId, ct);
        if (item is null)
        {
            return ItemNotFound(itemId);
        }

        item.Status = EntityStatus.ToBeDeleted;
        await db.SaveChangesAsync(ct);
        if (item.Kind == ContentItemKind.File)
        {
            files.Delete(item.Section.OfferingId, item.Id);
        }

        return Result.Success();
    }

    /// <summary>Moves the item one place up (-1) or down (+1) among the active items of its section.</summary>
    public async Task<Result> MoveItemAsync(Guid itemId, int direction, CancellationToken ct)
    {
        var item = await ActiveItemAsync(itemId, ct);
        if (item is null)
        {
            return ItemNotFound(itemId);
        }

        var siblings = await db.Items
            .Where(i => i.SectionId == item.SectionId && i.Status == EntityStatus.Active)
            .OrderBy(i => i.Position)
            .ToListAsync(ct);
        var index = siblings.FindIndex(i => i.Id == itemId) + Math.Sign(direction);
        if (index >= 0 && index < siblings.Count)
        {
            (item.Position, siblings[index].Position) = (siblings[index].Position, item.Position);
            await db.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    private async Task<Result<ContentItem>> AddItemAsync(Guid sectionId, ContentItemKind kind, string? title, Action<ContentItem> fill, CancellationToken ct)
    {
        var section = await ActiveSectionAsync(sectionId, ct);
        if (section is null)
        {
            return SectionNotFound(sectionId);
        }

        if (CheckTitle(ContentErrors.ItemInvalid, title) is { } invalid)
        {
            return invalid;
        }

        var item = NewItem(section, kind, title!.Trim());
        fill(item);
        await db.SaveChangesAsync(ct);
        return item;
    }

    private ContentItem NewItem(ContentSection section, ContentItemKind kind, string title)
    {
        // Deleted items keep their position, so "last" counts them too.
        var last = db.Items.Where(i => i.SectionId == section.Id).Max(i => (int?)i.Position) ?? 0;
        var item = New(new ContentItem { ExternalId = string.Empty, Source = DoctrinaSource, Title = title, Section = section });
        item.SectionId = section.Id;
        item.Kind = kind;
        item.Position = last + 1;
        return item;
    }

    private TEntity New<TEntity>(TEntity entity)
        where TEntity : ExchangeableEntity
    {
        // EF Core generates the sequential Id on Add; the Doctrina-owned ExternalId is that Id (design D2).
        db.Add(entity);
        entity.ExternalId = entity.Id.ToString("D");
        return entity;
    }

    private Task<ContentSection?> ActiveSectionAsync(Guid sectionId, CancellationToken ct) =>
        db.Sections.Include(s => s.Items.Where(i => i.Status == EntityStatus.Active))
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.Status == EntityStatus.Active, ct);

    private Task<ContentItem?> ActiveItemAsync(Guid itemId, CancellationToken ct) =>
        db.Items.Include(i => i.Section)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.Status == EntityStatus.Active && i.Section.Status == EntityStatus.Active, ct);

    private static Error? CheckTitle(string code, string? title) =>
        string.IsNullOrWhiteSpace(title) ? ContentErrors.Invalid(code, "title", "Enter a title.")
        : title.Trim().Length > ContentDbContext.TitleMaxLength ? ContentErrors.Invalid(code, "title", $"The title is longer than {ContentDbContext.TitleMaxLength} characters.")
        : null;

    private static Error? CheckUrl(string? url) =>
        url is not null
        && url.Trim().Length <= ContentDbContext.UrlMaxLength
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? null
            : ContentErrors.Invalid(ContentErrors.ItemInvalid, "url", LinkMessage);

    private static Error SectionNotFound(Guid id) => Error.NotFound(ContentErrors.SectionNotFound, $"Section {id:D} was not found.");

    private static Error ItemNotFound(Guid id) => Error.NotFound(ContentErrors.ItemNotFound, $"Item {id:D} was not found.");
}

/// <summary>Who may manage and read the content of an offering (design D4 of add-content).</summary>
internal sealed class ContentAccess(IAcademicDirectory directory)
{
    /// <summary><c>Content.Manage</c>, or lecturer or coordinator of the offering.</summary>
    public async Task<bool> CanManageAsync(ClaimsPrincipal user, Guid offeringId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Has(user, ContentPermissions.Manage)
            || (await directory.CurrentRolesAsync(user, offeringId, ct)).Any(r => r is OfferingRole.Lecturer or OfferingRole.Coordinator);
    }

    /// <summary>May manage, or <c>Content.Read</c>, or any active enrolment in the offering.</summary>
    public async Task<bool> CanReadAsync(ClaimsPrincipal user, Guid offeringId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Has(user, ContentPermissions.Manage) || Has(user, ContentPermissions.Read)
            || (await directory.CurrentRolesAsync(user, offeringId, ct)).Count > 0;
    }

    private static bool Has(ClaimsPrincipal user, string permission) => user.HasClaim(DoctrinaClaimTypes.Permission, permission);
}

/// <summary>The public contract of the Content module (design D1 of add-lti-platform).</summary>
internal sealed class ContentDirectory(ContentDbContext db, ContentAccess access, ContentService content) : IContentDirectory
{
    public async Task<Result<SectionInfo>> FindSectionForManagingAsync(ClaimsPrincipal user, Guid sectionId, CancellationToken cancellationToken)
    {
        var section = await db.Sections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sectionId && s.Status == EntityStatus.Active, cancellationToken);
        if (section is null)
        {
            return Error.NotFound(ContentErrors.SectionNotFound, $"Section {sectionId:D} was not found.");
        }

        return await access.CanManageAsync(user, section.OfferingId, cancellationToken)
            ? new SectionInfo(section.Id, section.OfferingId)
            : Error.Forbidden(IContentDirectory.AccessForbiddenCode, "You may not manage the content of this course offering.");
    }

    public async Task<Result> AddToolLinksAsync(ClaimsPrincipal user, Guid sectionId, Guid toolId, IReadOnlyList<NewToolLink> links, CancellationToken cancellationToken)
    {
        var section = await FindSectionForManagingAsync(user, sectionId, cancellationToken);
        return section.Error is { } denied ? denied : await content.AddToolLinksAsync(sectionId, toolId, links, cancellationToken);
    }

    public Task<bool> CanManageAsync(ClaimsPrincipal user, Guid offeringId, CancellationToken cancellationToken) =>
        access.CanManageAsync(user, offeringId, cancellationToken);

    public async Task<IReadOnlyList<ToolLinkInfo>> ToolLinksOfToolAsync(Guid offeringId, Guid toolId, CancellationToken cancellationToken) =>
        [.. (await db.Items.AsNoTracking()
            .Where(i => i.Kind == ContentItemKind.ToolLink && i.ToolId == toolId && i.Status == EntityStatus.Active
                && i.Section.OfferingId == offeringId && i.Section.Status == EntityStatus.Active)
            .OrderBy(i => i.Title).ThenBy(i => i.Id)
            .ToListAsync(cancellationToken))
            .Select(i => new ToolLinkInfo(i.Id, offeringId, i.Title, toolId, i.Url))];

    public async Task<Result<ToolLinkInfo>> FindToolLinkAsync(ClaimsPrincipal user, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await db.Items.AsNoTracking().Include(i => i.Section)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.Status == EntityStatus.Active && i.Section.Status == EntityStatus.Active, cancellationToken);
        if (item is not { Kind: ContentItemKind.ToolLink, ToolId: { } toolId })
        {
            return Error.NotFound(IContentDirectory.ItemNotFoundCode, $"Item {itemId:D} was not found.");
        }

        var offeringId = item.Section.OfferingId;
        if (!await access.CanReadAsync(user, offeringId, cancellationToken))
        {
            return Error.Forbidden(IContentDirectory.AccessForbiddenCode, "You may not read the content of this course offering.");
        }

        return item.IsVisible || await access.CanManageAsync(user, offeringId, cancellationToken)
            ? new ToolLinkInfo(item.Id, offeringId, item.Title, toolId, item.Url)
            : Error.NotFound(IContentDirectory.ItemNotFoundCode, $"Item {itemId:D} was not found.");
    }
}
