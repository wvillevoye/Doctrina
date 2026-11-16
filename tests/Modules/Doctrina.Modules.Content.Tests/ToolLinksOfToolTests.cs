using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Application;
using Doctrina.Modules.Content.Contracts;
using Doctrina.Modules.Content.Domain;
using Doctrina.Modules.Lti.Contracts;

namespace Doctrina.Modules.Content.Tests;

// Contract of change add-lti-ags; task 3.1: which tool links does a tool have in an offering (used to decide what a tool may reach).

public sealed class DeToolLinksVanEenToolInEenUitvoering
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly LtiToolSummary Quiz = new(Guid.NewGuid(), "Quiz tool", true);
    private static readonly LtiToolSummary Other = new(Guid.NewGuid(), "Other tool", false);

    private static async Task<(ContentDatabase Db, ContentService Service, IContentDirectory Directory, ContentSection Section)> SetUpAsync()
    {
        var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory, tools: new FakeToolCatalog(Quiz, Other));
        var section = (await service.CreateSectionAsync(directory.OfferingId, "Week 1", Ct)).Value!;
        await service.AddToolLinksAsync(section.Id, Quiz.Id, [new("Week 2 quiz", "https://quiz.example.org/launch?quiz=2"), new("Week 1 quiz", "https://quiz.example.org/launch?quiz=1")], Ct);
        await service.AddToolLinksAsync(section.Id, Other.Id, [new("Other thing", "https://other.example.org/launch")], Ct);
        return (db, service, new ContentDirectory(db.CreateContext(), new ContentAccess(directory), service), section);
    }

    [Fact]
    public async Task ToolLinksVanDeToolInDeUitvoering()
    {
        var (db, _, directory, section) = await SetUpAsync();
        await using var _ = db;

        var links = await directory.ToolLinksOfToolAsync(section.OfferingId, Quiz.Id, Ct);

        Assert.Equal(["Week 1 quiz", "Week 2 quiz"], links.Select(l => l.Title));
        Assert.All(links, l => Assert.Equal((section.OfferingId, Quiz.Id), (l.OfferingId, l.ToolId)));
    }

    [Fact]
    public async Task LeegVoorEenAndereToolOfUitvoering()
    {
        var (db, _, directory, section) = await SetUpAsync();
        await using var _ = db;

        Assert.Empty(await directory.ToolLinksOfToolAsync(section.OfferingId, Guid.NewGuid(), Ct));
        Assert.Empty(await directory.ToolLinksOfToolAsync(Guid.NewGuid(), Quiz.Id, Ct));
    }

    [Fact]
    public async Task VerwijderdeSectieTeltNietMee()
    {
        var (db, service, directory, section) = await SetUpAsync();
        await using var _ = db;
        await service.DeleteSectionAsync(section.Id, Ct);

        Assert.Empty(await directory.ToolLinksOfToolAsync(section.OfferingId, Quiz.Id, Ct));
    }
}
