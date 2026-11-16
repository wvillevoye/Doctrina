using System.Linq.Expressions;
using Doctrina.Core.Entities;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Academic.Domain;
using Doctrina.Modules.Academic.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Academic.Application;

/// <summary>
/// Implementation of <see cref="IAcademicExchangeReader"/> (design D4 of add-ooapi-interop): active records only, one query per
/// page plus a count. Filtering and ordering happen on the entity, the projection to a contract record comes last (EF Core
/// cannot translate a filter or order on a constructor projection).
/// </summary>
internal sealed class AcademicExchangeReader(AcademicDbContext db) : IAcademicExchangeReader
{
    private static readonly Func<Organization, ExchangeOrganization> ToOrganization = o =>
        new(o.Id, o.ExternalId, o.Name, o.ShortName, (OrganizationKind)(int)o.OrganizationType, o.ParentId);

    private static readonly Func<AcademicSession, ExchangeSession> ToSession = s =>
        new(s.Id, s.ExternalId, s.Name, (SessionKind)(int)s.AcademicSessionType, s.StartDate, s.EndDate, s.ParentId);

    private static readonly Func<Course, ExchangeCourse> ToCourse = c =>
        new(c.Id, c.ExternalId, c.Name, c.Abbreviation, c.Description, c.OrganizationId);

    private static readonly Func<CourseOffering, ExchangeOffering> ToOffering = o =>
        new(o.Id, o.ExternalId, o.Name, o.CourseId, o.AcademicSessionId, o.StartDate ?? o.AcademicSession.StartDate, o.EndDate ?? o.AcademicSession.EndDate);

    private static readonly Func<Association, ExchangeAssociation> ToAssociation = a =>
        new(a.Id, a.ExternalId, a.PersonId, a.CourseOfferingId, (OfferingRole)(int)a.Role);

    // Enums are stored as text, so they are mapped in memory (a cast to int cannot run in SQL).
    // Init properties (not a positional record), so EF Core can project to it; the roles come along in the same query.
    private static readonly Expression<Func<Person, PersonRow>> ToPersonRow = p => new PersonRow
    {
        Id = p.Id, ExternalId = p.ExternalId, GivenName = p.GivenName, SurnamePrefix = p.SurnamePrefix, Surname = p.Surname, DisplayName = p.DisplayName, Mail = p.Mail,
        Roles = p.Associations.Where(a => a.Status == EntityStatus.Active && a.CourseOffering.Status == EntityStatus.Active).Select(a => a.Role).ToList(),
    };

    public Task<ExchangePage<ExchangeOrganization>> OrganizationsAsync(string? primaryCode, int pageNumber, int pageSize, CancellationToken ct) =>
        PageAsync(db.Organizations.Where(o => o.Status == EntityStatus.Active && (primaryCode == null || o.ExternalId == primaryCode)).OrderBy(o => o.Name).ThenBy(o => o.Id),
            ToOrganization, pageNumber, pageSize, ct);

    public async Task<ExchangeOrganization?> OrganizationAsync(Guid id, CancellationToken ct) =>
        await db.Organizations.AsNoTracking().Where(o => o.Status == EntityStatus.Active && o.Id == id).SingleOrDefaultAsync(ct) is { } e ? ToOrganization(e) : null;

    public Task<ExchangePage<ExchangeSession>> SessionsAsync(string? primaryCode, int pageNumber, int pageSize, CancellationToken ct) =>
        PageAsync(db.AcademicSessions.Where(s => s.Status == EntityStatus.Active && (primaryCode == null || s.ExternalId == primaryCode)).OrderBy(s => s.StartDate).ThenBy(s => s.Name).ThenBy(s => s.Id),
            ToSession, pageNumber, pageSize, ct);

    public async Task<ExchangeSession?> SessionAsync(Guid id, CancellationToken ct) =>
        await db.AcademicSessions.AsNoTracking().Where(s => s.Status == EntityStatus.Active && s.Id == id).SingleOrDefaultAsync(ct) is { } e ? ToSession(e) : null;

    public Task<ExchangePage<ExchangeCourse>> CoursesAsync(string? primaryCode, int pageNumber, int pageSize, CancellationToken ct) =>
        PageAsync(db.Courses.Where(c => c.Status == EntityStatus.Active && (primaryCode == null || c.ExternalId == primaryCode)).OrderBy(c => c.Name).ThenBy(c => c.Id),
            ToCourse, pageNumber, pageSize, ct);

    public async Task<ExchangeCourse?> CourseAsync(Guid id, CancellationToken ct) =>
        await db.Courses.AsNoTracking().Where(c => c.Status == EntityStatus.Active && c.Id == id).SingleOrDefaultAsync(ct) is { } e ? ToCourse(e) : null;

    public Task<ExchangePage<ExchangePerson>> PersonsAsync(string? primaryCode, int pageNumber, int pageSize, CancellationToken ct)
    {
        return PersonPageAsync(
            db.Persons.Where(p => p.Status == EntityStatus.Active && (primaryCode == null || p.ExternalId == primaryCode)).OrderBy(p => p.Surname).ThenBy(p => p.GivenName).ThenBy(p => p.Id),
            pageNumber, pageSize, ct);
    }

    public async Task<ExchangePerson?> PersonAsync(Guid id, CancellationToken ct) =>
        await db.Persons.AsNoTracking().Where(p => p.Status == EntityStatus.Active && p.Id == id).Select(ToPersonRow).SingleOrDefaultAsync(ct) is { } row ? ToPerson(row) : null;

    public Task<ExchangePage<ExchangeOffering>> OfferingsAsync(Guid? courseId, Guid? sessionId, Guid? organizationId, int pageNumber, int pageSize, CancellationToken ct) =>
        PageAsync(
            db.CourseOfferings.Include(o => o.AcademicSession).Where(o => o.Status == EntityStatus.Active
                    && (courseId == null || o.CourseId == courseId) && (sessionId == null || o.AcademicSessionId == sessionId) && (organizationId == null || o.Course.OrganizationId == organizationId))
                .OrderBy(o => o.Name).ThenBy(o => o.Id),
            ToOffering, pageNumber, pageSize, ct);

    public async Task<ExchangeOffering?> OfferingAsync(Guid id, CancellationToken ct) =>
        await db.CourseOfferings.AsNoTracking().Include(o => o.AcademicSession).Where(o => o.Status == EntityStatus.Active && o.Id == id).SingleOrDefaultAsync(ct) is { } e ? ToOffering(e) : null;

    public Task<ExchangePage<ExchangeAssociation>> AssociationsAsync(Guid? offeringId, Guid? personId, OfferingRole? role, int pageNumber, int pageSize, CancellationToken ct)
    {
        var wanted = role is { } r ? (AssociationRole)(int)r : AssociationRole.Student;
        return PageAsync(
            Active(db.Associations).Where(a => (offeringId == null || a.CourseOfferingId == offeringId) && (personId == null || a.PersonId == personId) && (role == null || a.Role == wanted))
                .OrderBy(a => a.Id),
            ToAssociation, pageNumber, pageSize, ct);
    }

    public async Task<ExchangeAssociation?> AssociationAsync(Guid id, CancellationToken ct) =>
        await Active(db.Associations).AsNoTracking().Where(a => a.Id == id).SingleOrDefaultAsync(ct) is { } e ? ToAssociation(e) : null;

    // An enrolment counts when it, its person and its offering are active.
    private static IQueryable<Association> Active(IQueryable<Association> associations) =>
        associations.Where(a => a.Status == EntityStatus.Active && a.Person.Status == EntityStatus.Active && a.CourseOffering.Status == EntityStatus.Active);

    private static ExchangePerson ToPerson(PersonRow p) => new(
        p.Id, p.ExternalId, p.GivenName, p.SurnamePrefix, p.Surname,
        p.DisplayName ?? string.Join(' ', new[] { p.GivenName, p.SurnamePrefix, p.Surname }.Where(s => !string.IsNullOrEmpty(s))),
        p.Mail, [.. p.Roles.Select(AcademicDirectory.ToContract).Distinct().Order()]);

    private static async Task<ExchangePage<T>> PageAsync<TEntity, T>(IOrderedQueryable<TEntity> query, Func<TEntity, T> map, int pageNumber, int pageSize, CancellationToken ct)
        where TEntity : class
    {
        var total = await query.CountAsync(ct);
        var items = await query.AsNoTracking().Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new ExchangePage<T>([.. items.Select(map)], pageNumber, pageSize, total);
    }

    private static async Task<ExchangePage<ExchangePerson>> PersonPageAsync(IOrderedQueryable<Person> query, int pageNumber, int pageSize, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var rows = await query.AsNoTracking().Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(ToPersonRow).ToListAsync(ct);
        return new ExchangePage<ExchangePerson>([.. rows.Select(ToPerson)], pageNumber, pageSize, total);
    }

    private sealed record PersonRow
    {
        public Guid Id { get; init; }
        public string ExternalId { get; init; } = string.Empty;
        public string GivenName { get; init; } = string.Empty;
        public string? SurnamePrefix { get; init; }
        public string Surname { get; init; } = string.Empty;
        public string? DisplayName { get; init; }
        public string? Mail { get; init; }
        public List<AssociationRole> Roles { get; init; } = [];
    }
}
