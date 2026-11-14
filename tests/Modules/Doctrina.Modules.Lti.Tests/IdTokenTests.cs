using System.Text.Json;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Contracts;
using Doctrina.Modules.Lti.Application;
using Doctrina.Modules.Lti.Domain;

// Spec lti-launch (requirement "Inhoud van de id_token"), roles that the web tests can't reach.
namespace Doctrina.Modules.Lti.Tests;

public sealed class InhoudVanDeIdTokenRollen
{
    private const string Membership = "http://purl.imsglobal.org/vocab/lis/v2/membership";

    private static string[] Roles(params OfferingRole[] roles)
    {
        var tool = new LtiTool { ExternalId = "e", Source = "Doctrina", Name = "T", ClientId = "c", DeploymentId = "d", LoginUrl = "https://t/l", LaunchUrl = "https://t/x", RedirectUris = "https://t/x" };
        var json = IdToken.Payload("https://lms", tool, "sub", "n", DateTimeOffset.UnixEpoch, TimeSpan.FromMinutes(5),
            new ToolLinkInfo(Guid.Empty, Guid.Empty, "Quiz", Guid.Empty), new OfferingInfo(Guid.Empty, "X", "X", "X", "X"), roles, null, null, null, null);
        return JsonDocument.Parse(json).RootElement.GetProperty("https://purl.imsglobal.org/spec/lti/claim/roles").EnumerateArray().Select(r => r.GetString()!).ToArray();
    }

    [Fact]
    public void TeachingAssistantKrijgtDeRolTeachingAssistant() => Assert.Equal([Membership + "/Instructor#TeachingAssistant"], Roles(OfferingRole.TeachingAssistant));

    [Fact]
    public void CoordinatorIsInstructor() => Assert.Equal([Membership + "#Instructor"], Roles(OfferingRole.Coordinator, OfferingRole.Lecturer));

    [Fact]
    public void BeheerderZonderInschrijvingKrijgtEenInstitutierol() =>
        Assert.Equal(["http://purl.imsglobal.org/vocab/lis/v2/institution/person#Administrator"], Roles());
}
