using System.Security.Claims;
using Bunit;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Groups.Application;
using Doctrina.Modules.Groups.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Modules.Groups.Tests;

// Task 4.2: the group management page calls the service and shows the outcome (access is tested against the host).
public sealed class HetGroepsbeheerOpDePagina : IAsyncDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10); // LocalDB round trips take longer than bUnit's default of 1 s.

    private readonly BunitContext _context = new();
    private Fixture? _f;

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task GroupSetAanmakenEnWillekeurigVerdelen()
    {
        var page = await RenderAsync(students: 5);

        await SubmitAsync(page, "[data-testid=new-set]", ("input[type=text]", "Project teams"), ("input[type=checkbox]", true), ("input[type=number]", ""));
        Assert.Single(page.FindAll("[data-testid=group-set]"));
        var setId = (await _f!.Service().SetsAsync(_f.Directory.OfferingId, Ct)).Single().Id;

        await SubmitAsync(page, "[data-testid=distribute]", ("select", setId.ToString("D")), ("input", "2"));

        Assert.Equal("Students distributed.", page.Find("[data-testid=groups-message] p").TextContent.Trim());
        Assert.Equal(["Group 1 (3)", "Group 2 (2)"], page.FindAll("[data-testid=group-name]").Select(e => e.TextContent));
        Assert.Contains("Visible to students", page.Find("[data-testid=group-set]").TextContent, StringComparison.Ordinal);
        Assert.Equal("Not in a group: nobody", page.Find("[data-testid=unassigned]").TextContent);
    }

    [Fact]
    public async Task IndelenEnMeldingBijEenVolleGroep()
    {
        Guid groupId = default;
        var page = await RenderAsync(students: 2, async f =>
        {
            var set = await Fixture.Ok(f.Service().CreateSetAsync(f.Directory.OfferingId, "Pairs", true, 1, Ct));
            groupId = (await Fixture.Ok(f.Service().CreateGroupAsync(set.Id, "Pair 1", Ct))).Id;
        });
        var (first, second) = (_f!.Directory.Members[1], _f.Directory.Members[2]);

        await SubmitAsync(page, "[data-testid=assign]", ("select", first.PersonId.ToString("D")), ("select#last", groupId.ToString("D")));
        Assert.Contains("is now in Pair 1", page.Find("[data-testid=groups-message] p").TextContent, StringComparison.Ordinal);

        await SubmitAsync(page, "[data-testid=assign]", ("select", second.PersonId.ToString("D")), ("select#last", groupId.ToString("D")));

        Assert.Contains("Pair 1 is full", page.Find("[data-testid=result-error]").TextContent, StringComparison.Ordinal);
        Assert.Equal(["Pair 1 (1)"], page.FindAll("[data-testid=group-name]").Select(e => e.TextContent));
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        if (_f is not null)
        {
            await _f.DisposeAsync();
        }
    }

    private async Task<IRenderedComponent<ManageGroups>> RenderAsync(int students, Func<Fixture, Task>? setup = null)
    {
        _f = await Fixture.CreateAsync();
        var lecturer = _f.Directory.Add("P7000002", OfferingRole.Lecturer);
        _f.Directory.AddStudents(students);
        if (setup is not null)
        {
            await setup(_f);
        }

        _context.JSInterop.Mode = JSRuntimeMode.Loose;
        _context.AddAuthorization().SetAuthorized("Dirk Docent").SetClaims(new Claim(FakeDirectory.PersonClaim, lecturer.PersonId.ToString()));
        _context.Services.AddSingleton<IAcademicDirectory>(_f.Directory);
        _context.Services.AddScoped(_ => _f.Db.CreateContext());
        _context.Services.AddScoped<GroupService>();
        _context.Services.AddScoped<GroupsAccess>();
        return _context.Render<ManageGroups>(p => p.Add(c => c.OfferingId, _f.Directory.OfferingId));
    }

    /// <summary>
    /// Fills a form and submits it, awaiting the (async) event handlers, so the outcome is rendered when this returns.
    /// A selector "select#last" means the last select of the form; null values are skipped.
    /// </summary>
    private static async Task SubmitAsync(IRenderedComponent<ManageGroups> page, string formSelector, params (string Selector, object? Value)[] fields)
    {
        page.WaitForElement(formSelector, Wait); // the page loads its data asynchronously on the first render
        foreach (var (selector, value) in fields)
        {
            if (value is null)
            {
                continue;
            }

            // Find the field again every time (each change re-renders the form), inside the renderer's dispatcher so
            // no render can happen between finding and changing.
            await page.InvokeAsync(() =>
            {
                var form = page.Find(formSelector);
                var element = selector == "select#last" ? form.QuerySelectorAll("select")[^1] : form.QuerySelector(selector)!;
                return element.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = value });
            });
        }

        await page.InvokeAsync(() => page.Find(formSelector).SubmitAsync());
    }
}
