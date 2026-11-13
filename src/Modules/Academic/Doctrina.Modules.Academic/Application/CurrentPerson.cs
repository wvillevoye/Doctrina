using System.Security.Claims;
using Doctrina.Core.Entities;
using Doctrina.Infrastructure.Authentication;
using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Academic.Domain;
using Doctrina.Modules.Academic.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Academic.Application;

/// <summary>
/// The person of the signed-in user (spec person-linking, design D7): first by a linked login (issuer + <c>oid</c>),
/// otherwise by <c>personId</c> = ExternalId, after which the login is linked. Never creates or changes a person.
/// </summary>
internal sealed class CurrentPerson(AcademicDbContext db)
{
    public async Task<Person?> FindAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var oid = user.FindFirst(DoctrinaClaimTypes.ObjectId);
        var issuer = oid?.Issuer;
        if (oid is not null)
        {
            var linked = await db.Persons
                .Where(p => p.Status == EntityStatus.Active && p.Logins.Any(l => l.Issuer == issuer && l.Oid == oid.Value))
                .FirstOrDefaultAsync(cancellationToken);
            if (linked is not null)
            {
                return linked;
            }
        }

        var personId = user.FindFirst(DoctrinaClaimTypes.PersonId)?.Value;
        if (string.IsNullOrEmpty(personId))
        {
            return null;
        }

        // ponytail: with the same personId from several sources the first source wins; fine while the SIS is the only one.
        var person = await db.Persons
            .Where(p => p.Status == EntityStatus.Active && p.ExternalId == personId)
            .OrderBy(p => p.Source)
            .FirstOrDefaultAsync(cancellationToken);
        if (person is not null && oid is not null && issuer is not null
            && issuer.Length <= PersonLogin.IssuerMaxLength && oid.Value.Length <= PersonLogin.OidMaxLength)
        {
            await LinkAsync(person, issuer, oid.Value, cancellationToken);
        }

        return person;
    }

    private async Task LinkAsync(Person person, string issuer, string oid, CancellationToken cancellationToken)
    {
        // Another person may still hold this login (e.g. after it was set to ToBeDeleted): move it.
        var existing = await db.PersonLogins.SingleOrDefaultAsync(l => l.Issuer == issuer && l.Oid == oid, cancellationToken);
        if (existing is null)
        {
            db.PersonLogins.Add(new PersonLogin { PersonId = person.Id, Issuer = issuer, Oid = oid });
        }
        else
        {
            existing.PersonId = person.Id;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
        {
            // A parallel request linked the same login first; that's the same outcome.
            db.ChangeTracker.Clear();
        }
    }
}
