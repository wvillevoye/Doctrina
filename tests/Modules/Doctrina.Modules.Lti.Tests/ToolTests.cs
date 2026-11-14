using Doctrina.Core.Entities;
using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Lti.Tests;

// Spec lti-tool-registration of change add-lti-platform; task 3.2. Class = requirement, method = scenario.

internal static class ToolTestData
{
    public static ToolInput Quiz(string? name = "Quiz tool", string? loginUrl = "https://tool.example.org/login", string? redirectUris = "https://tool.example.org/launch") =>
        new(name, loginUrl, "https://tool.example.org/launch", redirectUris);

    public static ToolService Service(this LtiDatabase db) => new(db.CreateContext());
}

public sealed class BeheerdersRegistrerenLtiTools
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ToolRegistreren()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var result = await db.Service().CreateAsync(ToolTestData.Quiz(), Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var tool = Assert.Single(await db.Service().ToolsAsync(Ct));
        Assert.Equal("Quiz tool", tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.ClientId));
        Assert.False(string.IsNullOrWhiteSpace(tool.DeploymentId));
        Assert.Equal(tool.Id.ToString("D"), tool.ExternalId);
        Assert.Equal("Doctrina", tool.Source);
        Assert.Equal("https://tool.example.org/launch", tool.RedirectUris);
        var catalog = (ILtiToolCatalog)db.Service();
        Assert.Equal([new LtiToolSummary(tool.Id, "Quiz tool")], await catalog.ListAsync(Ct));
        Assert.True(await catalog.ExistsAsync(tool.Id, Ct));
    }

    [Fact]
    public async Task ElkeToolKrijgtEigenIdentifiers()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var a = (await db.Service().CreateAsync(ToolTestData.Quiz("A"), Ct)).Value!;
        var b = (await db.Service().CreateAsync(ToolTestData.Quiz("B"), Ct)).Value!;

        Assert.NotEqual(a.ClientId, b.ClientId);
        Assert.NotEqual(a.DeploymentId, b.DeploymentId);
    }

    [Fact]
    public async Task ToolZonderNaamWordtGeweigerd()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var result = await db.Service().CreateAsync(ToolTestData.Quiz(name: "  "), Ct);

        Assert.Equal("Lti.Tool.NameRequired", result.Error!.Code);
        Assert.Empty(await db.Service().ToolsAsync(Ct));
    }

    [Theory]
    [InlineData("http://tool.example.org/login")]
    [InlineData("tool.example.org/login")]
    [InlineData("ftp://tool.example.org/login")]
    [InlineData("")]
    public async Task AdresZonderHttpsWordtGeweigerd(string loginUrl)
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var result = await db.Service().CreateAsync(ToolTestData.Quiz(loginUrl: loginUrl), Ct);

        Assert.Equal("Lti.Tool.InvalidUrl", result.Error!.Code);
        Assert.Empty(await db.Service().ToolsAsync(Ct));
    }

    [Fact]
    public async Task RedirectUriZonderHttpsWordtGeweigerd()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var result = await db.Service().CreateAsync(ToolTestData.Quiz(redirectUris: "https://tool.example.org/launch\nhttp://tool.example.org/other"), Ct);

        Assert.Equal("Lti.Tool.InvalidUrl", result.Error!.Code);
    }

    [Fact]
    public async Task HttpIsToegestaanVoorLocalhost()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);
        var input = new ToolInput("Local tool", "http://localhost:8080/login", "http://localhost:8080/launch", "http://localhost:8080/launch");

        var result = await db.Service().CreateAsync(input, Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n \r\n ")]
    [InlineData(null)]
    public async Task ToolZonderRedirectUriWordtGeweigerd(string? redirectUris)
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var result = await db.Service().CreateAsync(ToolTestData.Quiz(redirectUris: redirectUris), Ct);

        Assert.Equal("Lti.Tool.RedirectUriRequired", result.Error!.Code);
        Assert.Empty(await db.Service().ToolsAsync(Ct));
    }

    [Fact]
    public async Task ToolWijzigenBehoudtDeIdentifiers()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);
        var tool = (await db.Service().CreateAsync(ToolTestData.Quiz(), Ct)).Value!;

        var result = await db.Service().UpdateAsync(
            tool.Id, new ToolInput("Renamed", "https://other.example.org/login", "https://other.example.org/launch", "https://other.example.org/launch\nhttps://other.example.org/second"), Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var changed = (await db.Service().FindAsync(tool.Id, Ct))!;
        Assert.Equal("Renamed", changed.Name);
        Assert.Equal("https://other.example.org/login", changed.LoginUrl);
        Assert.Equal(tool.ClientId, changed.ClientId);
        Assert.Equal(tool.DeploymentId, changed.DeploymentId);
    }

    [Fact]
    public async Task ToolWijzigenMetOngeldigeGegevensVeranderdNiets()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);
        var tool = (await db.Service().CreateAsync(ToolTestData.Quiz(), Ct)).Value!;

        var result = await db.Service().UpdateAsync(tool.Id, ToolTestData.Quiz(name: ""), Ct);

        Assert.Equal("Lti.Tool.NameRequired", result.Error!.Code);
        Assert.Equal("Quiz tool", (await db.Service().FindAsync(tool.Id, Ct))!.Name);
        Assert.Equal("Lti.Tool.NotFound", (await db.Service().UpdateAsync(Guid.NewGuid(), ToolTestData.Quiz(), Ct)).Error!.Code);
    }

    [Fact]
    public async Task ToolVerwijderen()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);
        var tool = (await db.Service().CreateAsync(ToolTestData.Quiz(), Ct)).Value!;

        var result = await db.Service().DeleteAsync(tool.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(await db.Service().ToolsAsync(Ct));
        Assert.Null(await db.Service().FindByClientIdAsync(tool.ClientId, Ct));
        Assert.False(await db.Service().ExistsAsync(tool.Id, Ct));
        Assert.Equal(EntityStatus.ToBeDeleted, (await db.CreateContext().Tools.SingleAsync(t => t.Id == tool.Id, Ct)).Status);
        Assert.Equal("Lti.Tool.NotFound", (await db.Service().DeleteAsync(tool.Id, Ct)).Error!.Code);
    }
}

public sealed class PersoonsgegevensPerTool
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NieuweToolDeeltStandaardGeenPersoonsgegevens()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var tool = (await db.Service().CreateAsync(ToolTestData.Quiz(), Ct)).Value!;

        Assert.False(tool.SendName);
        Assert.False(tool.SendEmail);
    }

    [Fact]
    public async Task BeheerderKiestWelkeGegevensMeegaan()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);
        var tool = (await db.Service().CreateAsync(ToolTestData.Quiz(), Ct)).Value!;

        await db.Service().UpdateAsync(tool.Id, ToolTestData.Quiz() with { SendName = true }, Ct);

        var saved = (await db.Service().FindAsync(tool.Id, Ct))!;
        Assert.True(saved.SendName);
        Assert.False(saved.SendEmail);
    }
}
