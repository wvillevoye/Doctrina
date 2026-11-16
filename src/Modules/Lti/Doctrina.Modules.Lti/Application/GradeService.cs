using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Contracts;
using Doctrina.Modules.Lti.Domain;
using Doctrina.Modules.Lti.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Lti.Application;

/// <summary>What a tool sends for a line item.</summary>
internal sealed record LineItemInput(
    string? Label, decimal? ScoreMaximum, string? ResourceId, string? Tag, string? ResourceLinkId, DateTimeOffset? StartDateTime, DateTimeOffset? EndDateTime);

/// <summary>What a tool sends for a score.</summary>
internal sealed record ScoreInput(
    string? UserId, decimal? ScoreGiven, decimal? ScoreMaximum, string? Comment, string? ActivityProgress, string? GradingProgress, DateTimeOffset? Timestamp);

/// <summary>A score of the signed-in student: tool link title (if still there), line item label, score and maximum.</summary>
internal sealed record MyScore(string? LinkTitle, string Label, decimal Given, decimal Maximum);

/// <summary>A student's score on a line item for the teacher's overview; <see langword="null"/> if none was given.</summary>
internal sealed record StudentScore(string Name, decimal? Given);

/// <summary>A line item of a tool link with the score of every student.</summary>
internal sealed record LineItemOverview(string Label, decimal Maximum, IReadOnlyList<StudentScore> Students);

/// <summary>A page of a list; <see cref="Next"/> is the id to continue after, or <see langword="null"/> on the last page.</summary>
internal sealed record Page<T>(IReadOnlyList<T> Items, Guid? Next);

/// <summary>
/// Line items, scores and results of Assignment and Grade Services (design D4–D6 of add-lti-ags). A tool only reaches
/// offerings where it has a tool link, and only the line items it created; anything else is "not found" so its
/// existence doesn't leak. Nothing personal (user ids, scores, comments) is logged.
/// </summary>
internal sealed class GradeService(LtiDbContext db, IContentDirectory content, IAcademicDirectory academic)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 500;

    /// <summary>The scores of a person in the offering that have a score given; only theirs, never anyone else's (design D7).</summary>
    public async Task<IReadOnlyList<MyScore>> MyScoresAsync(Guid offeringId, Guid person, CancellationToken ct)
    {
        var rows = await (from s in db.Scores.AsNoTracking()
                          join l in db.LineItems on s.LineItemId equals l.Id
                          where s.PersonId == person && l.OfferingId == offeringId && s.ScoreGiven != null
                          orderby l.Label, l.Id
                          select new { l.ToolId, l.ResourceLinkId, l.Label, Given = s.ScoreGiven!.Value, s.ScoreMaximum }).ToListAsync(ct);
        var titles = new Dictionary<Guid, string>();
        foreach (var toolId in rows.Select(r => r.ToolId).Distinct())
        {
            foreach (var link in await content.ToolLinksOfToolAsync(offeringId, toolId, ct))
            {
                titles[link.ItemId] = link.Title;
            }
        }

        return [.. rows.Select(r => new MyScore(r.ResourceLinkId is { } id && titles.TryGetValue(id, out var t) ? t : null, r.Label, r.Given, r.ScoreMaximum))];
    }

    /// <summary>The line items of a tool link with the score of every student of the offering (none yet = no score), for the teacher.</summary>
    public async Task<IReadOnlyList<LineItemOverview>> OverviewAsync(ToolLinkInfo link, CancellationToken ct)
    {
        var items = await db.LineItems.AsNoTracking()
            .Where(l => l.ToolId == link.ToolId && l.OfferingId == link.OfferingId && l.ResourceLinkId == link.ItemId)
            .OrderBy(l => l.Label).ThenBy(l => l.Id).ToListAsync(ct);
        var ids = items.Select(i => i.Id).ToList();
        var scores = await db.Scores.AsNoTracking().Where(s => ids.Contains(s.LineItemId)).ToListAsync(ct);
        var students = (await academic.MembersAsync(link.OfferingId, ct)).Where(m => m.Role == OfferingRole.Student).ToList();
        return [.. items.Select(i => new LineItemOverview(i.Label, i.ScoreMaximum,
            [.. students.Select(m => new StudentScore(m.Name, scores.FirstOrDefault(s => s.LineItemId == i.Id && s.PersonId == m.PersonId)?.ScoreGiven))]))];
    }

    /// <summary>The tool links of the tool in the offering, or <c>Lti.Ags.ContextNotFound</c> if it has none (design D4).</summary>
    public async Task<Result<IReadOnlyList<ToolLinkInfo>>> ContextAsync(LtiTool tool, Guid offeringId, CancellationToken ct)
    {
        var links = await content.ToolLinksOfToolAsync(offeringId, tool.Id, ct);
        return links.Count == 0
            ? Error.NotFound(LtiErrors.AgsContextNotFound, "The course offering was not found for this tool.")
            : Result<IReadOnlyList<ToolLinkInfo>>.Success(links);
    }

    public async Task<Result<LineItem>> CreateLineItemAsync(LtiTool tool, Guid offeringId, LineItemInput input, CancellationToken ct)
    {
        var links = await ContextAsync(tool, offeringId, ct);
        if (links.Error is { } missing)
        {
            return missing;
        }

        var item = new LineItem { ExternalId = string.Empty, Source = ToolService.DoctrinaSource, ToolId = tool.Id, OfferingId = offeringId, Label = string.Empty };
        if (Apply(item, input, links.Value!) is { } invalid)
        {
            return invalid;
        }

        db.Add(item);
        item.ExternalId = item.Id.ToString("D"); // EF Core generates the sequential Id on Add (huisregel 6)
        await db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<Result<LineItem>> FindLineItemAsync(LtiTool tool, Guid offeringId, Guid id, CancellationToken ct)
    {
        if ((await ContextAsync(tool, offeringId, ct)).Error is { } missing)
        {
            return missing;
        }

        return await db.LineItems.FirstOrDefaultAsync(l => l.Id == id && l.ToolId == tool.Id && l.OfferingId == offeringId, ct)
            ?? Result<LineItem>.Failure(LineItemNotFound(id));
    }

    public async Task<Result<Page<LineItem>>> ListLineItemsAsync(
        LtiTool tool, Guid offeringId, string? resourceLinkId, string? resourceId, string? tag, int? limit, Guid? after, CancellationToken ct)
    {
        if ((await ContextAsync(tool, offeringId, ct)).Error is { } missing)
        {
            return missing;
        }

        if (Limit(limit) is not { } take)
        {
            return Error.Validation(LtiErrors.AgsInvalidLineItem, $"Use a limit between 1 and {MaxLimit}.");
        }

        var query = db.LineItems.AsNoTracking().Where(l => l.ToolId == tool.Id && l.OfferingId == offeringId);
        if (resourceLinkId is not null)
        {
            query = Guid.TryParse(resourceLinkId, out var link) ? query.Where(l => l.ResourceLinkId == link) : query.Where(_ => false);
        }

        if (resourceId is not null)
        {
            query = query.Where(l => l.ResourceId == resourceId);
        }

        if (tag is not null)
        {
            query = query.Where(l => l.Tag == tag);
        }

        return await PageAsync(query, l => l.Id, after, take, ct);
    }

    public async Task<Result> UpdateLineItemAsync(LtiTool tool, Guid offeringId, Guid id, LineItemInput input, CancellationToken ct)
    {
        var found = await FindLineItemAsync(tool, offeringId, id, ct);
        if (found.Error is { } missing)
        {
            return missing;
        }

        var links = (await ContextAsync(tool, offeringId, ct)).Value!;
        if (Apply(found.Value!, input, links) is { } invalid)
        {
            return invalid;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Deletes the line item; its scores go with it.</summary>
    public async Task<Result> DeleteLineItemAsync(LtiTool tool, Guid offeringId, Guid id, CancellationToken ct)
    {
        var found = await FindLineItemAsync(tool, offeringId, id, ct);
        if (found.Error is { } missing)
        {
            return missing;
        }

        db.Remove(found.Value!);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Keeps the score of a student; an older <c>timestamp</c> than the kept score is refused (409), an equal or newer one replaces it.</summary>
    public async Task<Result> PostScoreAsync(LtiTool tool, Guid offeringId, Guid lineItemId, ScoreInput input, CancellationToken ct)
    {
        var found = await FindLineItemAsync(tool, offeringId, lineItemId, ct);
        if (found.Error is { } missing)
        {
            return missing;
        }

        var lineItem = found.Value!;
        if (CheckScore(input) is { } invalid)
        {
            return invalid;
        }

        var person = Guid.Parse(input.UserId!);
        if (!(await academic.MembersAsync(offeringId, ct)).Any(m => m.PersonId == person))
        {
            return Error.Validation(LtiErrors.AgsUserNotMember, "The user is not a member of the course offering.");
        }

        var timestamp = input.Timestamp!.Value.ToUniversalTime();
        var score = await db.Scores.FirstOrDefaultAsync(s => s.LineItemId == lineItem.Id && s.PersonId == person, ct);
        if (score is not null && timestamp < score.Timestamp)
        {
            return Error.Conflict(LtiErrors.AgsScoreOutdated, "A newer score already exists.");
        }

        if (score is null)
        {
            score = new Score { LineItemId = lineItem.Id, PersonId = person };
            db.Add(score);
        }

        score.ScoreGiven = input.ScoreGiven;
        score.ScoreMaximum = input.ScoreMaximum ?? lineItem.ScoreMaximum;
        score.Comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();
        score.ActivityProgress = Enum.Parse<ActivityProgress>(input.ActivityProgress!);
        score.GradingProgress = Enum.Parse<GradingProgress>(input.GradingProgress!);
        score.Timestamp = timestamp;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>The scores of a line item that have a score given, ordered by id; <paramref name="userId"/> limits them to one person.</summary>
    public async Task<Result<Page<Score>>> ResultsAsync(LtiTool tool, Guid offeringId, Guid lineItemId, string? userId, int? limit, Guid? after, CancellationToken ct)
    {
        var found = await FindLineItemAsync(tool, offeringId, lineItemId, ct);
        if (found.Error is { } missing)
        {
            return missing;
        }

        if (Limit(limit) is not { } take)
        {
            return Error.Validation(LtiErrors.AgsInvalidScore, $"Use a limit between 1 and {MaxLimit}.");
        }

        var query = db.Scores.AsNoTracking().Where(s => s.LineItemId == lineItemId && s.ScoreGiven != null);
        if (userId is not null)
        {
            query = Guid.TryParse(userId, out var person) ? query.Where(s => s.PersonId == person) : query.Where(_ => false);
        }

        return await PageAsync(query, s => s.Id, after, take, ct);
    }

    private static Error? Apply(LineItem item, LineItemInput input, IReadOnlyList<ToolLinkInfo> links)
    {
        if (string.IsNullOrWhiteSpace(input.Label) || input.Label.Trim().Length > LtiDbContext.NameMaxLength)
        {
            return Invalid(LtiErrors.AgsInvalidLineItem, "label", "Enter a label of at most 200 characters.");
        }

        if (input.ScoreMaximum is not > 0)
        {
            return Invalid(LtiErrors.AgsInvalidLineItem, "scoreMaximum", "The score maximum must be greater than 0.");
        }

        if (TooLong(input.ResourceId) || TooLong(input.Tag))
        {
            return Invalid(LtiErrors.AgsInvalidLineItem, "resourceId", "Use at most 100 characters.");
        }

        Guid? resourceLink = null;
        if (!string.IsNullOrWhiteSpace(input.ResourceLinkId))
        {
            // The resource link must be a tool link of this tool in this offering (design D4).
            if (!Guid.TryParse(input.ResourceLinkId, out var parsed) || links.All(l => l.ItemId != parsed))
            {
                return Invalid(LtiErrors.AgsInvalidLineItem, "resourceLinkId", "The resource link is not a link of this tool in this course offering.");
            }

            resourceLink = parsed;
        }

        item.Label = input.Label.Trim();
        item.ScoreMaximum = input.ScoreMaximum.Value;
        item.ResourceId = Blank(input.ResourceId);
        item.Tag = Blank(input.Tag);
        item.ResourceLinkId = resourceLink;
        item.StartDateTime = input.StartDateTime;
        item.EndDateTime = input.EndDateTime;
        return null;
    }

    private static Error? CheckScore(ScoreInput input)
    {
        if (!Guid.TryParse(input.UserId, out _))
        {
            return Invalid(LtiErrors.AgsInvalidScore, "userId", "Enter the userId from the launch.");
        }

        if ((input.ScoreGiven is null) != (input.ScoreMaximum is null))
        {
            return Invalid(LtiErrors.AgsInvalidScore, "scoreMaximum", "scoreGiven and scoreMaximum go together.");
        }

        if (input.ScoreMaximum is <= 0 || input.ScoreGiven is < 0)
        {
            return Invalid(LtiErrors.AgsInvalidScore, "scoreGiven", "Use a score of 0 or more and a maximum above 0.");
        }

        if (!Enum.TryParse<ActivityProgress>(input.ActivityProgress, out _) || !Enum.TryParse<GradingProgress>(input.GradingProgress, out _)
            || !Enum.IsDefined(Enum.Parse<ActivityProgress>(input.ActivityProgress!)) || !Enum.IsDefined(Enum.Parse<GradingProgress>(input.GradingProgress!)))
        {
            return Invalid(LtiErrors.AgsInvalidScore, "activityProgress", "Use a known activityProgress and gradingProgress.");
        }

        if (input.Timestamp is null)
        {
            return Invalid(LtiErrors.AgsInvalidScore, "timestamp", "Enter the timestamp.");
        }

        return input.Comment?.Length > LtiDbContext.CommentMaxLength
            ? Invalid(LtiErrors.AgsInvalidScore, "comment", "Use at most 1000 characters.")
            : null;
    }

    private static async Task<Page<T>> PageAsync<T>(IQueryable<T> query, System.Linq.Expressions.Expression<Func<T, Guid>> key, Guid? after, int take, CancellationToken ct)
    {
        var ordered = query.OrderBy(key);
        var rows = await (after is { } last
            ? ordered.Where(Greater(key, last))
            : ordered).Take(take + 1).ToListAsync(ct);
        var keyOf = key.Compile();
        return rows.Count > take
            ? new Page<T>(rows.Take(take).ToList(), keyOf(rows[take - 1]))
            : new Page<T>(rows, null);
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> Greater<T>(System.Linq.Expressions.Expression<Func<T, Guid>> key, Guid value)
    {
        // Guid has no ">" operator in C#; SQL Server compares uniqueidentifier, which EF Core translates from CompareTo.
        var compare = typeof(Guid).GetMethod(nameof(Guid.CompareTo), [typeof(Guid)])!;
        var call = System.Linq.Expressions.Expression.Call(key.Body, compare, System.Linq.Expressions.Expression.Constant(value));
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
            System.Linq.Expressions.Expression.GreaterThan(call, System.Linq.Expressions.Expression.Constant(0)), key.Parameters);
    }

    internal static int? Limit(int? limit) => limit is null ? DefaultLimit : limit is >= 1 and <= MaxLimit ? limit : null;

    private static bool TooLong(string? value) => value?.Trim().Length > LtiDbContext.IdMaxLength;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Error LineItemNotFound(Guid id) => Error.NotFound(LtiErrors.AgsLineItemNotFound, $"Line item {id:D} was not found.");

    private static Error Invalid(string code, string field, string message) => LtiErrors.Invalid(code, field, message);
}
