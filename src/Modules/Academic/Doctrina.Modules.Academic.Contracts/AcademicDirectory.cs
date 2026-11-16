using System.Security.Claims;
using Doctrina.Core.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Modules.Academic.Contracts;

/// <summary>An active course offering (design D1 of add-groups).</summary>
public sealed record OfferingInfo(Guid Id, string ExternalId, string Name, string CourseName, string SessionName);

/// <summary>The role of a person in a course offering (OOAPI association role).</summary>
public enum OfferingRole { Student, Lecturer, TeachingAssistant, Coordinator }

/// <summary>An active enrolment of an active person in a course offering.</summary>
public sealed record OfferingMember(Guid PersonId, string ExternalId, string Name, OfferingRole Role);

/// <summary>Name and e-mail of a person, for modules that pass them on with consent (add-lti-platform, design D9).</summary>
public sealed record PersonInfo(string Name, string GivenName, string FamilyName, string? Mail);

/// <summary>An active person in an offering with all their active roles and their name and e-mail (design D4 of add-lti-nrps).</summary>
public sealed record OfferingMemberDetail(Guid PersonId, IReadOnlyList<OfferingRole> Roles, string Name, string GivenName, string FamilyName, string? Mail);

/// <summary>A page of members; <see cref="Next"/> is the person id to continue after, or <see langword="null"/> on the last page.</summary>
public sealed record OfferingMemberPage(IReadOnlyList<OfferingMemberDetail> Items, Guid? Next);

/// <summary>Read access to academic data for other modules. Never exposes Academic's entities or tables.</summary>
public interface IAcademicDirectory
{
    /// <summary>The offering, or <c>Academic.CourseOffering.NotFound</c> if it doesn't exist or is to be deleted.</summary>
    Task<Result<OfferingInfo>> FindOfferingAsync(Guid offeringId, CancellationToken cancellationToken);

    /// <summary>The person of the signed-in user (linked via personId, then issuer + oid), or <see langword="null"/>.</summary>
    Task<Guid?> CurrentPersonIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// The other active offerings of the same course as <paramref name="offeringId"/>, newest session first (design D2 of
    /// add-course-templates); empty for an unknown or deleted offering.
    /// </summary>
    Task<IReadOnlyList<OfferingInfo>> SiblingOfferingsAsync(Guid offeringId, CancellationToken cancellationToken);

    /// <summary>Name and e-mail of an active person, or <see langword="null"/>.</summary>
    Task<PersonInfo?> FindPersonAsync(Guid personId, CancellationToken cancellationToken);

    /// <summary>Active enrolments of active persons in the offering, ordered by name.</summary>
    Task<IReadOnlyList<OfferingMember>> MembersAsync(Guid offeringId, CancellationToken cancellationToken);

    /// <summary>
    /// One page of persons with an active enrolment in the offering, ordered by person id, each once with all their active roles.
    /// With <paramref name="roles"/> only persons that have one of those roles; <paramref name="after"/> is the last person id
    /// of the previous page. Needs one database round trip per page.
    /// </summary>
    Task<OfferingMemberPage> MemberPageAsync(Guid offeringId, IReadOnlyCollection<OfferingRole>? roles, Guid? after, int take, CancellationToken cancellationToken);
}

public static class AcademicDirectoryExtensions
{
    /// <summary>
    /// The roles of the signed-in user in the offering; empty without a person or an active enrolment (design D4 of
    /// add-content). The shared base for "lecturer of this offering" checks in other modules.
    /// </summary>
    public static async Task<IReadOnlyList<OfferingRole>> CurrentRolesAsync(
        this IAcademicDirectory directory, ClaimsPrincipal user, Guid offeringId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var personId = await directory.CurrentPersonIdAsync(user, cancellationToken);
        return personId is null
            ? []
            : [.. (await directory.MembersAsync(offeringId, cancellationToken)).Where(m => m.PersonId == personId).Select(m => m.Role)];
    }
}

/// <summary>
/// A component shown on the page of a course offering (<c>/courses/{offering}</c>). The component gets the parameter
/// <c>[Parameter] public Guid OfferingId { get; set; }</c>.
/// </summary>
public sealed record OfferingPageSection(Type Component, int Order);

public static class OfferingPageSectionExtensions
{
    /// <summary>Registers <typeparamref name="TComponent"/> as a section of the offering page; call from <c>ConfigureServices</c>.</summary>
    public static IServiceCollection AddOfferingPageSection<TComponent>(this IServiceCollection services, int order) =>
        services.AddSingleton(new OfferingPageSection(typeof(TComponent), order));
}
