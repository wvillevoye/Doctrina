using System.Net;
using Doctrina.Host.IntegrationTests.DataPersistence;
using Doctrina.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;

// Spec ui-components, requirement "De stijlgids toont elk component" (add-design-system, taak 7.1, design D11).
namespace Doctrina.Host.IntegrationTests.HostUi;

public sealed class DeStijlgidsToontElkComponent(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    /// <summary>Alle publieke componenten van Doctrina.UI (Dc*), zodat een nieuw component niet ongemerkt ontbreekt.</summary>
    private static IEnumerable<Type> PublicComponents =>
        typeof(DcButton).Assembly.GetExportedTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && !t.IsAbstract && t.Name.StartsWith("Dc", StringComparison.Ordinal));

    [Fact]
    public async Task ElkComponentStaatInDeStijlgids()
    {
        using var host = factory.InEnvironment(Environments.Development);
        using var browser = host.CreateBrowser();

        using var response = await browser.GetAsync(new Uri("/style-guide", UriKind.Relative), Ct);
        var page = Html.Parse(await response.Content.ReadAsStringAsync(Ct));
        var sections = page.QuerySelectorAll("main section[id^='dc-']").Select(s => s.Id!).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Style guide · Doctrina", page.Title);
        Assert.Equal(19, PublicComponents.Count());
        Assert.All(PublicComponents, c => Assert.True(sections.Contains(SectionId(c)), $"Sectie '{SectionId(c)}' voor {c.Name} ontbreekt in de stijlgids."));
        // Elk voorbeeld staat in een lichte en een donkere container.
        Assert.All(page.QuerySelectorAll("main section[id^='dc-']"), s =>
        {
            Assert.NotNull(s.QuerySelector("[data-dc-theme=light]"));
            Assert.NotNull(s.QuerySelector("[data-dc-theme=dark]"));
        });
        Assert.Equal(page.QuerySelectorAll("[id]").Length, page.QuerySelectorAll("[id]").Select(e => e.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task StijlgidsStandaardVerborgenInProductie()
    {
        using var host = factory.InEnvironment(Environments.Production);
        using var browser = host.CreateBrowser();

        using var response = await browser.GetAsync(new Uri("/style-guide", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StijlgidsAanTeZettenBuitenDevelopment()
    {
        using var host = factory.InEnvironment(Environments.Production, ("Ui:StyleGuide:Enabled", "true"));
        using var browser = host.CreateBrowser();

        using var response = await browser.GetAsync(new Uri("/style-guide", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary><c>DcPageHeader</c> → <c>dc-page-header</c>; <c>DcTable`1</c> → <c>dc-table</c> (design D11).</summary>
    private static string SectionId(Type component)
    {
        var name = component.Name.Split('`')[0][2..];
        return "dc-" + string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }
}
