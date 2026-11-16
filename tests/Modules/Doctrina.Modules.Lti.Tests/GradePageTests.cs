using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Contracts;
using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Components;
using Doctrina.Modules.Lti.Components.Pages;
using Doctrina.TestKit.Fakes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Modules.Lti.Tests;

// Specs lti-grade-services of change add-lti-ags; task 5.2: the pages call GradeService and show the outcome (the 403 for a
// student is tested against the host). Class = requirement, method = scenario.

public sealed class EenStudentZietDeEigenScoresOpDePagina : IAsyncDisposable
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private GradeScenario? _s;
    private PageFixture? _f;

    private async Task<IRenderedComponent<MyScoresSection>> RenderAsync(Func<GradeScenario, OfferingMember> signedIn)
    {
        _s = await GradeScenario.CreateAsync();
        _f = new PageFixtureBuilder(_s).Build();
        var item = await _s.CreateItemAsync("Quiz 1", resourceLinkId: _s.Link.ItemId.ToString("D"));
        await _s.Service().PostScoreAsync(_s.Tool, _s.Offering, item.Id, GradeScenario.Score(_s.Anna, 8, 10), Ct);
        _f.Context.AddAuthorization().SetClaims(new Claim(FakeDirectory.PersonClaim, signedIn(_s).PersonId.ToString()));
        return _f.Context.Render<MyScoresSection>(p => p.Add(c => c.OfferingId, _s.Offering));
    }

    [Fact]
    public async Task StudentZietDeEigenScore()
    {
        var section = await RenderAsync(s => s.Anna);

        Assert.Equal("Quiz 1 – Quiz 1: 8 / 10", section.WaitForElement("[data-testid=my-score]", PageFixture.Wait).TextContent.Trim());
    }

    [Fact]
    public async Task StudentZietGeenScoresVanAnderen()
    {
        var section = await RenderAsync(s => s.Bram);

        section.WaitForState(() => true, PageFixture.Wait);
        Assert.Empty(section.FindAll("[data-testid=my-scores]")); // no section at all
        Assert.DoesNotContain("8 / 10", section.Markup, StringComparison.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        if (_f is not null)
        {
            await _f.Context.DisposeAsync();
        }

        if (_s is not null)
        {
            await _s.DisposeAsync();
        }
    }
}

public sealed class EenDocentZietDeScoresPerToolLinkOpDePagina : IAsyncDisposable
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private GradeScenario? _s;
    private PageFixture? _f;

    private async Task<IRenderedComponent<ScoreOverview>> RenderAsync(bool canManage)
    {
        _s = await GradeScenario.CreateAsync();
        _s.Content.CanManage = canManage;
        _f = new PageFixtureBuilder(_s).Build();
        var item = await _s.CreateItemAsync("Quiz 1", resourceLinkId: _s.Link.ItemId.ToString("D"));
        await _s.Service().PostScoreAsync(_s.Tool, _s.Offering, item.Id, GradeScenario.Score(_s.Anna, 8, 10), Ct);
        return _f.Context.Render<ScoreOverview>(p => p.Add(c => c.ItemId, _s.Link.ItemId));
    }

    [Fact]
    public async Task DocentZietHetOverzicht()
    {
        var page = await RenderAsync(canManage: true);

        page.WaitForElement("[data-testid=line-item]", PageFixture.Wait);
        Assert.Contains("I6000001 Name", page.Markup, StringComparison.Ordinal);
        Assert.Contains("8 / 10", page.Markup, StringComparison.Ordinal);
        Assert.Contains("I6000002 Name", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZonderBeheerrechtGaatDePaginaNaarToegangGeweigerd()
    {
        var page = await RenderAsync(canManage: false);

        page.WaitForState(() => _f!.Context.Services.GetRequiredService<NavigationManager>().Uri.EndsWith("/access-denied", StringComparison.Ordinal), PageFixture.Wait);
        Assert.DoesNotContain("I6000001", page.Markup, StringComparison.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        if (_f is not null)
        {
            await _f.Context.DisposeAsync();
        }

        if (_s is not null)
        {
            await _s.DisposeAsync();
        }
    }
}

/// <summary>A page fixture on the database of a <see cref="GradeScenario"/>, with its fake directories.</summary>
internal sealed class PageFixtureBuilder(GradeScenario s)
{
    public PageFixture Build()
    {
        var f = PageFixture.For(s.Db);
        f.Context.Services.AddScoped<IContentDirectory>(_ => s.Content);
        f.Context.Services.AddSingleton<IAcademicDirectory>(s.Directory);
        f.Context.Services.AddScoped<GradeService>();
        return f;
    }
}
