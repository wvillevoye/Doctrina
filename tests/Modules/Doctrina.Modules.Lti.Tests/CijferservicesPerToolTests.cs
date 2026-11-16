using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Contracts;

namespace Doctrina.Modules.Lti.Tests;

// Spec lti-tool-registration of change add-lti-ags; task 1.1. Class = requirement, method = scenario.

public sealed class CijferservicesPerTool
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ToolInput WithGrades(string? publicKeysUrl) =>
        ToolTestData.Quiz() with { GradeServices = true, PublicKeysUrl = publicKeysUrl };

    [Fact]
    public async Task ToolMetCijferservicesOpslaan()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var result = await db.Service().CreateAsync(WithGrades("https://tool.example.org/jwks"), Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var tool = Assert.Single(await db.Service().ToolsAsync(Ct));
        Assert.True(tool.GradeServices);
        Assert.Null(tool.DeepLinkingUrl);
        Assert.Equal("https://tool.example.org/jwks", tool.PublicKeysUrl);
        Assert.True(Assert.Single(await ((ILtiToolCatalog)db.Service()).ListAsync(Ct)).SupportsGradeServices);
    }

    [Fact]
    public async Task CijferservicesZonderPublicKeysUrlWordtGeweigerd()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var result = await db.Service().CreateAsync(WithGrades(null), Ct);

        Assert.Equal("Lti.Tool.PublicKeysUrlRequired", result.Error!.Code);
        Assert.Empty(await db.Service().ToolsAsync(Ct));
    }

    [Fact]
    public async Task BestaandeToolHeeftGeenCijferservices()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);
        var tool = (await db.Service().CreateAsync(ToolTestData.Quiz(), Ct)).Value!;

        var update = await db.Service().UpdateAsync(tool.Id, ToolTestData.Quiz("Renamed"), Ct);

        Assert.True(update.IsSuccess, update.Error?.ToString());
        Assert.False((await db.Service().FindAsync(tool.Id, Ct))!.GradeServices);
        Assert.False(Assert.Single(await ((ILtiToolCatalog)db.Service()).ListAsync(Ct)).SupportsGradeServices);
    }
}
