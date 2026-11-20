using System.Globalization;
using System.Security.Claims;
using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Modules.Content.Domain;
using Doctrina.Modules.Content.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Modules.Content.Application;

/// <summary>The introduction of an offering as the page shows it; the layout already falls back to "Default".</summary>
internal sealed record IntroductionView(Guid? TemplateId, string Body, bool Published, IntroductionLayout Layout);

/// <summary>
/// Introductions of course offerings and their templates (design D2-D6 of add-course-introduction). Every method
/// saves once and returns a <see cref="Result"/>. Authorization is the caller's job (see <see cref="ContentAccess"/>).
/// </summary>
internal sealed class IntroductionService(ContentDbContext db)
{
    public const string DefaultTemplateName = "Default";

    /// <summary>The active templates, "Default" first; creates "Default" when it does not exist yet.</summary>
    public async Task<IReadOnlyList<IntroductionTemplate>> TemplatesAsync(CancellationToken ct)
    {
        await EnsureDefaultAsync(ct);
        return await db.IntroductionTemplates.AsNoTracking()
            .Where(t => t.Status == EntityStatus.Active)
            .OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name)
            .ToListAsync(ct);
    }

    /// <summary>The introduction of the offering, or <see langword="null"/> when it has none.</summary>
    public async Task<IntroductionView?> FindAsync(Guid offeringId, CancellationToken ct)
    {
        var intro = await db.Introductions.AsNoTracking().FirstOrDefaultAsync(i => i.OfferingId == offeringId && i.Status == EntityStatus.Active, ct);
        if (intro is null)
        {
            return null;
        }

        var templates = await TemplatesAsync(ct);
        var template = templates.FirstOrDefault(t => t.Id == intro.TemplateId) ?? templates.First(t => t.IsDefault);
        return new IntroductionView(intro.TemplateId, intro.Body, intro.Published, template.Layout);
    }

    /// <summary>Creates or updates the introduction of the offering.</summary>
    public async Task<Result> SaveAsync(Guid offeringId, Guid? templateId, string? body, bool published, CancellationToken ct)
    {
        body ??= string.Empty;
        if (body.Length > ContentDbContext.IntroductionTextMaxLength)
        {
            return ContentErrors.Invalid(ContentErrors.IntroductionInvalid, "text", $"The text is longer than {ContentDbContext.IntroductionTextMaxLength.ToString("N0", CultureInfo.InvariantCulture)} characters.");
        }

        if (templateId is not null && !await db.IntroductionTemplates.AnyAsync(t => t.Id == templateId && t.Status == EntityStatus.Active, ct))
        {
            return Error.NotFound(ContentErrors.TemplateNotFound, $"Template {templateId:D} was not found.");
        }

        var intro = await db.Introductions.FirstOrDefaultAsync(i => i.OfferingId == offeringId, ct);
        if (intro is null)
        {
            intro = New(new CourseIntroduction { ExternalId = string.Empty, Source = ContentService.DoctrinaSource, OfferingId = offeringId });
        }

        (intro.TemplateId, intro.Body, intro.Published, intro.Status) = (templateId, body, published, EntityStatus.Active);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Creates a template, or updates it when <paramref name="id"/> is given.</summary>
    public async Task<Result> SaveTemplateAsync(Guid? id, string? name, IntroductionLayout layout, string? startBody, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ContentErrors.Invalid(ContentErrors.TemplateInvalid, "name", "Enter a name.");
        }

        if (name.Trim().Length > ContentDbContext.TemplateNameMaxLength)
        {
            return ContentErrors.Invalid(ContentErrors.TemplateInvalid, "name", $"The name is longer than {ContentDbContext.TemplateNameMaxLength} characters.");
        }

        if ((startBody ?? string.Empty).Length > ContentDbContext.IntroductionTextMaxLength)
        {
            return ContentErrors.Invalid(ContentErrors.TemplateInvalid, "startBody", $"The text is longer than {ContentDbContext.IntroductionTextMaxLength.ToString("N0", CultureInfo.InvariantCulture)} characters.");
        }

        IntroductionTemplate? template;
        if (id is null)
        {
            template = New(new IntroductionTemplate { ExternalId = string.Empty, Source = ContentService.DoctrinaSource, Name = name.Trim() });
        }
        else
        {
            template = await db.IntroductionTemplates.FirstOrDefaultAsync(t => t.Id == id && t.Status == EntityStatus.Active, ct);
            if (template is null)
            {
                return TemplateNotFound(id.Value);
            }
        }

        (template.Name, template.Layout, template.StartBody) = (name.Trim(), layout, startBody ?? string.Empty);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Deletes a template (soft). "Default" cannot be deleted; offerings that used it fall back to "Default".</summary>
    public async Task<Result> DeleteTemplateAsync(Guid id, CancellationToken ct)
    {
        var template = await db.IntroductionTemplates.FirstOrDefaultAsync(t => t.Id == id && t.Status == EntityStatus.Active, ct);
        if (template is null)
        {
            return TemplateNotFound(id);
        }

        if (template.IsDefault)
        {
            return Error.Conflict(ContentErrors.TemplateDefaultProtected, "The default template cannot be deleted.");
        }

        template.Status = EntityStatus.ToBeDeleted;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // "Default" is created on first use: its Id comes from EF Core like every other key, which a migration cannot do.
    private async Task EnsureDefaultAsync(CancellationToken ct)
    {
        if (await db.IntroductionTemplates.AnyAsync(t => t.IsDefault, ct))
        {
            return;
        }

        var template = New(new IntroductionTemplate { ExternalId = string.Empty, Source = ContentService.DoctrinaSource, Name = DefaultTemplateName });
        template.IsDefault = true;
        await db.SaveChangesAsync(ct);
    }

    private TEntity New<TEntity>(TEntity entity)
        where TEntity : ExchangeableEntity
    {
        // EF Core generates the sequential Id on Add; the Doctrina-owned ExternalId is that Id (design D2 of add-content).
        db.Add(entity);
        entity.ExternalId = entity.Id.ToString("D");
        return entity;
    }

    private static Error TemplateNotFound(Guid id) => Error.NotFound(ContentErrors.TemplateNotFound, $"Template {id:D} was not found.");
}

/// <summary>Who sees the introduction section of an offering (spec course-introduction).</summary>
internal static class IntroductionVisibility
{
    /// <summary>
    /// Those who may manage always see the section (a draft, or the link to add an introduction); everyone else only
    /// a published introduction.
    /// </summary>
    public static async Task<bool> IsVisibleAsync(IServiceProvider services, ClaimsPrincipal user, Guid offeringId)
    {
        var ct = CancellationToken.None;
        var access = services.GetRequiredService<ContentAccess>();
        if (await access.CanManageAsync(user, offeringId, ct))
        {
            return true;
        }

        return await access.CanReadAsync(user, offeringId, ct)
            && (await services.GetRequiredService<IntroductionService>().FindAsync(offeringId, ct))?.Published == true;
    }
}
