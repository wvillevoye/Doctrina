using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Application;

namespace Doctrina.Modules.Content.Tests;

// Spec course-content; tasks 3.5 and 4.1 of add-content.

public sealed class AlleenDocentenVanDeUitvoeringEnBeherenContentToegang
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(OfferingRole.Lecturer, true)]
    [InlineData(OfferingRole.Coordinator, true)]
    [InlineData(OfferingRole.TeachingAssistant, false)]
    [InlineData(OfferingRole.Student, false)]
    public async Task BeherenPerRol(OfferingRole role, bool expected)
    {
        var directory = new FakeDirectory();
        var person = directory.Add("I6000001", role);

        Assert.Equal(expected, await new ContentAccess(directory).CanManageAsync(FakeDirectory.User(person.PersonId), directory.OfferingId, Ct));
    }

    [Fact]
    public async Task DocentVanEenAndereUitvoering()
    {
        var directory = new FakeDirectory();
        var lecturer = directory.Add("L1", OfferingRole.Lecturer);

        Assert.False(await new ContentAccess(directory).CanManageAsync(FakeDirectory.User(lecturer.PersonId), Guid.NewGuid(), Ct));
        Assert.False(await new ContentAccess(directory).CanReadAsync(FakeDirectory.User(lecturer.PersonId), Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task BeheerderMetContentManage()
    {
        var directory = new FakeDirectory();
        var access = new ContentAccess(directory);

        Assert.True(await access.CanManageAsync(FakeDirectory.User(null, ContentPermissions.Manage), directory.OfferingId, Ct));
        Assert.True(await access.CanReadAsync(FakeDirectory.User(null, ContentPermissions.Manage), directory.OfferingId, Ct));
        Assert.False(await access.CanManageAsync(FakeDirectory.User(null), directory.OfferingId, Ct));
    }

    [Fact]
    public async Task LezenMetContentReadOfMetEenInschrijving()
    {
        var directory = new FakeDirectory();
        var student = directory.Add("I6000001");
        var access = new ContentAccess(directory);

        Assert.True(await access.CanReadAsync(FakeDirectory.User(student.PersonId), directory.OfferingId, Ct));
        Assert.True(await access.CanReadAsync(FakeDirectory.User(null, ContentPermissions.Read), directory.OfferingId, Ct));
        Assert.False(await access.CanManageAsync(FakeDirectory.User(null, ContentPermissions.Read), directory.OfferingId, Ct));
        Assert.False(await access.CanReadAsync(FakeDirectory.User(Guid.NewGuid()), directory.OfferingId, Ct));
        Assert.False(await access.CanReadAsync(FakeDirectory.User(null), directory.OfferingId, Ct));
    }
}

public sealed class PaginasWordenInMarkdownGeschrevenEnVeiligWeergegeven
{
    [Fact]
    public void OpmaakWordtWeergegeven()
    {
        var html = Markdown.ToSafeHtml("# Welcome\n\n**Read this first**");

        Assert.Contains("<h1", html, StringComparison.Ordinal);
        Assert.Contains("Welcome</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Read this first</strong>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("text <img src=x onerror=alert(1)> text")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>")]
    public void HtmlEnScriptsWordenNietUitgevoerd(string text)
    {
        var html = Markdown.ToSafeHtml(text);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<a href=\"javascript", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](JaVaScRiPt:alert(1))")]
    [InlineData("[x](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("![x](data:image/png;base64,AAAA)")]
    [InlineData("![x](mailto:me@example.test)")]
    [InlineData("<javascript:alert(1)>")]
    public void GevaarlijkeAdressenWordenLeeggemaakt(string text)
    {
        var html = Markdown.ToSafeHtml(text);

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mailto:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("[x](https://example.test/a)", "href=\"https://example.test/a\"")]
    [InlineData("[x](http://example.test/a)", "href=\"http://example.test/a\"")]
    [InlineData("[x](mailto:me@example.test)", "href=\"mailto:me@example.test\"")]
    [InlineData("![x](https://example.test/a.png)", "src=\"https://example.test/a.png\"")]
    public void GewoneAdressenBlijvenWerken(string text, string expected) =>
        Assert.Contains(expected, Markdown.ToSafeHtml(text), StringComparison.Ordinal);

    [Fact]
    public void LegeTekstGeeftLegeWeergave() => Assert.Equal(string.Empty, Markdown.ToSafeHtml(null).Trim());
}
