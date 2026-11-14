using System.Security.Claims;
using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Academic.Domain;
using Doctrina.Modules.Academic.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Academic.Application;

/// <summary>Implementation of the public contract <see cref="IAcademicDirectory"/> (design D1 of add-groups).</summary>
internal sealed class AcademicDirectory(AcademicDbContext db, CurrentPerson currentPerson) : IAcademicDirectory, IDisposable
{
    public const string OfferingNotFoundCode = "Academic.CourseOffering.NotFound";

    // Page sections call the directory at the same time and share this scoped DbContext, which allows one operation at a time.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public void Dispose() => _gate.Dispose();

    private async Task<T> SerializedAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await operation(); }
        finally { _gate.Release(); }
    }

    public Task<Result<OfferingInfo>> FindOfferingAsync(Guid offeringId, CancellationToken cancellationToken) =>
        SerializedAsync(() => FindOfferingCoreAsync(offeringId, cancellationToken), cancellationToken);

    private async Task<Result<OfferingInfo>> FindOfferingCoreAsync(Guid offeringId, CancellationToken cancellationToken)
    {
        var offering = await db.CourseOfferings.AsNoTracking()
            .Where(o => o.Id == offeringId && o.Status == EntityStatus.Active)
            .Select(o => new OfferingInfo(o.Id, o.ExternalId, o.Name, o.Course.Name, o.AcademicSession.Name))
            .SingleOrDefaultAsync(cancellationToken);
        return offering is null
            ? Error.NotFound(OfferingNotFoundCode, $"Course offering {offeringId} doesn't exist.")
            : offering;
    }

    public Task<IReadOnlyList<OfferingInfo>> SiblingOfferingsAsync(Guid offeringId, CancellationToken cancellationToken) =>
        SerializedAsync<IReadOnlyList<OfferingInfo>>(async () =>
        {
            var rows = await db.CourseOfferings.AsNoTracking()
                .Where(o => o.Id != offeringId && o.Status == EntityStatus.Active
                    && o.Course.Offerings.Any(x => x.Id == offeringId && x.Status == EntityStatus.Active))
                .Select(o => new { Info = new OfferingInfo(o.Id, o.ExternalId, o.Name, o.Course.Name, o.AcademicSession.Name), o.AcademicSession.StartDate })
                .ToListAsync(cancellationToken);
            return [.. rows.OrderByDescending(r => r.StartDate).ThenBy(r => r.Info.Name, StringComparer.CurrentCulture).Select(r => r.Info)];
        }, cancellationToken);

    public Task<Guid?> CurrentPersonIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
        SerializedAsync(async () => (await currentPerson.FindAsync(user, cancellationToken))?.Id, cancellationToken);

    public Task<PersonInfo?> FindPersonAsync(Guid personId, CancellationToken cancellationToken) =>
        SerializedAsync(async () =>
        {
            var p = await db.Persons.AsNoTracking()
                .Where(x => x.Id == personId && x.Status == EntityStatus.Active)
                .SingleOrDefaultAsync(cancellationToken);
            return p is null
                ? null
                : new PersonInfo(p.FullName, p.GivenName, string.Join(' ', new[] { p.SurnamePrefix, p.Surname }.Where(s => !string.IsNullOrEmpty(s))), p.Mail);
        }, cancellationToken);

    public Task<IReadOnlyList<OfferingMember>> MembersAsync(Guid offeringId, CancellationToken cancellationToken) =>
        SerializedAsync(() => MembersCoreAsync(offeringId, cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<OfferingMember>> MembersCoreAsync(Guid offeringId, CancellationToken cancellationToken)
    {
        var rows = await db.Associations.AsNoTracking()
            .Where(a => a.CourseOfferingId == offeringId && a.Status == EntityStatus.Active && a.Person.Status == EntityStatus.Active)
            .Select(a => new { a.PersonId, a.Person.ExternalId, a.Person.DisplayName, a.Person.GivenName, a.Person.SurnamePrefix, a.Person.Surname, a.Role })
            .ToListAsync(cancellationToken);
        return [.. rows
            .Select(r => new OfferingMember(
                r.PersonId, r.ExternalId,
                r.DisplayName ?? string.Join(' ', new[] { r.GivenName, r.SurnamePrefix, r.Surname }.Where(p => !string.IsNullOrEmpty(p))),
                ToContract(r.Role)))
            .OrderBy(m => m.Name, StringComparer.CurrentCulture)];
    }

    public static OfferingRole ToContract(AssociationRole role) => role switch
    {
        AssociationRole.Student => OfferingRole.Student,
        AssociationRole.Lecturer => OfferingRole.Lecturer,
        AssociationRole.TeachingAssistant => OfferingRole.TeachingAssistant,
        _ => OfferingRole.Coordinator,
    };
}
