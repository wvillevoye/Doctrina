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

    public List<OfferingMember> Members { get; } = [];

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
        Task.FromResult(offeringId == OfferingId
            ? Result<OfferingInfo>.Success(new OfferingInfo(OfferingId, "BIO101-2026-P1", "Cell biology (2026-P1)", "Cell biology", "Period 1"))
            : Result<OfferingInfo>.Failure(Error.NotFound("Academic.CourseOffering.NotFound", "Not found.")));

    public Task<Guid?> CurrentPersonIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
        Task.FromResult(Guid.TryParse(user.FindFirst(PersonClaim)?.Value, out var id) ? id : (Guid?)null);

    public Task<IReadOnlyList<OfferingMember>> MembersAsync(Guid offeringId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OfferingMember>>(offeringId == OfferingId ? [.. Members] : []);
}
