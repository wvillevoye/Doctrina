using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Content.Contracts;
using Doctrina.Modules.Lti.Domain;

namespace Doctrina.Modules.Lti.Application;

/// <summary>A participant as a tool sees it; name and e-mail are <see langword="null"/> unless the tool may get them (design D5).</summary>
internal sealed record Member(string UserId, IReadOnlyList<string> Roles, string? Name, string? GivenName, string? FamilyName, string? Email);

/// <summary>A page of the participants of an offering.</summary>
internal sealed record MemberList(OfferingInfo Offering, Page<Member> Members);

/// <summary>
/// The participants of an offering for a tool with membership services (design D2 to D6 of add-lti-nrps): only offerings where
/// the tool has a tool link, one row per person with all their LIS roles, and name and e-mail only as far as the tool is set up
/// to receive them. Nothing of the participants is logged.
/// </summary>
internal sealed class MembershipService(IContentDirectory content, IAcademicDirectory academic)
{
    public async Task<Result<MemberList>> ListAsync(LtiTool tool, Guid offeringId, string? role, string? resourceLinkId, int? limit, Guid? after, CancellationToken ct)
    {
        var take = GradeService.Limit(limit);
        if (take is null)
        {
            return Invalid($"Use a limit between 1 and {GradeService.MaxLimit}.");
        }

        var roles = RolesOf(role);
        if (roles is null)
        {
            return Invalid("Use the role Learner or Instructor, or a full LIS role URI.");
        }

        if (resourceLinkId is not null && !Guid.TryParse(resourceLinkId, out _))
        {
            return Invalid("The resource link id is not valid.");
        }

        var links = await content.ToolLinksOfToolAsync(offeringId, tool.Id, ct);
        if (links.Count == 0)
        {
            return Error.NotFound(LtiErrors.NrpsContextNotFound, "The course offering was not found for this tool.");
        }

        // The list is the same for every resource link of the offering (design D6); the id is only checked.
        if (resourceLinkId is not null && !links.Any(l => l.ItemId == Guid.Parse(resourceLinkId)))
        {
            return Error.NotFound(LtiErrors.NrpsResourceLinkNotFound, "The resource link was not found in this course offering.");
        }

        var offering = await academic.FindOfferingAsync(offeringId, ct);
        if (offering.Error is not null)
        {
            return Error.NotFound(LtiErrors.NrpsContextNotFound, "The course offering was not found for this tool.");
        }

        var page = await academic.MemberPageAsync(offeringId, roles.Count == 0 ? null : roles, after, take.Value, ct);
        var members = page.Items.Select(m => new Member(
            m.PersonId.ToString("N"),
            [.. m.Roles.Select(IdToken.RoleUri).Distinct()],
            tool.SendName ? m.Name : null,
            tool.SendName ? m.GivenName : null,
            tool.SendName ? m.FamilyName : null,
            tool.SendEmail ? m.Mail : null)).ToList();
        return new MemberList(offering.Value!, new Page<Member>(members, page.Next));
    }

    /// <summary>The offering roles a <c>role</c> filter stands for: none (no filter), or <see langword="null"/> if it is not a known role.</summary>
    private static List<OfferingRole>? RolesOf(string? role) => role switch
    {
        null => [],
        "Learner" or IdToken.Learner => [OfferingRole.Student],
        "Instructor" or IdToken.Instructor => [OfferingRole.Lecturer, OfferingRole.Coordinator],
        IdToken.TeachingAssistant => [OfferingRole.TeachingAssistant],
        _ => null,
    };

    private static Error Invalid(string message) => Error.Validation(LtiErrors.NrpsInvalidRequest, message);
}
