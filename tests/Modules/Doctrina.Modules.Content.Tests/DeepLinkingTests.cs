using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Application;
using Doctrina.Modules.Content.Contracts;
using Doctrina.Modules.Content.Domain;
using Doctrina.Modules.Lti.Contracts;

namespace Doctrina.Modules.Content.Tests;

// Spec lti-deep-linking of change add-lti-deep-linking; task 2.1 (the Content side of confirming a selection).
// Class = requirement, method = scenario.

public sealed class DeDocentBevestigtDeSelectie
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly LtiToolSummary Quiz = new(Guid.NewGuid(), "Quiz tool", true);

    private static readonly IReadOnlyList<NewToolLink> Selection =
    [
        new("Week 1 quiz", "https://quiz.example.org/launch?quiz=1"),
        new("Week 2 quiz", "https://quiz.example.org/launch?quiz=2"),
    ];

    private static async Task<(ContentDatabase Db, ContentService Service, IContentDirectory Directory, System.Security.Claims.ClaimsPrincipal Lecturer, ContentSection Section)> SetUpAsync()
    {
        var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var lecturer = directory.Add("L1", OfferingRole.Lecturer);
        var service = db.CreateService(directory, tools: new FakeToolCatalog(Quiz));
        var section = (await service.CreateSectionAsync(directory.OfferingId, "Week 1", Ct)).Value!;
        IContentDirectory contract = new ContentDirectory(db.CreateContext(), new ContentAccess(directory), service);
        return (db, service, contract, FakeDirectory.User(lecturer.PersonId), section);
    }

    [Fact]
    public async Task DocentVoegtDeSelectieToe()
    {
        var (db, service, directory, lecturer, section) = await SetUpAsync();
        await using var _ = db;

        var result = await directory.AddToolLinksAsync(lecturer, section.Id, Quiz.Id, Selection, Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var items = Assert.Single(await service.SectionsAsync(section.OfferingId, true, Ct)).Items;
        Assert.Equal(["Week 1 quiz", "Week 2 quiz"], items.Select(i => i.Title));
        Assert.All(items, i => Assert.Equal((ContentItemKind.ToolLink, Quiz.Id, false), (i.Kind, i.ToolId!.Value, i.Published)));
        Assert.Equal(["https://quiz.example.org/launch?quiz=1", "https://quiz.example.org/launch?quiz=2"], items.Select(i => i.Url));
        Assert.Equal([1, 2], items.Select(i => i.Position));
    }

    [Fact]
    public async Task StudentZietDeNieuweToolLinkNiet()
    {
        var (db, service, directory, lecturer, section) = await SetUpAsync();
        await using var _ = db;

        await directory.AddToolLinksAsync(lecturer, section.Id, Quiz.Id, Selection, Ct);

        Assert.Empty(await service.SectionsAsync(section.OfferingId, false, Ct));
    }

    [Fact]
    public async Task VerwijderdeSectieVoegtNietsToe()
    {
        var (db, service, directory, lecturer, section) = await SetUpAsync();
        await using var _ = db;
        await service.DeleteSectionAsync(section.Id, Ct);

        var result = await directory.AddToolLinksAsync(lecturer, section.Id, Quiz.Id, Selection, Ct);

        Assert.Equal("Content.Section.NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task ZonderBeheerrechtWordtNietsToegevoegd()
    {
        var (db, service, directory, _, section) = await SetUpAsync();
        await using var _1 = db;

        var result = await directory.AddToolLinksAsync(FakeDirectory.User(Guid.NewGuid()), section.Id, Quiz.Id, Selection, Ct);

        Assert.Equal(IContentDirectory.AccessForbiddenCode, result.Error!.Code);
        Assert.Empty(Assert.Single(await service.SectionsAsync(section.OfferingId, true, Ct)).Items);
    }

    [Fact]
    public async Task OngeldigeSelectieVoegtNietsToe()
    {
        var (db, service, directory, lecturer, section) = await SetUpAsync();
        await using var _ = db;

        var result = await directory.AddToolLinksAsync(lecturer, section.Id, Quiz.Id, [Selection[0], new("Bad", "ftp://quiz.example.org/x")], Ct);

        Assert.Equal("Content.Item.Invalid", result.Error!.Code);
        Assert.Empty(Assert.Single(await service.SectionsAsync(section.OfferingId, true, Ct)).Items);
    }
}

// Spec lti-deep-linking of change add-lti-deep-linking; task 6.1 (the copy side).
public sealed class EenToolLinkMetEigenAdresStartDatAdres
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GekopieerdeToolLinkBehoudtHetAdres()
    {
        await using var s = await CopyScenario.CreateAsync();
        var week = await s.SourceSectionAsync("Week 1");
        Assert.True((await s.Service.AddToolLinksAsync(week.Id, CopyScenario.Quiz.Id, [new("Quiz 7", "https://quiz.example.org/launch?quiz=7")], Ct)).IsSuccess);

        var result = await s.CopyAsync();

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var copy = Assert.Single(Assert.Single(await s.TargetAsync()).Items);
        Assert.Equal((CopyScenario.Quiz.Id, "https://quiz.example.org/launch?quiz=7"), (copy.ToolId!.Value, copy.Url));
    }
}
