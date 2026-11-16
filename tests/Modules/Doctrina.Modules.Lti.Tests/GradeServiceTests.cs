using System.Security.Claims;
using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Contracts;
using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Domain;
using Doctrina.TestKit.Fakes;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Lti.Tests;

// Specs lti-grade-services of change add-lti-ags; tasks 4.2-4.4 (the rules of the service; the HTTP layer is tested against
// the host). Class = requirement, method = scenario.

/// <summary>A content directory that says which tool links a tool has in an offering.</summary>
internal sealed class GradeContent : IContentDirectory
{
    public Dictionary<(Guid Offering, Guid Tool), List<ToolLinkInfo>> Links { get; } = [];

    public ToolLinkInfo AddLink(Guid offering, Guid tool, string title)
    {
        var link = new ToolLinkInfo(Guid.NewGuid(), offering, title, tool);
        Links.TryAdd((offering, tool), []);
        Links[(offering, tool)].Add(link);
        return link;
    }

    public bool CanManage { get; set; } = true;

    public Task<bool> CanManageAsync(ClaimsPrincipal user, Guid offeringId, CancellationToken cancellationToken) => Task.FromResult(CanManage);

    public Task<IReadOnlyList<ToolLinkInfo>> ToolLinksOfToolAsync(Guid offeringId, Guid toolId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ToolLinkInfo>>(Links.TryGetValue((offeringId, toolId), out var l) ? [.. l] : []);

    public Task<Result<ToolLinkInfo>> FindToolLinkAsync(ClaimsPrincipal user, Guid itemId, CancellationToken cancellationToken) =>
        Task.FromResult(Links.Values.SelectMany(l => l).FirstOrDefault(l => l.ItemId == itemId) is { } found
            ? Result<ToolLinkInfo>.Success(found)
            : Result<ToolLinkInfo>.Failure(Error.NotFound(IContentDirectory.ItemNotFoundCode, "Not found.")));

    public Task<Result<SectionInfo>> FindSectionForManagingAsync(ClaimsPrincipal user, Guid sectionId, CancellationToken cancellationToken) =>
        Task.FromResult(Result<SectionInfo>.Failure(Error.NotFound("Content.Section.NotFound", "Not found.")));

    public Task<Result> AddToolLinksAsync(ClaimsPrincipal user, Guid sectionId, Guid toolId, IReadOnlyList<NewToolLink> links, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(Error.NotFound("Content.Section.NotFound", "Not found.")));
}

/// <summary>Two tools with grade services, tool links in one offering, two students in it, and the service under test.</summary>
internal sealed class GradeScenario : IAsyncDisposable
{
    public static readonly DateTimeOffset T0 = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private GradeScenario(LtiDatabase db) => Db = db;

    public LtiDatabase Db { get; }
    public LtiTool Tool { get; private set; } = null!;
    public LtiTool OtherTool { get; private set; } = null!;
    public FakeDirectory Directory { get; } = new();
    public GradeContent Content { get; } = new();
    public ToolLinkInfo Link { get; private set; } = null!;
    public ToolLinkInfo OtherLink { get; private set; } = null!;
    public OfferingMember Anna { get; private set; } = null!;
    public OfferingMember Bram { get; private set; } = null!;
    public Guid Offering => Directory.OfferingId;

    public static async Task<GradeScenario> CreateAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var s = new GradeScenario(await LtiDatabase.CreateAsync(ct));
        var input = ToolTestData.Quiz() with { GradeServices = true, PublicKeysUrl = "https://tool.example.org/jwks" };
        s.Tool = (await s.Db.Service().CreateAsync(input, ct)).Value!;
        s.OtherTool = (await s.Db.Service().CreateAsync(input with { Name = "Other tool" }, ct)).Value!;
        s.Link = s.Content.AddLink(s.Offering, s.Tool.Id, "Quiz 1");
        s.OtherLink = s.Content.AddLink(s.Offering, s.OtherTool.Id, "Other quiz");
        s.Anna = s.Directory.Add("I6000001");
        s.Bram = s.Directory.Add("I6000002");
        return s;
    }

    /// <summary>A service with its own context, like a new request.</summary>
    public GradeService Service() => new(Db.CreateContext(), Content, Directory);

    public static LineItemInput Item(string label = "Quiz 1", decimal max = 10, string? resourceLinkId = null, string? tag = null) =>
        new(label, max, null, tag, resourceLinkId, null, null);

    public async Task<LineItem> CreateItemAsync(string label = "Quiz 1", LtiTool? tool = null, string? resourceLinkId = null) =>
        (await Service().CreateLineItemAsync(tool ?? Tool, Offering, Item(label, resourceLinkId: resourceLinkId), TestContext.Current.CancellationToken)).Value!;

    public static ScoreInput Score(OfferingMember who, decimal? given = 8, decimal? max = 10, DateTimeOffset? at = null, string progress = "Completed", string grading = "FullyGraded", string? comment = null) =>
        new(who.PersonId.ToString("N"), given, max, comment, progress, grading, at ?? T0);

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}

public sealed class LineItemsVanEenToolBeheren
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LineItemAanmaken()
    {
        await using var s = await GradeScenario.CreateAsync();

        var result = await s.Service().CreateLineItemAsync(s.Tool, s.Offering, GradeScenario.Item("Quiz 1", 10, s.Link.ItemId.ToString("D"), "week-1"), Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var item = Assert.Single(await s.Db.CreateContext().LineItems.ToListAsync(Ct));
        Assert.Equal(("Quiz 1", 10m, "week-1", s.Link.ItemId, s.Tool.Id, s.Offering), (item.Label, item.ScoreMaximum, item.Tag, item.ResourceLinkId, item.ToolId, item.OfferingId));
        Assert.Equal(item.Id.ToString("D"), item.ExternalId);
        Assert.Equal("Doctrina", item.Source);
    }

    [Fact]
    public async Task LineItemMetEenResourceLinkVanEenAndereTool()
    {
        await using var s = await GradeScenario.CreateAsync();

        var result = await s.Service().CreateLineItemAsync(s.Tool, s.Offering, GradeScenario.Item(resourceLinkId: s.OtherLink.ItemId.ToString("D")), Ct);

        Assert.Equal("Lti.Ags.InvalidLineItem", result.Error!.Code);
        Assert.Empty(await s.Db.CreateContext().LineItems.ToListAsync(Ct));
    }

    [Fact]
    public async Task OngeldigLineItem()
    {
        await using var s = await GradeScenario.CreateAsync();

        foreach (var input in new[] { GradeScenario.Item(label: " "), GradeScenario.Item(max: 0), GradeScenario.Item(max: -5) })
        {
            var result = await s.Service().CreateLineItemAsync(s.Tool, s.Offering, input, Ct);
            Assert.Equal("Lti.Ags.InvalidLineItem", result.Error!.Code);
        }

        Assert.Empty(await s.Db.CreateContext().LineItems.ToListAsync(Ct));
    }

    [Fact]
    public async Task LijstFilterenOpResourceLink()
    {
        await using var s = await GradeScenario.CreateAsync();
        await s.CreateItemAsync("Linked", resourceLinkId: s.Link.ItemId.ToString("D"));
        await s.CreateItemAsync("Loose");

        var page = await s.Service().ListLineItemsAsync(s.Tool, s.Offering, s.Link.ItemId.ToString("D"), null, null, null, null, Ct);

        Assert.Equal(["Linked"], page.Value!.Items.Select(i => i.Label));
        Assert.Null(page.Value.Next);
    }

    [Fact]
    public async Task LijstIsGepagineerd()
    {
        await using var s = await GradeScenario.CreateAsync();
        foreach (var n in Enumerable.Range(1, 5))
        {
            await s.CreateItemAsync($"Item {n}");
        }

        var first = (await s.Service().ListLineItemsAsync(s.Tool, s.Offering, null, null, null, 2, null, Ct)).Value!;
        var second = (await s.Service().ListLineItemsAsync(s.Tool, s.Offering, null, null, null, 2, first.Next, Ct)).Value!;
        var third = (await s.Service().ListLineItemsAsync(s.Tool, s.Offering, null, null, null, 2, second.Next, Ct)).Value!;

        Assert.Equal([2, 2, 1], [first.Items.Count, second.Items.Count, third.Items.Count]);
        Assert.Null(third.Next);
        Assert.Equal(5, first.Items.Concat(second.Items).Concat(third.Items).Select(i => i.Id).Distinct().Count());
    }

    [Fact]
    public async Task UitvoeringZonderToolLinkVanDezeTool()
    {
        await using var s = await GradeScenario.CreateAsync();
        var elsewhere = s.Directory.SourceOfferingId; // the tool has no link there

        var created = await s.Service().CreateLineItemAsync(s.Tool, elsewhere, GradeScenario.Item(), Ct);
        var listed = await s.Service().ListLineItemsAsync(s.Tool, elsewhere, null, null, null, null, null, Ct);

        Assert.Equal("Lti.Ags.ContextNotFound", created.Error!.Code);
        Assert.Equal("Lti.Ags.ContextNotFound", listed.Error!.Code);
    }

    [Fact]
    public async Task LineItemVanEenAndereTool()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync();

        var found = await s.Service().FindLineItemAsync(s.OtherTool, s.Offering, item.Id, Ct);
        var updated = await s.Service().UpdateLineItemAsync(s.OtherTool, s.Offering, item.Id, GradeScenario.Item("Hijacked"), Ct);
        var deleted = await s.Service().DeleteLineItemAsync(s.OtherTool, s.Offering, item.Id, Ct);
        var listed = await s.Service().ListLineItemsAsync(s.OtherTool, s.Offering, null, null, null, null, null, Ct);

        Assert.All([found.Error, updated.Error, deleted.Error], e => Assert.Equal("Lti.Ags.LineItemNotFound", e!.Code));
        Assert.Empty(listed.Value!.Items);
        Assert.Equal("Quiz 1", (await s.Db.CreateContext().LineItems.SingleAsync(Ct)).Label);
    }

    [Fact]
    public async Task LineItemWijzigen()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync();

        var updated = await s.Service().UpdateLineItemAsync(s.Tool, s.Offering, item.Id, GradeScenario.Item("Renamed", 20), Ct);

        Assert.True(updated.IsSuccess, updated.Error?.ToString());
        var saved = await s.Db.CreateContext().LineItems.SingleAsync(Ct);
        Assert.Equal(("Renamed", 20m), (saved.Label, saved.ScoreMaximum));
    }
}

public sealed class ScoresVanStudentenPosten
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ScorePosten()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync();

        var result = await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Anna, 8, 10, comment: "Well done"), Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var score = await s.Db.CreateContext().Scores.SingleAsync(Ct);
        Assert.Equal((s.Anna.PersonId, 8m, 10m, "Well done", ActivityProgress.Completed, GradingProgress.FullyGraded, GradeScenario.T0),
            (score.PersonId, score.ScoreGiven, score.ScoreMaximum, score.Comment, score.ActivityProgress, score.GradingProgress, score.Timestamp));
    }

    [Fact]
    public async Task OudereScoreWordtGeweigerd()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync();
        await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Anna, 5, 10, GradeScenario.T0), Ct);

        var result = await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Anna, 9, 10, GradeScenario.T0.AddHours(-1)), Ct);

        Assert.Equal("Lti.Ags.ScoreOutdated", result.Error!.Code);
        Assert.Equal(5m, (await s.Db.CreateContext().Scores.SingleAsync(Ct)).ScoreGiven);
    }

    [Fact]
    public async Task NieuwereScoreVervangtDeOude()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync();
        await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Anna, 5, 10, GradeScenario.T0), Ct);

        var result = await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Anna, 9, 10, GradeScenario.T0.AddHours(1)), Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(9m, (await s.Db.CreateContext().Scores.SingleAsync(Ct)).ScoreGiven); // one row per student and line item
    }

    [Fact]
    public async Task GebruikerIsGeenLidVanDeUitvoering()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync();
        var stranger = new OfferingMember(Guid.NewGuid(), "X", "X", OfferingRole.Student);

        var result = await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(stranger), Ct);

        Assert.Equal("Lti.Ags.UserNotMember", result.Error!.Code);
        Assert.Empty(await s.Db.CreateContext().Scores.ToListAsync(Ct));
    }

    [Fact]
    public async Task OngeldigeScore()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync();
        var invalid = new[]
        {
            GradeScenario.Score(s.Anna, given: 5, max: null),
            GradeScenario.Score(s.Anna, given: null, max: 10),
            GradeScenario.Score(s.Anna, given: -1, max: 10),
            GradeScenario.Score(s.Anna, given: 5, max: 0),
            GradeScenario.Score(s.Anna, progress: "Unknown"),
            GradeScenario.Score(s.Anna, grading: "7"),
            GradeScenario.Score(s.Anna, comment: new string('x', 1001)),
            GradeScenario.Score(s.Anna) with { Timestamp = null },
            GradeScenario.Score(s.Anna) with { UserId = "not-a-guid" },
        };

        foreach (var input in invalid)
        {
            var result = await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, input, Ct);
            Assert.Equal("Lti.Ags.InvalidScore", result.Error!.Code);
        }

        Assert.Empty(await s.Db.CreateContext().Scores.ToListAsync(Ct));
    }

    [Fact]
    public async Task ScoreZonderCijferBewaartAlleenDeVoortgang()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync();

        var result = await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Anna, null, null, progress: "Started", grading: "NotReady"), Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var score = await s.Db.CreateContext().Scores.SingleAsync(Ct);
        Assert.Null(score.ScoreGiven);
        Assert.Equal(10m, score.ScoreMaximum); // the maximum of the line item
    }
}

public sealed class ResultsLezen
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResultsVanEenLineItem()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync();
        await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Anna, 8, 10), Ct);
        await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Bram, 6, 10), Ct);
        await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Directory.Add("I6000003"), null, null, progress: "Started", grading: "NotReady"), Ct);

        var page = await s.Service().ResultsAsync(s.Tool, s.Offering, item.Id, null, null, null, Ct);

        Assert.Equal([8m, 6m], page.Value!.Items.Select(r => r.ScoreGiven!.Value).OrderByDescending(x => x)); // only scores with a score given
        Assert.Equal(2, page.Value.Items.Count);
    }

    [Fact]
    public async Task ResultsVoorEenGebruiker()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync();
        await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Anna, 8, 10), Ct);
        await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Bram, 6, 10), Ct);

        var page = await s.Service().ResultsAsync(s.Tool, s.Offering, item.Id, s.Bram.PersonId.ToString("N"), null, null, Ct);

        var result = Assert.Single(page.Value!.Items);
        Assert.Equal((s.Bram.PersonId, 6m), (result.PersonId, result.ScoreGiven));
    }

    [Fact]
    public async Task ResultsVanEenAndereTool()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync();

        var page = await s.Service().ResultsAsync(s.OtherTool, s.Offering, item.Id, null, null, null, Ct);

        Assert.Equal("Lti.Ags.LineItemNotFound", page.Error!.Code);
    }
}

public sealed class ScoresVerdwijnenMetHunLineItem
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LineItemVerwijderen()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync();
        var other = await s.CreateItemAsync("Quiz 2");
        await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Anna), Ct);
        await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Bram), Ct);
        await s.Service().PostScoreAsync(s.Tool, s.Offering, other.Id, GradeScenario.Score(s.Anna), Ct);

        var result = await s.Service().DeleteLineItemAsync(s.Tool, s.Offering, item.Id, Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var db = s.Db.CreateContext();
        Assert.Equal(other.Id, (await db.LineItems.SingleAsync(Ct)).Id);
        Assert.Equal(other.Id, (await db.Scores.SingleAsync(Ct)).LineItemId); // the scores of the other line item stay
    }
}

public sealed class EenStudentZietDeEigenScores
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StudentZietDeEigenScore()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync("Quiz 1", resourceLinkId: s.Link.ItemId.ToString("D"));
        await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Anna, 8, 10), Ct);

        var mine = await s.Service().MyScoresAsync(s.Offering, s.Anna.PersonId, Ct);

        var score = Assert.Single(mine);
        Assert.Equal(("Quiz 1", "Quiz 1", 8m, 10m), (score.LinkTitle, score.Label, score.Given, score.Maximum));
    }

    [Fact]
    public async Task StudentZietGeenScoresVanAnderen()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync();
        await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Anna, 8, 10), Ct);
        await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Bram, 3, 10), Ct);

        var mine = await s.Service().MyScoresAsync(s.Offering, s.Bram.PersonId, Ct);

        Assert.Equal([3m], mine.Select(m => m.Given));
        Assert.Empty(await s.Service().MyScoresAsync(s.Offering, Guid.NewGuid(), Ct));
    }
}

public sealed class EenDocentZietDeScoresPerToolLink
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DocentZietHetOverzicht()
    {
        await using var s = await GradeScenario.CreateAsync();
        var item = await s.CreateItemAsync("Quiz 1", resourceLinkId: s.Link.ItemId.ToString("D"));
        await s.CreateItemAsync("Elsewhere"); // not bound to this tool link
        await s.Service().PostScoreAsync(s.Tool, s.Offering, item.Id, GradeScenario.Score(s.Anna, 8, 10), Ct);

        var overview = await s.Service().OverviewAsync(s.Link, Ct);

        var quiz = Assert.Single(overview);
        Assert.Equal("Quiz 1", quiz.Label);
        Assert.Equal([("I6000001 Name", 8m), ("I6000002 Name", null)], quiz.Students.Select(x => (x.Name, x.Given)));
    }
}
