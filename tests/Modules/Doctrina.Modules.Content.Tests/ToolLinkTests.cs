using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Application;
using Doctrina.Modules.Content.Contracts;
using Doctrina.Modules.Content.Domain;
using Doctrina.Modules.Lti.Contracts;

namespace Doctrina.Modules.Content.Tests;

// Spec lti-launch of change add-lti-platform; task 2.2. Class = requirement, method = scenario.

internal sealed class FakeToolCatalog(params LtiToolSummary[] tools) : ILtiToolCatalog
{
    public Task<IReadOnlyList<LtiToolSummary>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LtiToolSummary>>(tools);

    public Task<bool> ExistsAsync(Guid toolId, CancellationToken cancellationToken) => Task.FromResult(tools.Any(t => t.Id == toolId));
}

public sealed class ToolLinksInEenSectie
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly LtiToolSummary Quiz = new(Guid.NewGuid(), "Quiz tool");

    [Fact]
    public async Task DocentVoegtEenToolLinkToe()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory, tools: new FakeToolCatalog(Quiz));
        var section = await service.SectionAsync(directory, "Week 1");

        var result = await service.AddToolLinkAsync(section.Id, "Weekly quiz", Quiz.Id, Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(ContentItemKind.ToolLink, result.Value!.Kind);
        Assert.Equal(Quiz.Id, result.Value.ToolId);
        Assert.False(result.Value.Published);
        Assert.Equal(["Weekly quiz"], await service.TitlesAsync(directory.OfferingId, drafts: true, section.Id));
    }

    [Fact]
    public async Task ToolLinkNaarEenOnbekendeToolWordtGeweigerd()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory, tools: new FakeToolCatalog(Quiz));
        var section = await service.SectionAsync(directory, "Week 1");

        var result = await service.AddToolLinkAsync(section.Id, "Weekly quiz", Guid.NewGuid(), Ct);

        Assert.Equal("Content.Item.UnknownTool", result.Error!.Code);
        Assert.Empty(await service.TitlesAsync(directory.OfferingId, drafts: true, section.Id));
    }

    [Fact]
    public async Task ZonderLtiModuleGeenToolLinks()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1");

        var result = await service.AddToolLinkAsync(section.Id, "Weekly quiz", Quiz.Id, Ct);

        Assert.Equal("Content.Item.UnknownTool", result.Error!.Code);
    }
}

public sealed class ToolLinksVolgenDeRegelsVanItems
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly LtiToolSummary Quiz = new(Guid.NewGuid(), "Quiz tool");

    [Fact]
    public async Task ToolLinkPubliceertVerbergtEnVerplaatstZoalsAndereItems()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory, tools: new FakeToolCatalog(Quiz));
        var section = await service.SectionAsync(directory, "Week 1", published: true);
        var quiz = (await service.AddToolLinkAsync(section.Id, "Weekly quiz", Quiz.Id, Ct)).Value!;
        await service.LinkAsync(section.Id, "Guide", published: true);

        Assert.Equal(["Guide"], await service.TitlesAsync(directory.OfferingId, drafts: false, section.Id));
        await service.SetItemPublishedAsync(quiz.Id, true, Ct);
        Assert.Equal(["Weekly quiz", "Guide"], await service.TitlesAsync(directory.OfferingId, drafts: false, section.Id));
        await service.MoveItemAsync(quiz.Id, 1, Ct);
        Assert.Equal(["Guide", "Weekly quiz"], await service.TitlesAsync(directory.OfferingId, drafts: false, section.Id));
        await service.SetItemPublishedAsync(quiz.Id, false, Ct);
        Assert.Equal(["Guide"], await service.TitlesAsync(directory.OfferingId, drafts: false, section.Id));
        Assert.True((await service.RenameItemAsync(quiz.Id, "Quiz", Ct)).IsSuccess);
        Assert.True((await service.DeleteItemAsync(quiz.Id, Ct)).IsSuccess);
        Assert.Equal(["Guide"], await service.TitlesAsync(directory.OfferingId, drafts: true, section.Id));
    }
}

public sealed class DeContentDirectoryGeeftToolLinksAanAndereModules
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly LtiToolSummary Quiz = new(Guid.NewGuid(), "Quiz tool");

    private static async Task<(ContentDatabase Db, FakeDirectory Directory, ContentDirectory Contract, ContentItem Item, OfferingMember Student, OfferingMember Lecturer)> SetupAsync(bool published)
    {
        var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var student = directory.Add("S1");
        var lecturer = directory.Add("L1", OfferingRole.Lecturer);
        var service = db.CreateService(directory, tools: new FakeToolCatalog(Quiz));
        var section = await service.SectionAsync(directory, "Week 1", published: true);
        var item = (await service.AddToolLinkAsync(section.Id, "Weekly quiz", Quiz.Id, Ct)).Value!;
        if (published)
        {
            await service.SetItemPublishedAsync(item.Id, true, Ct);
        }

        return (db, directory, new ContentDirectory(db.CreateContext(), new ContentAccess(directory), service), item, student, lecturer);
    }

    [Fact]
    public async Task DeelnemerVindtEenGepubliceerdeToolLink()
    {
        var (db, _, contract, item, student, _) = await SetupAsync(published: true);
        await using (db)
        {
            var result = await contract.FindToolLinkAsync(FakeDirectory.User(student.PersonId), item.Id, Ct);

            Assert.True(result.IsSuccess, result.Error?.ToString());
            Assert.Equal(Quiz.Id, result.Value!.ToolId);
            Assert.Equal("Weekly quiz", result.Value.Title);
        }
    }

    [Fact]
    public async Task ConceptIsOnzichtbaarVoorEenStudentMaarNietVoorEenDocent()
    {
        var (db, _, contract, item, student, lecturer) = await SetupAsync(published: false);
        await using (db)
        {
            Assert.Equal("Content.Item.NotFound", (await contract.FindToolLinkAsync(FakeDirectory.User(student.PersonId), item.Id, Ct)).Error!.Code);
            Assert.True((await contract.FindToolLinkAsync(FakeDirectory.User(lecturer.PersonId), item.Id, Ct)).IsSuccess);
        }
    }

    [Fact]
    public async Task NietDeelnemerKrijgtForbidden()
    {
        var (db, _, contract, item, _, _) = await SetupAsync(published: true);
        await using (db)
        {
            Assert.Equal("Content.Access.Forbidden", (await contract.FindToolLinkAsync(FakeDirectory.User(Guid.NewGuid()), item.Id, Ct)).Error!.Code);
        }
    }

    [Fact]
    public async Task ItemDatGeenToolLinkIsOfNietBestaat()
    {
        var (db, directory, contract, _, student, _) = await SetupAsync(published: true);
        await using (db)
        {
            var service = db.CreateService(directory);
            var section = (await service.SectionsAsync(directory.OfferingId, true, Ct))[0];
            var link = await service.LinkAsync(section.Id, "Guide", published: true);

            Assert.Equal("Content.Item.NotFound", (await contract.FindToolLinkAsync(FakeDirectory.User(student.PersonId), link.Id, Ct)).Error!.Code);
            Assert.Equal("Content.Item.NotFound", (await contract.FindToolLinkAsync(FakeDirectory.User(student.PersonId), Guid.NewGuid(), Ct)).Error!.Code);
        }
    }
}
