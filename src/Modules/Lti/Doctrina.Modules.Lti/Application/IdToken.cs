using System.Text.Json;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Contracts;
using Doctrina.Modules.Lti.Domain;

namespace Doctrina.Modules.Lti.Application;

/// <summary>The payload of the LTI resource link launch <c>id_token</c> (design D9 of add-lti-platform).</summary>
internal static class IdToken
{
    private const string Claim = "https://purl.imsglobal.org/spec/lti/claim/";
    private const string Membership = "http://purl.imsglobal.org/vocab/lis/v2/membership";

    public static string Payload(
        string issuer, LtiTool tool, string subject, string nonce, DateTimeOffset now, TimeSpan lifetime,
        ToolLinkInfo link, OfferingInfo offering, IReadOnlyList<OfferingRole> roles,
        string? name, string? givenName, string? familyName, string? email)
    {
        var claims = new Dictionary<string, object>
        {
            ["iss"] = issuer,
            ["aud"] = tool.ClientId,
            ["sub"] = subject,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.Add(lifetime).ToUnixTimeSeconds(),
            ["nonce"] = nonce,
            [Claim + "message_type"] = "LtiResourceLinkRequest",
            [Claim + "version"] = "1.3.0",
            [Claim + "deployment_id"] = tool.DeploymentId,
            [Claim + "target_link_uri"] = tool.LaunchUrl,
            [Claim + "resource_link"] = new { id = link.ItemId.ToString("D"), title = link.Title },
            [Claim + "context"] = new
            {
                id = offering.Id.ToString("D"),
                label = offering.ExternalId,
                title = offering.Name,
                type = new[] { "http://purl.imsglobal.org/vocab/lis/v2/course#CourseOffering" },
            },
            [Claim + "roles"] = Roles(roles),
            [Claim + "launch_presentation"] = new { document_target = "window" },
        };
        Add(claims, "name", name);
        Add(claims, "given_name", givenName);
        Add(claims, "family_name", familyName);
        Add(claims, "email", email);
        return JsonSerializer.Serialize(claims);
    }

    /// <summary>LIS roles; someone without an enrolment (an administrator) is an institution administrator.</summary>
    private static string[] Roles(IReadOnlyList<OfferingRole> roles) => roles.Count == 0
        ? ["http://purl.imsglobal.org/vocab/lis/v2/institution/person#Administrator"]
        : [.. roles.Select(r => r switch
            {
                OfferingRole.Student => Membership + "#Learner",
                OfferingRole.TeachingAssistant => Membership + "/Instructor#TeachingAssistant",
                _ => Membership + "#Instructor",
            }).Distinct()];

    private static void Add(Dictionary<string, object> claims, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            claims[key] = value;
        }
    }
}
