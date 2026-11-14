using Bunit;
using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace Doctrina.Modules.Lti.Tests;

// Specs lti-tool-registration of change add-lti-platform; task 3.4: the page calls the services and shows the outcome
// (access and the platform details are tested against the host).

internal sealed class PageFixture : IAsyncDisposable
{
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(10); // LocalDB round trips take longer than bUnit's default of 1 s.

    private PageFixture(LtiDatabase db) => Db = db;

    public LtiDatabase Db { get; }

    public BunitContext Context { get; } = new();

    public static async Task<PageFixture> CreateAsync()
    {
        var f = new PageFixture(await LtiDatabase.CreateAsync(Xunit.TestContext.Current.CancellationToken));
        f.Context.JSInterop.Mode = JSRuntimeMode.Loose;
        f.Context.AddAuthorization().SetAuthorized("Lotte Beheer");
        f.Context.Services.AddScoped(_ => f.Db.CreateContext());
        f.Context.Services.AddSingleton<IDataProtectionProvider>(DataProtectionProvider.Create("lti-page-tests"));
        f.Context.Services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        f.Context.Services.AddSingleton(Options.Create(new LtiOptions()));
        f.Context.Services.AddScoped<ToolService>();
        f.Context.Services.AddScoped<KeyService>();
        return f;
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await Db.DisposeAsync();
    }
}

public sealed class BeheerdersRegistrerenLtiToolsOpDePagina : IAsyncDisposable
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private PageFixture? _f;

    [Fact]
    public async Task ToolRegistrerenViaDePagina()
    {
        _f = await PageFixture.CreateAsync();
        var page = Render();

        await FillAndSubmitAsync(page, "Quiz tool");

        Assert.Contains("Tool “Quiz tool” registered.", page.Find("[data-testid=lti-message] p").TextContent, StringComparison.Ordinal);
        var tool = page.Find("[data-testid=tool]");
        Assert.Contains("Quiz tool", tool.TextContent, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(tool.QuerySelector("[data-testid=client-id]")!.TextContent));
        Assert.False(string.IsNullOrWhiteSpace(tool.QuerySelector("[data-testid=deployment-id]")!.TextContent));
        Assert.Contains("Nothing", tool.TextContent, StringComparison.Ordinal); // no personal data shared by default
    }

    [Fact]
    public async Task EenToolZonderHttpsToontDeMelding()
    {
        _f = await PageFixture.CreateAsync();
        var page = Render();

        await FillAndSubmitAsync(page, "Quiz tool", loginUrl: "http://tool.example.org/login");

        Assert.Contains("https://", page.Find("[data-testid=result-error]").TextContent, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("[data-testid=tool]"));
    }

    [Fact]
    public async Task ToolWijzigenEnVerwijderen()
    {
        _f = await PageFixture.CreateAsync();
        var tool = (await new ToolService(_f.Db.CreateContext()).CreateAsync(ToolTestData.Quiz(), Ct)).Value!;
        var page = Render();
        page.WaitForElement("[data-testid=tool]", PageFixture.Wait);

        await page.InvokeAsync(() => page.Find("[data-testid=edit-tool]").ClickAsync());
        await page.InvokeAsync(() => page.Find("[data-testid=tool-form] input[type=text]").ChangeAsync(new ChangeEventArgs { Value = "Renamed" }));
        await page.InvokeAsync(() => page.Find("[data-testid=tool-form]").SubmitAsync());

        Assert.Equal("Tool “Renamed” saved.", page.Find("[data-testid=lti-message] p").TextContent.Trim());
        Assert.Equal(tool.ClientId, page.Find("[data-testid=client-id]").TextContent);
        Assert.Contains("Renamed", page.Find("[data-testid=tool]").TextContent, StringComparison.Ordinal);

        await page.InvokeAsync(() => page.Find("[data-testid=delete-tool]").ClickAsync());
        await page.InvokeAsync(() => page.Find("[data-testid=confirm-delete]").ClickAsync());

        Assert.Empty(page.FindAll("[data-testid=tool]"));
        Assert.Equal("Tool “Renamed” deleted.", page.Find("[data-testid=lti-message] p").TextContent.Trim());
    }

    [Fact]
    public async Task SleutelRoterenViaDePagina()
    {
        _f = await PageFixture.CreateAsync();
        var page = Render();
        page.WaitForState(() => page.Find("[data-testid=active-kid]").TextContent.Trim().Length > 0, PageFixture.Wait); // the key is made while loading
        var before = page.Find("[data-testid=active-kid]").TextContent;

        await page.InvokeAsync(() => page.Find("[data-testid=rotate-key]").ClickAsync());

        Assert.NotEqual(before, page.Find("[data-testid=active-kid]").TextContent);
        Assert.Contains("A new signing key is active.", page.Find("[data-testid=lti-message] p").TextContent, StringComparison.Ordinal);
        Assert.Equal(2, (await _f.Db.CreateContext().Keys.CountAsync(Ct)));
    }

    public async ValueTask DisposeAsync()
    {
        if (_f is not null)
        {
            await _f.DisposeAsync();
        }
    }

    private IRenderedComponent<ManageTools> Render() => _f!.Context.Render<ManageTools>();

    private static async Task FillAndSubmitAsync(IRenderedComponent<ManageTools> page, string name, string loginUrl = "https://tool.example.org/login")
    {
        page.WaitForElement("[data-testid=tool-form]", PageFixture.Wait); // the page loads its data asynchronously
        await page.InvokeAsync(() => page.Find("[data-testid=tool-form] input[type=text]").ChangeAsync(new ChangeEventArgs { Value = name }));
        var urls = page.FindAll("[data-testid=tool-form] input[type=url]");
        await page.InvokeAsync(() => urls[0].ChangeAsync(new ChangeEventArgs { Value = loginUrl }));
        await page.InvokeAsync(() => page.FindAll("[data-testid=tool-form] input[type=url]")[1].ChangeAsync(new ChangeEventArgs { Value = "https://tool.example.org/launch" }));
        await page.InvokeAsync(() => page.Find("[data-testid=tool-form] textarea").ChangeAsync(new ChangeEventArgs { Value = "https://tool.example.org/launch" }));
        await page.InvokeAsync(() => page.Find("[data-testid=tool-form]").SubmitAsync());
    }
}
