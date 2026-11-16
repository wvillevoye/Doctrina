using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Contracts;

namespace Doctrina.Modules.Lti.Tests;

// Spec lti-tool-registration of change add-lti-nrps; task 1.1. Class = requirement, method = scenario.

public sealed class LedenservicesPerTool
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ToolInput WithMembers(string? publicKeysUrl) =>
        ToolTestData.Quiz() with { MembershipServices = true, PublicKeysUrl = publicKeysUrl };

    [Fact]
    public async Task ToolMetLedenservicesOpslaan()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var result = await db.Service().CreateAsync(WithMembers("https://tool.example.org/jwks"), Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var tool = Assert.Single(await db.Service().ToolsAsync(Ct));
        Assert.True(tool.MembershipServices);
        Assert.Null(tool.DeepLinkingUrl);
        Assert.Equal("https://tool.example.org/jwks", tool.PublicKeysUrl);
        Assert.True(Assert.Single(await ((ILtiToolCatalog)db.Service()).ListAsync(Ct)).SupportsMembershipServices);
    }

    [Fact]
    public async Task LedenservicesZonderPublicKeysUrlWordtGeweigerd()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var result = await db.Service().CreateAsync(WithMembers(null), Ct);

        Assert.Equal("Lti.Tool.PublicKeysUrlRequired", result.Error!.Code);
        Assert.Empty(await db.Service().ToolsAsync(Ct));
    }

    [Fact]
    public async Task BestaandeToolHeeftGeenLedenservices()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);
        var tool = (await db.Service().CreateAsync(ToolTestData.Quiz(), Ct)).Value!;

        var update = await db.Service().UpdateAsync(tool.Id, ToolTestData.Quiz("Renamed"), Ct);

        Assert.True(update.IsSuccess, update.Error?.ToString());
        Assert.False((await db.Service().FindAsync(tool.Id, Ct))!.MembershipServices);
        Assert.False(Assert.Single(await ((ILtiToolCatalog)db.Service()).ListAsync(Ct)).SupportsMembershipServices);
    }
}
