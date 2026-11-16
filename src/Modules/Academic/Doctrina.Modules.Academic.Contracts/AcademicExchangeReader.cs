namespace Doctrina.Modules.Academic.Contracts;

// Read contract for modules that offer the core model in a standard format (design D4 of add-ooapi-interop). Plain records,
// only active data; the mapping to a standard (field names, codes) belongs to the calling module.

/// <summary>The kind of an organization (OOAPI organization type).</summary>
public enum OrganizationKind { Root, Institution, Faculty, Department, Branch, Academy, School }

/// <summary>The kind of an academic session (OOAPI academic session type).</summary>
public enum SessionKind { AcademicYear, Semester, Trimester, Quarter, TestingPeriod, Period }

/// <summary>One page of a list; <see cref="TotalItems"/> counts all items of the list, not only this page.</summary>
public sealed record ExchangePage<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalItems);

public sealed record ExchangeOrganization(Guid Id, string ExternalId, string Name, string? ShortName, OrganizationKind Kind, Guid? ParentId);

public sealed record ExchangeSession(Guid Id, string ExternalId, string Name, SessionKind Kind, DateOnly StartDate, DateOnly EndDate, Guid? ParentId);

public sealed record ExchangeCourse(Guid Id, string ExternalId, string Name, string? Abbreviation, string? Description, Guid? OrganizationId);

/// <summary>An offering; the dates are those of the offering, or of its academic session if it has none.</summary>
public sealed record ExchangeOffering(Guid Id, string ExternalId, string Name, Guid CourseId, Guid SessionId, DateOnly StartDate, DateOnly EndDate);

/// <summary>A person with the roles of their active enrolments (none: not enrolled anywhere).</summary>
public sealed record ExchangePerson(
    Guid Id, string ExternalId, string GivenName, string? SurnamePrefix, string Surname, string DisplayName, string? Mail, IReadOnlyList<OfferingRole> Roles);

public sealed record ExchangeAssociation(Guid Id, string ExternalId, Guid PersonId, Guid OfferingId, OfferingRole Role);

/// <summary>
/// Paged lists and lookups by id of the active records of the core model. Lists are ordered by name (or date) and then by
/// id, so a page is stable. <c>primaryCode</c> filters on the external id. Never exposes Academic's entities or tables.
/// </summary>
public interface IAcademicExchangeReader
{
    Task<ExchangePage<ExchangeOrganization>> OrganizationsAsync(string? primaryCode, int pageNumber, int pageSize, CancellationToken cancellationToken);

    Task<ExchangeOrganization?> OrganizationAsync(Guid id, CancellationToken cancellationToken);

    Task<ExchangePage<ExchangeSession>> SessionsAsync(string? primaryCode, int pageNumber, int pageSize, CancellationToken cancellationToken);

    Task<ExchangeSession?> SessionAsync(Guid id, CancellationToken cancellationToken);

    Task<ExchangePage<ExchangeCourse>> CoursesAsync(string? primaryCode, int pageNumber, int pageSize, CancellationToken cancellationToken);

    Task<ExchangeCourse?> CourseAsync(Guid id, CancellationToken cancellationToken);

    Task<ExchangePage<ExchangePerson>> PersonsAsync(string? primaryCode, int pageNumber, int pageSize, CancellationToken cancellationToken);

    Task<ExchangePerson?> PersonAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The offerings of a course, a session or an organization (the organization of the course); all given filters apply.</summary>
    Task<ExchangePage<ExchangeOffering>> OfferingsAsync(Guid? courseId, Guid? sessionId, Guid? organizationId, int pageNumber, int pageSize, CancellationToken cancellationToken);

    Task<ExchangeOffering?> OfferingAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The enrolments in an offering or of a person, optionally of one role; active enrolments of active persons only.</summary>
    Task<ExchangePage<ExchangeAssociation>> AssociationsAsync(Guid? offeringId, Guid? personId, OfferingRole? role, int pageNumber, int pageSize, CancellationToken cancellationToken);

    Task<ExchangeAssociation?> AssociationAsync(Guid id, CancellationToken cancellationToken);
}
