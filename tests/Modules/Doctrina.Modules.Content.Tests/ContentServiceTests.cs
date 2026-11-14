using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Modules.Content.Application;
using Doctrina.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Content.Tests;

// Spec course-content; tasks 3.1-3.4 and 4.2 of add-content. Class = requirement, method = scenario.

internal static class ContentTestHelpers
{
    public static async Task<ContentSection> SectionAsync(this ContentService service, FakeDirectory directory, string title, bool published = false)
    {
        var section = (await service.CreateSectionAsync(directory.OfferingId, title, TestContext.Current.CancellationToken)).Value!;
        if (published)
        {
            await service.SetSectionPublishedAsync(section.Id, true, TestContext.Current.CancellationToken);
        }

        return section;
    }

    public static async Task<ContentItem> LinkAsync(this ContentService service, Guid sectionId, string title, bool published = false)
    {
        var item = (await service.AddLinkAsync(sectionId, title, "https://example.test/" + title.Replace(' ', '-'), TestContext.Current.CancellationToken)).Value!;
        if (published)
        {
            await service.SetItemPublishedAsync(item.Id, true, TestContext.Current.CancellationToken);
        }

        return item;
    }

    public static async Task<string[]> TitlesAsync(this ContentService service, Guid offeringId, bool drafts, Guid? sectionId = null)
    {
        var sections = await service.SectionsAsync(offeringId, drafts, TestContext.Current.CancellationToken);
        return sectionId is null
            ? [.. sections.Select(s => s.Title)]
            : [.. sections.Single(s => s.Id == sectionId).Items.Select(i => i.Title)];
    }
}

public sealed class DocentenDelenDeContentVanEenUitvoeringInSectiesIn
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DocentMaaktEenSectie()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);

        var section = await service.SectionAsync(directory, "Week 1");

        Assert.Equal(["Week 1"], await service.TitlesAsync(directory.OfferingId, drafts: true));
        Assert.Equal(section.Id.ToString("D"), section.ExternalId);
        Assert.Equal("Doctrina", section.Source);
        Assert.False(section.Published);
    }

    [Fact]
    public async Task SectieZonderTitel()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);

        var result = await service.CreateSectionAsync(directory.OfferingId, "  ", Ct);

        Assert.Equal(ContentErrors.SectionInvalid, result.Error!.Code);
        Assert.Contains("title", result.Error.Details!.Keys);
        Assert.Empty(await db.CreateContext().Sections.ToListAsync(Ct));
    }

    [Fact]
    public async Task SectieInEenOnbekendeUitvoering()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);

        var result = await db.CreateService(new FakeDirectory()).CreateSectionAsync(Guid.NewGuid(), "Week 1", Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task SectieMetItemsVerwijderen()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1", published: true);
        var page = (await service.AddPageAsync(section.Id, "Introduction", Ct)).Value!;
        var file = (await service.AddFileAsync(section.Id, null, "syllabus.pdf", "application/pdf", new MemoryStream([1, 2, 3]), Ct)).Value!;
        await using (var opened = service.OpenFile(file).Value!)
        {
            Assert.True(opened.CanRead);
        }

        Assert.True((await service.DeleteSectionAsync(section.Id, Ct)).IsSuccess);

        Assert.Empty(await service.SectionsAsync(directory.OfferingId, includeDrafts: true, Ct));
        Assert.Null(await service.FindItemAsync(directory.OfferingId, page.Id, includeDrafts: true, Ct));
        Assert.Equal(ContentErrors.FileMissing, service.OpenFile(file).Error!.Code);
        var rows = await db.CreateContext().Items.ToListAsync(Ct);
        Assert.All(rows, i => Assert.Equal(EntityStatus.ToBeDeleted, i.Status));
    }
}

public sealed class EenSectieBevatPaginasBestandenEnLinks
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LinkToevoegen()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1");

        var link = (await service.AddLinkAsync(section.Id, "Course guide", "https://example.test/guide", Ct)).Value!;

        var items = (await service.SectionsAsync(directory.OfferingId, true, Ct)).Single().Items;
        Assert.Equal(("Course guide", ContentItemKind.Link, "https://example.test/guide"), (items.Single().Title, items.Single().Kind, items.Single().Url));
        Assert.Equal(link.Id.ToString("D"), link.ExternalId);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.test/file")]
    [InlineData("example.test")]
    [InlineData("")]
    public async Task LinkZonderGeldigAdres(string url)
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1");

        var result = await service.AddLinkAsync(section.Id, "Course guide", url, Ct);

        Assert.Equal(ContentErrors.ItemInvalid, result.Error!.Code);
        Assert.Equal(ContentService.LinkMessage, result.Error.Details!["url"].Single());
        Assert.Empty(await db.CreateContext().Items.ToListAsync(Ct));
    }

    [Fact]
    public async Task ItemVerwijderen()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1");
        var link = await service.LinkAsync(section.Id, "Course guide");

        Assert.True((await service.DeleteItemAsync(link.Id, Ct)).IsSuccess);

        Assert.Empty(await service.TitlesAsync(directory.OfferingId, true, section.Id));
        Assert.Equal(ContentErrors.ItemNotFound, (await service.DeleteItemAsync(link.Id, Ct)).Error!.Code);
    }

    [Fact]
    public async Task ItemHernoemenEnPaginaOpslaan()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1");
        var page = (await service.AddPageAsync(section.Id, "Intro", Ct)).Value!;

        Assert.True((await service.SavePageAsync(page.Id, "Introduction", "# Welcome", Ct)).IsSuccess);
        Assert.Equal(ContentErrors.ItemInvalid, (await service.RenameItemAsync(page.Id, " ", Ct)).Error!.Code);

        var saved = (await service.FindItemAsync(directory.OfferingId, page.Id, includeDrafts: true, Ct))!;
        Assert.Equal(("Introduction", "# Welcome"), (saved.Title, saved.Body));
    }
}

public sealed class ContentIsPasZichtbaarNaPubliceren
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NieuwItemIsEenConcept()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1", published: true);

        var page = (await service.AddPageAsync(section.Id, "Introduction", Ct)).Value!;

        Assert.Empty(await service.TitlesAsync(directory.OfferingId, drafts: false, section.Id));
        Assert.Equal(["Introduction"], await service.TitlesAsync(directory.OfferingId, drafts: true, section.Id));
        Assert.Null(await service.FindItemAsync(directory.OfferingId, page.Id, includeDrafts: false, Ct));
    }

    [Fact]
    public async Task GepubliceerdItemInEenVerborgenSectie()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1", published: false);
        var page = (await service.AddPageAsync(section.Id, "Introduction", Ct)).Value!;
        await service.SetItemPublishedAsync(page.Id, true, Ct);

        Assert.Empty(await service.TitlesAsync(directory.OfferingId, drafts: false));
        Assert.Null(await service.FindItemAsync(directory.OfferingId, page.Id, includeDrafts: false, Ct));
        Assert.NotNull(await service.FindItemAsync(directory.OfferingId, page.Id, includeDrafts: true, Ct));
    }

    [Fact]
    public async Task ItemVerbergen()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1", published: true);
        var page = await service.LinkAsync(section.Id, "Introduction", published: true);
        Assert.Equal(["Introduction"], await service.TitlesAsync(directory.OfferingId, false, section.Id));

        await service.SetItemPublishedAsync(page.Id, false, Ct);

        Assert.Empty(await service.TitlesAsync(directory.OfferingId, false, section.Id));
    }
}

public sealed class DocentenBepalenDeVolgordeVanSectiesEnItems
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ItemOmhoogVerplaatsen()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1");
        await service.LinkAsync(section.Id, "A");
        await service.LinkAsync(section.Id, "B");
        var c = await service.LinkAsync(section.Id, "C");
        Assert.Equal(["A", "B", "C"], await service.TitlesAsync(directory.OfferingId, true, section.Id));

        await service.MoveItemAsync(c.Id, -1, Ct);

        Assert.Equal(["A", "C", "B"], await service.TitlesAsync(directory.OfferingId, true, section.Id));
    }

    [Fact]
    public async Task SectieOmlaagVerplaatsen()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var week1 = await service.SectionAsync(directory, "Week 1", published: true);
        await service.SectionAsync(directory, "Week 2", published: true);

        await service.MoveSectionAsync(week1.Id, 1, Ct);

        Assert.Equal(["Week 2", "Week 1"], await service.TitlesAsync(directory.OfferingId, drafts: false));
    }

    [Fact]
    public async Task HetEersteItemOmhoogVerplaatsenWijzigtNiets()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1");
        var a = await service.LinkAsync(section.Id, "A");
        await service.LinkAsync(section.Id, "B");

        Assert.True((await service.MoveItemAsync(a.Id, -1, Ct)).IsSuccess);

        Assert.Equal(["A", "B"], await service.TitlesAsync(directory.OfferingId, true, section.Id));
    }

    [Fact]
    public async Task EenVerwijderdItemWordtOvergeslagen()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1");
        await service.LinkAsync(section.Id, "A");
        var b = await service.LinkAsync(section.Id, "B");
        var c = await service.LinkAsync(section.Id, "C");
        await service.DeleteItemAsync(b.Id, Ct);

        await service.MoveItemAsync(c.Id, -1, Ct);

        Assert.Equal(["C", "A"], await service.TitlesAsync(directory.OfferingId, true, section.Id));
    }
}

public sealed class BestandenWordenGeuploadEnGedownload
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BestandUploadenEnDownloaden()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1", published: true);
        byte[] bytes = [.. Enumerable.Range(0, 200_000).Select(i => (byte)(i % 251))];

        var item = (await service.AddFileAsync(section.Id, null, "syllabus.pdf", "application/pdf", new MemoryStream(bytes), Ct)).Value!;
        await service.SetItemPublishedAsync(item.Id, true, Ct);

        var found = (await service.FindItemAsync(directory.OfferingId, item.Id, includeDrafts: false, Ct))!;
        await using var stream = service.OpenFile(found).Value!;
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, Ct);
        Assert.Equal(("syllabus.pdf", "syllabus.pdf", "application/pdf", 200_000L), (found.Title, found.FileName, found.ContentType, found.Size));
        Assert.Equal(bytes, copy.ToArray());
    }

    [Fact]
    public async Task BestandTeGroot()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory, maxSizeMb: 1);
        var section = await service.SectionAsync(directory, "Week 1");

        var result = await service.AddFileAsync(section.Id, null, "big.bin", null, new MemoryStream(new byte[2 * 1024 * 1024]), Ct);

        Assert.Equal(ContentErrors.FileTooLarge, result.Error!.Code);
        Assert.Equal("The file is larger than 1 MB.", result.Error.Details!["file"].Single());
        Assert.Empty(await db.CreateContext().Items.ToListAsync(Ct));
        Assert.Empty(Directory.Exists(db.FilesPath) ? Directory.EnumerateFiles(db.FilesPath, "*", SearchOption.AllDirectories) : []);
    }

    [Fact]
    public async Task VerwijderenHaaltHetBestandVanSchijf()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1");
        var item = (await service.AddFileAsync(section.Id, "Syllabus", "syllabus.pdf", "application/pdf", new MemoryStream([1]), Ct)).Value!;
        Assert.Single(Directory.EnumerateFiles(db.FilesPath, "*", SearchOption.AllDirectories));

        await service.DeleteItemAsync(item.Id, Ct);

        Assert.Empty(Directory.EnumerateFiles(db.FilesPath, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(@"..\..\x.pdf", "x.pdf")]
    [InlineData("../../etc/x.pdf", "x.pdf")]
    [InlineData(@"C:\Users\me\x.pdf", "x.pdf")]
    public async Task EenBestandsnaamMetEenPadWordtAlleenDeNaam(string sent, string expected)
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1");

        var item = (await service.AddFileAsync(section.Id, null, sent, null, new MemoryStream([1]), Ct)).Value!;

        Assert.Equal(expected, item.FileName);
        Assert.All(Directory.EnumerateFiles(db.FilesPath, "*", SearchOption.AllDirectories), f => Assert.StartsWith(db.FilesPath, Path.GetFullPath(f)));
    }

    [Fact]
    public async Task EenOntbrekendBestandGeeftContentFileMissing()
    {
        await using var db = await ContentDatabase.CreateAsync(Ct);
        var directory = new FakeDirectory();
        var service = db.CreateService(directory);
        var section = await service.SectionAsync(directory, "Week 1");
        var item = (await service.AddFileAsync(section.Id, null, "a.txt", null, new MemoryStream([1]), Ct)).Value!;
        Directory.Delete(db.FilesPath, recursive: true);

        Assert.Equal(ContentErrors.FileMissing, service.OpenFile(item).Error!.Code);
    }
}
