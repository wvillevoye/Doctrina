using System.Security.Claims;
using Doctrina.Core.Results;
using Doctrina.Infrastructure.Authentication;
using Doctrina.Modules.Academic.Contracts;

namespace Doctrina.TestKit.Fakes;

/// <summary>
/// In-memory <see cref="IAcademicDirectory"/>: one offering with members, so module tests don't need the Academic
/// database (add-groups task 2.2, shared since add-content task 1.2). The "current person" is read from the claim
/// <c>personGuid</c>.
/// </summary>
public sealed class FakeDirectory : IAcademicDirectory
{
    public const string PersonClaim = "personGuid";

    public Guid OfferingId { get; } = new("11111111-0000-0000-0000-000000000001");

    /// <summary>A second offering of the same course (add-course-templates): the usual source of a copy.</summary>
    public Guid SourceOfferingId { get; } = new("11111111-0000-0000-0000-000000000002");

    /// <summary>An offering of another course, so it has no siblings.</summary>
    public Guid OtherCourseOfferingId { get; } = new("11111111-0000-0000-0000-000000000003");

    /// <summary><see cref="OfferingId"/> and <see cref="SourceOfferingId"/> are offerings of the same course; set to false to make them unrelated.</summary>
    public bool HasSiblings { get; set; } = true;

    public List<OfferingMember> Members { get; } = [];

    /// <summary>The members of <see cref="SourceOfferingId"/>; add an existing member to give them a role there too.</summary>
    public List<OfferingMember> SourceMembers { get; } = [];

    public OfferingMember Add(string externalId, OfferingRole role = OfferingRole.Student)
    {
        var member = new OfferingMember(new Guid($"22222222-0000-0000-0000-{Members.Count + 1:D12}"), externalId, externalId + " Name", role);
        Members.Add(member);
        return member;
    }

    public List<OfferingMember> AddStudents(int count) => [.. Enumerable.Range(1, count).Select(i => Add($"I6{i:D6}"))];

    public static ClaimsPrincipal User(Guid? personId, params string[] permissions) =>
        new(new ClaimsIdentity(
            [.. personId is { } id ? [new Claim(PersonClaim, id.ToString())] : Array.Empty<Claim>(),
             .. permissions.Select(p => new Claim(DoctrinaClaimTypes.Permission, p))],
            "test"));

    public Task<Result<OfferingInfo>> FindOfferingAsync(Guid offeringId, CancellationToken cancellationToken) =>
        Task.FromResult(Info(offeringId) is { } info
            ? Result<OfferingInfo>.Success(info)
            : Result<OfferingInfo>.Failure(Error.NotFound("Academic.CourseOffering.NotFound", "Not found.")));

    private OfferingInfo? Info(Guid id) =>
        id == OfferingId ? new OfferingInfo(OfferingId, "BIO101-2026-P1", "Cell biology (2026-P1)", "Cell biology", "Period 1")
        : id == SourceOfferingId ? new OfferingInfo(SourceOfferingId, "BIO101-2025-P1", "Cell biology (2025-P1)", "Cell biology", "Period 1")
        : id == OtherCourseOfferingId ? new OfferingInfo(OtherCourseOfferingId, "WIS201-2026-P1", "Algebra (2026-P1)", "Algebra", "Period 1")
        : null;

    public Task<IReadOnlyList<OfferingInfo>> SiblingOfferingsAsync(Guid offeringId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OfferingInfo>>(
            !HasSiblings ? []
            : offeringId == OfferingId ? [Info(SourceOfferingId)!]
            : offeringId == SourceOfferingId ? [Info(OfferingId)!]
            : []);

    public Task<Guid?> CurrentPersonIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
        Task.FromResult(Guid.TryParse(user.FindFirst(PersonClaim)?.Value, out var id) ? id : (Guid?)null);

    public Task<PersonInfo?> FindPersonAsync(Guid personId, CancellationToken cancellationToken) =>
        Task.FromResult(Members.FirstOrDefault(m => m.PersonId == personId) is { } m
            ? new PersonInfo(m.Name, m.ExternalId, "Name", m.ExternalId + "@example.test")
            : null);

    public Task<IReadOnlyList<OfferingMember>> MembersAsync(Guid offeringId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OfferingMember>>(
            offeringId == OfferingId ? [.. Members] : offeringId == SourceOfferingId ? [.. SourceMembers] : []);

    public Task<OfferingMemberPage> MemberPageAsync(Guid offeringId, IReadOnlyCollection<OfferingRole>? roles, Guid? after, int take, CancellationToken cancellationToken)
    {
        var people = (offeringId == OfferingId ? Members : offeringId == SourceOfferingId ? SourceMembers : [])
            .GroupBy(m => m.PersonId)
            .Where(g => (roles is null || g.Any(m => roles.Contains(m.Role))) && (after is null || g.Key.CompareTo(after.Value) > 0))
            .OrderBy(g => g.Key).Take(take + 1).ToList();
        var items = people.Take(take).Select(g => new OfferingMemberDetail(
            g.Key, [.. g.Select(m => m.Role).Distinct().Order()], g.First().Name, g.First().ExternalId, "Name", g.First().ExternalId + "@example.test")).ToList();
        return Task.FromResult(new OfferingMemberPage(items, people.Count > take ? items[^1].PersonId : null));
    }
}
