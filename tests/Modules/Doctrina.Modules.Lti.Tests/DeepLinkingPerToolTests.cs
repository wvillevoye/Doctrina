using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Contracts;

namespace Doctrina.Modules.Lti.Tests;

// Spec lti-tool-registration of change add-lti-deep-linking; task 1.1. Class = requirement, method = scenario.

public sealed class DeepLinkingPerTool
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ToolInput WithDeepLinking(string? deepLinkingUrl, string? publicKeysUrl) =>
        ToolTestData.Quiz() with { DeepLinkingUrl = deepLinkingUrl, PublicKeysUrl = publicKeysUrl };

    [Fact]
    public async Task ToolMetDeepLinkingOpslaan()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var result = await db.Service().CreateAsync(WithDeepLinking("https://tool.example.org/dl", "https://tool.example.org/jwks"), Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var tool = Assert.Single(await db.Service().ToolsAsync(Ct));
        Assert.Equal("https://tool.example.org/dl", tool.DeepLinkingUrl);
        Assert.Equal("https://tool.example.org/jwks", tool.PublicKeysUrl);
        Assert.True(Assert.Single(await ((ILtiToolCatalog)db.Service()).ListAsync(Ct)).SupportsDeepLinking);
    }

    [Fact]
    public async Task DeepLinkingUrlZonderPublicKeysUrlWordtGeweigerd()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var result = await db.Service().CreateAsync(WithDeepLinking("https://tool.example.org/dl", null), Ct);

        Assert.Equal("Lti.Tool.PublicKeysUrlRequired", result.Error!.Code);
        Assert.Empty(await db.Service().ToolsAsync(Ct));
    }

    [Fact]
    public async Task PublicKeysUrlZonderDeepLinkingUrlWordtGeweigerd()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var result = await db.Service().CreateAsync(WithDeepLinking("  ", "https://tool.example.org/jwks"), Ct);

        Assert.Equal("Lti.Tool.DeepLinkingUrlRequired", result.Error!.Code);
        Assert.Empty(await db.Service().ToolsAsync(Ct));
    }

    [Fact]
    public async Task DeepLinkingAdresZonderHttpsWordtGeweigerd()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var result = await db.Service().CreateAsync(WithDeepLinking("https://tool.example.org/dl", "http://tool.example.org/jwks"), Ct);

        Assert.Equal("Lti.Tool.InvalidUrl", result.Error!.Code);
        Assert.Empty(await db.Service().ToolsAsync(Ct));
    }

    [Fact]
    public async Task BestaandeToolBlijftWerkenZonderDeepLinking()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);
        var tool = (await db.Service().CreateAsync(ToolTestData.Quiz(), Ct)).Value!;

        var update = await db.Service().UpdateAsync(tool.Id, ToolTestData.Quiz("Renamed"), Ct);

        Assert.True(update.IsSuccess, update.Error?.ToString());
        var found = (await db.Service().FindAsync(tool.Id, Ct))!;
        Assert.Null(found.DeepLinkingUrl);
        Assert.Null(found.PublicKeysUrl);
        Assert.False(Assert.Single(await ((ILtiToolCatalog)db.Service()).ListAsync(Ct)).SupportsDeepLinking);
    }
}
