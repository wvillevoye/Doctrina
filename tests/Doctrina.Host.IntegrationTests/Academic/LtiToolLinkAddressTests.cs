using System.Net;
using Doctrina.Modules.Content.Application;
using Doctrina.Modules.Content.Contracts;
using Doctrina.TestKit.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

// Spec lti-deep-linking of change add-lti-deep-linking; task 6.1. Class = requirement, method = scenario.
namespace Doctrina.Host.IntegrationTests.Academic;

public sealed class EenToolLinkMetEigenAdresStartDatAdres(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private const string Address = "https://tool.example.org/launch?quiz=7";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The scenario with a second tool link, added like a deep linking selection, published, with the address the tool chose.</summary>
    private async Task<LaunchScenario> ScenarioAsync()
    {
        var s = await LaunchScenario.CreateAsync(factory, Ct);
        await s.Academic.ContentAsync(c => c.AddToolLinksAsync(s.Section.Id, s.Tool.Id, [new NewToolLink("Quiz 7", Address)], CancellationToken.None));
        using var scope = s.Academic.Host.Services.CreateScope();
        var section = Assert.Single(await scope.ServiceProvider.GetRequiredService<ContentService>().SectionsAsync(s.Offering, true, Ct));
        var link = section.Items.Single(i => i.Title == "Quiz 7");
        await s.Academic.ContentAsync(c => c.SetItemPublishedAsync(link.Id, true, CancellationToken.None));
        s.Link = link;
        return s;
    }

    [Fact]
    public async Task LaunchGebruiktHetGekozenAdres()
    {
        await using var s = await ScenarioAsync();

        using var response = await s.GetAsync(factory, StubUser.Student, $"/lti/launch/{s.Link.Id:D}", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(Address, QueryHelpers.ParseQuery(response.Headers.Location!.Query)["target_link_uri"]);
    }

    [Fact]
    public async Task TokenBevatHetGekozenAdres()
    {
        await using var s = await ScenarioAsync();
        using var session = await (await ToolSession.SignInAsync(s, factory, StubUser.Student, Ct)).StartAsync(Ct);

        using var response = await session.AuthorizeAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await LaunchForms.VerifiedAsync(s.Academic, LaunchForms.Field(await response.Content.ReadAsStringAsync(Ct), "id_token"), s.Tool.ClientId, Ct);
        Assert.Equal(Address, token.GetClaim("https://purl.imsglobal.org/spec/lti/claim/target_link_uri").Value);
    }

    [Fact]
    public async Task EenToolLinkZonderEigenAdresBlijftDeLaunchUrlGebruiken()
    {
        await using var s = await LaunchScenario.CreateAsync(factory, Ct);

        using var response = await s.GetAsync(factory, StubUser.Student, $"/lti/launch/{s.Link.Id:D}", Ct);

        Assert.Equal("https://tool.example.org/launch", QueryHelpers.ParseQuery(response.Headers.Location!.Query)["target_link_uri"]);
    }
}
