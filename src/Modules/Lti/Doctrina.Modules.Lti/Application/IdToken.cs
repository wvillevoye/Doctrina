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

    private const string DeepLinkingClaim = "https://purl.imsglobal.org/spec/lti-dl/claim/";

    public static string Payload(
        string issuer, LtiTool tool, string subject, string nonce, DateTimeOffset now, TimeSpan lifetime,
        ToolLinkInfo link, OfferingInfo offering, IReadOnlyList<OfferingRole> roles,
        string? name, string? givenName, string? familyName, string? email, Guid? lineItemId = null)
    {
        var claims = Common(issuer, tool, subject, nonce, now, lifetime, "LtiResourceLinkRequest", link.LaunchUrl ?? tool.LaunchUrl, offering, roles, name, givenName, familyName, email);
        claims[Claim + "resource_link"] = new { id = link.ItemId.ToString("D"), title = link.Title };
        if (tool.GradeServices)
        {
            // Assignment and Grade Services endpoint claim (design D8 of add-lti-ags).
            var lineItems = $"{issuer.TrimEnd('/')}/lti/ags/{offering.Id:D}/lineitems";
            claims["https://purl.imsglobal.org/spec/lti-ags/claim/endpoint"] = lineItemId is { } id
                ? new { scope = LtiScopes.AllowedFor(tool), lineitems = lineItems, lineitem = $"{lineItems}/{id:D}" }
                : (object)new { scope = LtiScopes.AllowedFor(tool), lineitems = lineItems };
        }

        if (tool.MembershipServices)
        {
            // Names and Role Provisioning Services claim (design D8 of add-lti-nrps).
            claims["https://purl.imsglobal.org/spec/lti-nrps/claim/namesroleservice"] = new
            {
                context_memberships_url = $"{issuer.TrimEnd('/')}/lti/nrps/{offering.Id:D}/memberships",
                service_versions = new[] { "2.0" },
            };
        }

        return JsonSerializer.Serialize(claims);
    }

    /// <summary>The payload of the <c>LtiDeepLinkingRequest</c> (design D2 of add-lti-deep-linking): no resource link, but the settings of the response.</summary>
    public static string DeepLinkingPayload(
        string issuer, LtiTool tool, string subject, string nonce, DateTimeOffset now, TimeSpan lifetime, string data,
        OfferingInfo offering, IReadOnlyList<OfferingRole> roles,
        string? name, string? givenName, string? familyName, string? email)
    {
        var claims = Common(issuer, tool, subject, nonce, now, lifetime, "LtiDeepLinkingRequest", tool.DeepLinkingUrl!, offering, roles, name, givenName, familyName, email);
        claims[DeepLinkingClaim + "deep_linking_settings"] = new
        {
            deep_link_return_url = ReturnUrl(issuer),
            accept_types = new[] { "ltiResourceLink" },
            accept_presentation_document_targets = new[] { "window" },
            accept_multiple = true,
            auto_create = false,
            data,
        };
        return JsonSerializer.Serialize(claims);
    }

    /// <summary>Where a tool posts its deep linking response.</summary>
    public static string ReturnUrl(string issuer) => issuer.TrimEnd('/') + "/lti/deep-linking/return";

    private static Dictionary<string, object> Common(
        string issuer, LtiTool tool, string subject, string nonce, DateTimeOffset now, TimeSpan lifetime,
        string messageType, string targetLinkUri, OfferingInfo offering, IReadOnlyList<OfferingRole> roles,
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
            [Claim + "message_type"] = messageType,
            [Claim + "version"] = "1.3.0",
            [Claim + "deployment_id"] = tool.DeploymentId,
            [Claim + "target_link_uri"] = targetLinkUri,
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
        return claims;
    }

    /// <summary>LIS roles; someone without an enrolment (an administrator) is an institution administrator.</summary>
    private static string[] Roles(IReadOnlyList<OfferingRole> roles) => roles.Count == 0
        ? ["http://purl.imsglobal.org/vocab/lis/v2/institution/person#Administrator"]
        : [.. roles.Select(RoleUri).Distinct()];

    /// <summary>The LIS role of an offering role; shared with the NRPS membership list so a launch and a list agree.</summary>
    public static string RoleUri(OfferingRole role) => role switch
    {
        OfferingRole.Student => Learner,
        OfferingRole.TeachingAssistant => TeachingAssistant,
        _ => Instructor,
    };

    public const string Learner = Membership + "#Learner";
    public const string Instructor = Membership + "#Instructor";
    public const string TeachingAssistant = Membership + "/Instructor#TeachingAssistant";

    private static void Add(Dictionary<string, object> claims, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            claims[key] = value;
        }
    }
}
