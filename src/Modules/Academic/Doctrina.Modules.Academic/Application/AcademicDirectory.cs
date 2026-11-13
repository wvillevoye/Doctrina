using System.Security.Claims;
using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Academic.Domain;
using Doctrina.Modules.Academic.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Academic.Application;

/// <summary>Implementation of the public contract <see cref="IAcademicDirectory"/> (design D1 of add-groups).</summary>
internal sealed class AcademicDirectory(AcademicDbContext db, CurrentPerson currentPerson) : IAcademicDirectory
{
    public const string OfferingNotFoundCode = "Academic.CourseOffering.NotFound";

    public async Task<Result<OfferingInfo>> FindOfferingAsync(Guid offeringId, CancellationToken cancellationToken)
    {
        var offering = await db.CourseOfferings.AsNoTracking()
            .Where(o => o.Id == offeringId && o.Status == EntityStatus.Active)
            .Select(o => new OfferingInfo(o.Id, o.ExternalId, o.Name, o.Course.Name, o.AcademicSession.Name))
            .SingleOrDefaultAsync(cancellationToken);
        return offering is null
            ? Error.NotFound(OfferingNotFoundCode, $"Course offering {offeringId} doesn't exist.")
            : offering;
    }

    public async Task<Guid?> CurrentPersonIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
        (await currentPerson.FindAsync(user, cancellationToken))?.Id;

    public async Task<IReadOnlyList<OfferingMember>> MembersAsync(Guid offeringId, CancellationToken cancellationToken)
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
