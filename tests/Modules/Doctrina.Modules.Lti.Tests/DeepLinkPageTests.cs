using System.Security.Claims;
using System.Text.Json;
using Bunit;
using Doctrina.Core.Results;
using Doctrina.Modules.Content.Contracts;
using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Components.Pages;
using Doctrina.Modules.Lti.Domain;
using Doctrina.TestKit.Fakes;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Modules.Lti.Tests;

// Spec lti-deep-linking of change add-lti-deep-linking; task 5.1: the confirmation page calls the services and shows the
// outcome (the statuses for another user or an expired selection are tested against the host).

/// <summary>A content directory that allows managing every section and records what is added.</summary>
internal sealed class RecordingContentDirectory : IContentDirectory
{
    public List<(Guid Section, Guid Tool, IReadOnlyList<NewToolLink> Links)> Added { get; } = [];

    public Task<Result<ToolLinkInfo>> FindToolLinkAsync(ClaimsPrincipal user, Guid itemId, CancellationToken cancellationToken) =>
        Task.FromResult(Result<ToolLinkInfo>.Failure(Error.NotFound(IContentDirectory.ItemNotFoundCode, "Not found.")));

    public Task<Result<SectionInfo>> FindSectionForManagingAsync(ClaimsPrincipal user, Guid sectionId, CancellationToken cancellationToken) =>
        Task.FromResult(Result<SectionInfo>.Success(new SectionInfo(sectionId, new FakeDirectory().OfferingId)));

    public Task<Result> AddToolLinksAsync(ClaimsPrincipal user, Guid sectionId, Guid toolId, IReadOnlyList<NewToolLink> links, CancellationToken cancellationToken)
    {
        Added.Add((sectionId, toolId, links));
        return Task.FromResult(Result.Success());
    }
}

public sealed class DeDocentBevestigtDeSelectieOpDePagina : IAsyncDisposable
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private PageFixture? _f;

    private async Task<(Guid Section, Guid Tool, Guid Selection, RecordingContentDirectory Content)> SetUpAsync()
    {
        _f = await PageFixture.CreateAsync();
        var content = new RecordingContentDirectory();
        var services = _f.Context.Services;
        services.AddScoped<IContentDirectory>(_ => content);
        services.AddSingleton<Doctrina.Modules.Academic.Contracts.IAcademicDirectory>(new FakeDirectory());
        services.AddSingleton(new HttpClient());
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddScoped<JwksClient>();
        services.AddScoped<DeepLinkingService>();
        services.AddScoped<LaunchService>();

        var input = ToolTestData.Quiz() with { DeepLinkingUrl = "https://tool.example.org/dl", PublicKeysUrl = "https://tool.example.org/jwks" };
        var tool = (await new ToolService(_f.Db.CreateContext()).CreateAsync(input, Ct)).Value!;
        var section = Guid.NewGuid();
        using var scope = services.BuildServiceProvider().CreateScope();
        var subject = await scope.ServiceProvider.GetRequiredService<LaunchService>().SubjectAsync(new ClaimsPrincipal(), Ct); // the signed-in test user has no sub
        await using var db = _f.Db.CreateContext();
        var selection = new DeepLinkSelection
        {
            ToolId = tool.Id,
            SectionId = section,
            Subject = subject,
            ItemsJson = JsonSerializer.Serialize(new[] { new DeepLinkItem("Week 1 quiz", "https://tool.example.org/launch?quiz=1"), new DeepLinkItem("Week 2 quiz", "https://tool.example.org/launch?quiz=2") }),
            Expires = _f.Context.Services.GetRequiredService<TimeProvider>().GetUtcNow().AddMinutes(10),
        };
        db.Add(selection);
        await db.SaveChangesAsync(Ct);
        return (section, tool.Id, selection.Id, content);
    }

    [Fact]
    public async Task DeBevestigingspaginaToontDeTitels()
    {
        var (_, _, selection, content) = await SetUpAsync();

        var page = _f!.Context.Render<ConfirmDeepLinking>(p => p.Add(c => c.SelectionId, selection));

        page.WaitForElement("[data-testid=selection]", PageFixture.Wait);
        Assert.Equal(["Week 1 quiz", "Week 2 quiz"], page.FindAll("[data-testid=selection-item]").Select(e => e.TextContent.Trim()));
        Assert.Empty(content.Added); // nothing is added before the teacher confirms
    }

    [Fact]
    public async Task DocentVoegtDeSelectieToe()
    {
        var (section, tool, selection, content) = await SetUpAsync();
        var page = _f!.Context.Render<ConfirmDeepLinking>(p => p.Add(c => c.SelectionId, selection));
        page.WaitForElement("[data-testid=add-selection]", PageFixture.Wait);

        await page.InvokeAsync(() => page.Find("[data-testid=add-selection]").ClickAsync());

        var (addedSection, addedTool, links) = Assert.Single(content.Added);
        Assert.Equal((section, tool), (addedSection, addedTool));
        Assert.Equal(["Week 1 quiz", "Week 2 quiz"], links.Select(l => l.Title));
        Assert.Equal(["https://tool.example.org/launch?quiz=1", "https://tool.example.org/launch?quiz=2"], links.Select(l => l.LaunchUrl));
        Assert.Equal(0, await _f.Db.CreateContext().Selections.CountAsync(Ct)); // removed after adding
        Assert.EndsWith($"/lti/deep-linking/back/{section:D}", _f.Context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        if (_f is not null)
        {
            await _f.DisposeAsync();
        }
    }
}
