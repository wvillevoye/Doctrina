using System.Security.Claims;
using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Infrastructure.Authentication;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Domain;
using Doctrina.Modules.Content.Infrastructure;
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

    public static readonly string[] All = [SectionInvalid, SectionNotFound, ItemInvalid, ItemNotFound, FileTooLarge, FileMissing];

    public static Error Invalid(string code, string field, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>
/// All changes to sections and items (design D3 of add-content). Every method saves once and returns a
/// <see cref="Result"/>. Reading goes through <see cref="SectionsAsync"/> and <see cref="FindItemAsync"/>, so the
/// visibility rule lives in one place.
/// </summary>
internal sealed class ContentService(ContentDbContext db, IAcademicDirectory directory, ContentFileStore files)
{
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
