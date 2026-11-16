using System.Security.Claims;
using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Application;
using Doctrina.Modules.Content.Domain;
using Doctrina.Modules.Lti.Contracts;

namespace Doctrina.Modules.Content.Tests;

// Spec course-content of change add-course-templates; tasks 2.1 to 2.3. Class = requirement, method = scenario.

/// <summary>A lecturer of two offerings of the same course; the target is empty, the source is filled by the test.</summary>
internal sealed class CopyScenario : IAsyncDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    public static readonly LtiToolSummary Quiz = new(Guid.NewGuid(), "Quiz tool");

    private CopyScenario(ContentDatabase db, FakeDirectory directory, ContentService service, ClaimsPrincipal lecturer)
    {
        Db = db;
        Directory = directory;
        Service = service;
        Lecturer = lecturer;
    }

    public ContentDatabase Db { get; }
    public FakeDirectory Directory { get; }
    public ContentService Service { get; }
    public ClaimsPrincipal Lecturer { get; }
    public Guid Target => Directory.OfferingId;
    public Guid Source => Directory.SourceOfferingId;

    public static async Task<CopyScenario> CreateAsync()
    {
        var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var lecturer = directory.Add("L1", OfferingRole.Lecturer);
        directory.SourceMembers.Add(lecturer);
        return new CopyScenario(db, directory, db.CreateService(directory, tools: new FakeToolCatalog(Quiz)), FakeDirectory.User(lecturer.PersonId));
    }

    public Task<Result<CopyResult>> CopyAsync(Guid? source = null, ClaimsPrincipal? user = null, ContentService? service = null) =>
        (service ?? Service).CopyFromAsync(user ?? Lecturer, Target, source ?? Source, Ct);

    public async Task<ContentSection> SourceSectionAsync(string title, bool published = false)
    {
        var section = (await Service.CreateSectionAsync(Source, title, Ct)).Value!;
        if (published)
        {
            await Service.SetSectionPublishedAsync(section.Id, true, Ct);
        }

        return section;
    }

    public async Task<ContentItem> SourceItemAsync(Task<Result<ContentItem>> add, bool published = false)
    {
        var item = (await add).Value!;
        if (published)
        {
            await Service.SetItemPublishedAsync(item.Id, true, Ct);
        }

        return item;
    }

    public Task<ContentItem> SourceFileAsync(Guid sectionId, string name, byte[] bytes) =>
        SourceItemAsync(Service.AddFileAsync(sectionId, null, name, "application/pdf", new MemoryStream(bytes), Ct));

    public async Task<IReadOnlyList<ContentSection>> TargetAsync(bool drafts = true) => await Service.SectionsAsync(Target, drafts, Ct);

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();
}

public sealed partial class ContentKopierenNaarEenLegeUitvoering
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DocentKopieertDeContentVanEenEerdereUitvoering()
    {
        await using var s = await CopyScenario.CreateAsync();
        var week = await s.SourceSectionAsync("Week 1");
        await s.SourceItemAsync(s.Service.AddPageAsync(week.Id, "Introduction", Ct));
        await s.SourceItemAsync(s.Service.AddLinkAsync(week.Id, "Syllabus", "https://example.test/syllabus", Ct));
        await s.SourceFileAsync(week.Id, "notes.pdf", [1, 2, 3]);

        var result = await s.CopyAsync();

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(new CopyResult(1, 3, 0), result.Value);
        var copied = Assert.Single(await s.TargetAsync());
        Assert.Equal("Week 1", copied.Title);
        Assert.Equal(["Introduction", "Syllabus", "notes.pdf"], copied.Items.Select(i => i.Title));
        Assert.Equal([ContentItemKind.Page, ContentItemKind.Link, ContentItemKind.File], copied.Items.Select(i => i.Kind));
        Assert.Equal("https://example.test/syllabus", copied.Items[1].Url);
    }

    [Fact]
    public async Task GekopieerdeContentIsEenConceptEnOnzichtbaarVoorStudenten()
    {
        await using var s = await CopyScenario.CreateAsync();
        var week = await s.SourceSectionAsync("Week 1", published: true);
        await s.SourceItemAsync(s.Service.AddLinkAsync(week.Id, "Syllabus", "https://example.test/s", Ct), published: true);

        await s.CopyAsync();

        var copied = Assert.Single(await s.TargetAsync());
        Assert.False(copied.Published);
        Assert.All(copied.Items, i => Assert.False(i.Published));
        Assert.Empty(await s.TargetAsync(drafts: false));
    }

    [Fact]
    public async Task BestandenWordenGekopieerdEnZijnLosVanDeBron()
    {
        await using var s = await CopyScenario.CreateAsync();
        var week = await s.SourceSectionAsync("Week 1");
        var original = await s.SourceFileAsync(week.Id, "syllabus.pdf", [9, 8, 7]);

        await s.CopyAsync();
        await s.Service.DeleteItemAsync(original.Id, Ct);

        var copy = Assert.Single(Assert.Single(await s.TargetAsync()).Items);
        var item = await s.Service.FindItemAsync(s.Target, copy.Id, true, Ct);
        var opened = s.Service.OpenFile(item!);
        Assert.True(opened.IsSuccess, opened.Error?.ToString());
        await using var stream = opened.Value!;
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes, Ct);
        Assert.Equal([9, 8, 7], bytes.ToArray());
        Assert.Equal(("syllabus.pdf", "application/pdf", 3L), (copy.FileName, copy.ContentType, copy.Size));
    }

    [Fact]
    public async Task DeBronBlijftOngewijzigd()
    {
        await using var s = await CopyScenario.CreateAsync();
        var published = await s.SourceSectionAsync("Week 1", published: true);
        await s.SourceItemAsync(s.Service.AddLinkAsync(published.Id, "A", "https://example.test/a", Ct), published: true);
        await s.SourceItemAsync(s.Service.AddLinkAsync(published.Id, "B", "https://example.test/b", Ct));
        await s.SourceSectionAsync("Week 2");
        static string Snapshot(IEnumerable<ContentSection> sections) => string.Join('|', sections.Select(x =>
            $"{x.Id}:{x.Title}:{x.Position}:{x.Published}[{string.Join(',', x.Items.Select(i => $"{i.Id}:{i.Title}:{i.Position}:{i.Published}"))}]"));
        var before = Snapshot(await s.Service.SectionsAsync(s.Source, true, Ct));

        await s.CopyAsync();

        Assert.Equal(before, Snapshot(await s.Service.SectionsAsync(s.Source, true, Ct)));
    }

    [Fact]
    public async Task VerwijderdeItemsGaanNietMee()
    {
        await using var s = await CopyScenario.CreateAsync();
        var keep = await s.SourceSectionAsync("Keep");
        var gone = await s.SourceSectionAsync("Gone");
        await s.SourceItemAsync(s.Service.AddLinkAsync(keep.Id, "Stays", "https://example.test/1", Ct));
        var removed = await s.SourceItemAsync(s.Service.AddLinkAsync(keep.Id, "Removed", "https://example.test/2", Ct));
        await s.Service.DeleteItemAsync(removed.Id, Ct);
        await s.Service.DeleteSectionAsync(gone.Id, Ct);

        var result = await s.CopyAsync();

        Assert.Equal(new CopyResult(1, 1, 0), result.Value);
        var copied = Assert.Single(await s.TargetAsync());
        Assert.Equal(["Stays"], copied.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task ToolLinkBehoudtDeTool()
    {
        await using var s = await CopyScenario.CreateAsync();
        var week = await s.SourceSectionAsync("Week 1");
        await s.SourceItemAsync(s.Service.AddToolLinkAsync(week.Id, "Weekly quiz", CopyScenario.Quiz.Id, Ct));

        await s.CopyAsync();

        var copy = Assert.Single(Assert.Single(await s.TargetAsync()).Items);
        Assert.Equal((ContentItemKind.ToolLink, CopyScenario.Quiz.Id), (copy.Kind, copy.ToolId));
    }

    [Fact]
    public async Task ToolLinkNaarEenVerwijderdeToolWordtOvergeslagen()
    {
        await using var s = await CopyScenario.CreateAsync();
        var week = await s.SourceSectionAsync("Week 1");
        await s.SourceItemAsync(s.Service.AddToolLinkAsync(week.Id, "Weekly quiz", CopyScenario.Quiz.Id, Ct));
        await s.SourceItemAsync(s.Service.AddPageAsync(week.Id, "Introduction", Ct));
        var withoutTool = s.Db.CreateService(s.Directory, tools: new FakeToolCatalog()); // the tool was deleted

        var result = await s.CopyAsync(service: withoutTool);

        Assert.Equal(new CopyResult(1, 1, 1), result.Value);
        Assert.Equal(["Introduction"], Assert.Single(await s.TargetAsync()).Items.Select(i => i.Title));
    }

    [Fact]
    public async Task OntbrekendBestandWordtOvergeslagen()
    {
        await using var s = await CopyScenario.CreateAsync();
        var week = await s.SourceSectionAsync("Week 1");
        var file = await s.SourceFileAsync(week.Id, "lost.pdf", [1]);
        await s.SourceItemAsync(s.Service.AddPageAsync(week.Id, "Introduction", Ct));
        File.Delete(Path.Combine(s.Db.FilesPath, s.Source.ToString("N"), file.Id.ToString("N")));

        var result = await s.CopyAsync();

        Assert.Equal(new CopyResult(1, 1, 1), result.Value);
        Assert.Equal(["Introduction"], Assert.Single(await s.TargetAsync()).Items.Select(i => i.Title));
    }

    [Fact]
    public async Task UitvoeringMetContentWordtNietOverschreven()
    {
        await using var s = await CopyScenario.CreateAsync();
        await s.SourceSectionAsync("Week 1");
        await s.Service.CreateSectionAsync(s.Target, "Existing", Ct);

        var result = await s.CopyAsync();

        Assert.Equal("Content.Copy.TargetNotEmpty", result.Error!.Code);
        Assert.Equal(["Existing"], (await s.TargetAsync()).Select(x => x.Title));
    }

    [Fact]
    public async Task EenUitvoeringMetAlleenVerwijderdeSectiesTeltAlsLeeg()
    {
        await using var s = await CopyScenario.CreateAsync();
        await s.SourceSectionAsync("Week 1");
        var old = (await s.Service.CreateSectionAsync(s.Target, "Old", Ct)).Value!;
        await s.Service.DeleteSectionAsync(old.Id, Ct);

        var result = await s.CopyAsync();

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(["Week 1"], (await s.TargetAsync()).Select(x => x.Title));
    }

    [Fact]
    public async Task BronVanEenAndereCursusWordtGeweigerd()
    {
        await using var s = await CopyScenario.CreateAsync();
        await s.SourceSectionAsync("Week 1");

        foreach (var source in new[] { s.Directory.OtherCourseOfferingId, s.Target, Guid.NewGuid() })
        {
            var result = await s.CopyAsync(source);

            Assert.Equal("Content.Copy.InvalidSource", result.Error!.Code);
            Assert.Equal(ErrorType.Validation, result.Error.Type);
        }

        Assert.Empty(await s.TargetAsync());
    }

    [Fact]
    public async Task ZonderBeheerrechtOpDeBronWordtNietsGekopieerd()
    {
        await using var s = await CopyScenario.CreateAsync();
        await s.SourceSectionAsync("Week 1");
        var targetOnly = s.Directory.Add("L2", OfferingRole.Lecturer); // not a lecturer of the source

        var result = await s.CopyAsync(user: FakeDirectory.User(targetOnly.PersonId));

        Assert.Equal(("Content.Access.Forbidden", ErrorType.Forbidden), (result.Error!.Code, result.Error.Type));
        Assert.Empty(await s.TargetAsync());
    }

    [Fact]
    public async Task ZonderBeheerrechtOpDeNieuweUitvoeringWordtNietsGekopieerd()
    {
        await using var s = await CopyScenario.CreateAsync();
        await s.SourceSectionAsync("Week 1");
        var student = s.Directory.Add("I6000001"); // a student of the target only

        var result = await s.CopyAsync(user: FakeDirectory.User(student.PersonId));

        Assert.Equal("Content.Access.Forbidden", result.Error!.Code);
        Assert.Empty(await s.TargetAsync());
    }

    [Fact]
    public async Task KopierenIsAllesOfNiets()
    {
        await using var s = await CopyScenario.CreateAsync();
        var week = await s.SourceSectionAsync("Week 1");
        await s.SourceFileAsync(week.Id, "first.pdf", [1]);
        var second = await s.SourceFileAsync(week.Id, "second.pdf", [2]);
        // Copying the second file fails while it is held open exclusively.
        using var locked = new FileStream(Path.Combine(s.Db.FilesPath, s.Source.ToString("N"), second.Id.ToString("N")), FileMode.Open, FileAccess.Read, FileShare.None);

        await Assert.ThrowsAsync<IOException>(() => s.CopyAsync());

        Assert.Empty(await s.TargetAsync());
        var targetFolder = Path.Combine(s.Db.FilesPath, s.Target.ToString("N"));
        Assert.Empty(Directory.Exists(targetFolder) ? Directory.GetFiles(targetFolder, "*", SearchOption.AllDirectories) : []);
    }
}
