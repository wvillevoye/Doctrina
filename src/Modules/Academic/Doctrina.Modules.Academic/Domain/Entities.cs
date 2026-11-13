using Doctrina.Core.Entities;

namespace Doctrina.Modules.Academic.Domain;

// The OOAPI v5 core model (design D2 of add-academic-core). Field names follow OOAPI; "name" is a single string.

public sealed class Organization : ExchangeableEntity
{
    public required string Name { get; set; }
    public string? ShortName { get; set; }
    public OrganizationType OrganizationType { get; set; }
    public Guid? ParentId { get; set; }
    public Organization? Parent { get; set; }
}

public sealed class AcademicSession : ExchangeableEntity
{
    public required string Name { get; set; }
    public AcademicSessionType AcademicSessionType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? ParentId { get; set; }
    public AcademicSession? Parent { get; set; }
}

public sealed class Course : ExchangeableEntity
{
    public required string Name { get; set; }
    public string? Abbreviation { get; set; }
    public string? Description { get; set; }
    public decimal? StudyLoad { get; set; }
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public List<CourseOffering> Offerings { get; } = [];
}

public sealed class CourseOffering : ExchangeableEntity
{
    public required string Name { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public Guid AcademicSessionId { get; set; }
    public AcademicSession AcademicSession { get; set; } = null!;
    public List<Association> Associations { get; } = [];
}

public sealed class Person : ExchangeableEntity
{
    public required string GivenName { get; set; }
    public string? SurnamePrefix { get; set; }
    public required string Surname { get; set; }
    public string? DisplayName { get; set; }
    public string? Mail { get; set; }
    public List<Association> Associations { get; } = [];
    public List<PersonLogin> Logins { get; } = [];

    /// <summary>Display name, or "given name [prefix] surname".</summary>
    public string FullName => DisplayName ?? string.Join(' ', new[] { GivenName, SurnamePrefix, Surname }.Where(p => !string.IsNullOrEmpty(p)));
}

/// <summary>Enrolment of a person in a course offering, with a role.</summary>
public sealed class Association : ExchangeableEntity
{
    public AssociationRole Role { get; set; }
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public Guid CourseOfferingId { get; set; }
    public CourseOffering CourseOffering { get; set; } = null!;
}

/// <summary>A login (issuer + <c>oid</c>) linked to a person (design D7). Not exchangeable.</summary>
public sealed class PersonLogin : Entity
{
    public const int IssuerMaxLength = 500;
    public const int OidMaxLength = 255;

    public Guid PersonId { get; set; }
    public required string Issuer { get; set; }
    public required string Oid { get; set; }
}

public enum OrganizationType { Root, Institution, Faculty, Department, Branch, Academy, School }

public enum AcademicSessionType { AcademicYear, Semester, Trimester, Quarter, TestingPeriod, Period }

public enum AssociationRole { Student, Lecturer, TeachingAssistant, Coordinator }
