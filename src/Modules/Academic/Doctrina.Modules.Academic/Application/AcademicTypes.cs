using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Academic.Domain;
using Doctrina.Modules.Academic.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Academic.Application;

internal enum UpsertOutcome { Created, Updated, Unchanged }

/// <summary>A field of an input record (= a CSV column), by its OOAPI name.</summary>
internal sealed record Field(string Name, bool Required);

/// <summary>
/// One importable type: its fields, its validation and its upsert on ExternalId + Source (design D3). The CSV import
/// uses it now; REST and the generic import of add-api-and-import will use the same instances (house rule 12).
/// Input is a record of OOAPI field name → text, so a CSV row and (later) a JSON object map onto it directly.
/// </summary>
internal abstract class AcademicType
{
    public const string StatusField = "status";

    internal static readonly IReadOnlyDictionary<string, EntityStatus> Statuses =
        new Dictionary<string, EntityStatus> { ["active"] = EntityStatus.Active, ["toBeDeleted"] = EntityStatus.ToBeDeleted };

    /// <summary>Type name in the import, e.g. <c>persons</c>.</summary>
    public abstract string Name { get; }

    /// <summary>Entity name in error codes, e.g. <c>Person</c>.</summary>
    public abstract string EntityName { get; }

    /// <summary>Field that holds the ExternalId, e.g. <c>personId</c>.</summary>
    public abstract string KeyField { get; }

    protected abstract IReadOnlyList<Field> DataFields { get; }

    /// <summary>All fields: key, data fields and <c>status</c>.</summary>
    public IReadOnlyList<Field> Fields => [new(KeyField, true), .. DataFields, new(StatusField, false)];

    public string InvalidCode => $"Academic.{EntityName}.Invalid";

    public string ReferenceNotFoundCode => $"Academic.{EntityName}.ReferenceNotFound";

    public string DuplicateCode => $"Academic.{EntityName}.Duplicate";

    public string ConflictCode => $"Academic.{EntityName}.Conflict";

    /// <summary>Checks the fields without touching the database.</summary>
    public string NotFoundCode => $"Academic.{EntityName}.NotFound";

    public string DuplicateExternalIdCode => $"Academic.{EntityName}.DuplicateExternalId";

    public abstract Result Validate(IReadOnlyDictionary<string, string?> fields);

    /// <summary>All records of the type, including what <see cref="ToFields"/> needs (change add-rest-api, design D2).</summary>
    public abstract IQueryable<ExchangeableEntity> Query(AcademicDbContext db);

    /// <summary>
    /// The record as fields with the same names as <see cref="Fields"/>: the reverse of the upsert, so reading a record
    /// and writing it back changes nothing. References are ExternalIds, dates <c>yyyy-MM-dd</c>, numbers invariant.
    /// </summary>
    public abstract Dictionary<string, string?> ToFields(ExchangeableEntity entity);

    internal static string StatusValue(EntityStatus status) => status == EntityStatus.Active ? "active" : "toBeDeleted";

    protected static string? Date(DateOnly? date) => date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    protected static string ValueOf<TEnum>(IReadOnlyDictionary<string, TEnum> values, TEnum value)
        where TEnum : struct => values.First(v => EqualityComparer<TEnum>.Default.Equals(v.Value, value)).Key;

    public abstract Task<Result<UpsertOutcome>> UpsertAsync(
        AcademicDbContext db, string source, IReadOnlyDictionary<string, string?> fields, CancellationToken cancellationToken);

    protected Error Invalid(FieldReader reader, string code) =>
        Error.Validation(code, $"The {EntityName} record is invalid.", reader.Errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
}

internal abstract class AcademicType<TEntity, TValues> : AcademicType
    where TEntity : ExchangeableEntity
{
    public override Result Validate(IReadOnlyDictionary<string, string?> fields)
    {
        var reader = new FieldReader(fields);
        Read(reader, out _, out _);
        return reader.IsValid ? Result.Success() : Invalid(reader, InvalidCode);
    }

    public override async Task<Result<UpsertOutcome>> UpsertAsync(
        AcademicDbContext db, string source, IReadOnlyDictionary<string, string?> fields, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var reader = new FieldReader(fields);
        var values = Read(reader, out var externalId, out var status);
        if (!reader.IsValid)
        {
            return Invalid(reader, InvalidCode);
        }

        values = await ResolveAsync(new References(db, source, reader), values, cancellationToken);
        if (!reader.IsValid)
        {
            return Invalid(reader, ReferenceNotFoundCode);
        }

        var entity = await db.Set<TEntity>().SingleOrDefaultAsync(e => e.Source == source && e.ExternalId == externalId, cancellationToken);
        await CheckAsync(db, entity, values, reader, cancellationToken);
        if (!reader.IsValid)
        {
            return Invalid(reader, DuplicateCode);
        }

        var created = entity is null;
        if (entity is null)
        {
            entity = Create(externalId!, source, values);
            db.Add(entity);
        }
        else
        {
            Apply(entity, values);
        }

        entity.Status = status ?? EntityStatus.Active;
        db.ChangeTracker.DetectChanges();
        var outcome = created ? UpsertOutcome.Created
            : db.Entry(entity).State == EntityState.Modified ? UpsertOutcome.Updated : UpsertOutcome.Unchanged;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
        {
            // Saved concurrently by someone else; the next attempt sees their record.
            db.ChangeTracker.Clear();
            return Error.Conflict(ConflictCode, $"The {EntityName} record was saved at the same time by another process. Try again.");
        }

        return outcome;
    }

    private TValues Read(FieldReader reader, out string? externalId, out EntityStatus? status)
    {
        externalId = reader.Text(KeyField, ExchangeableEntity.ExternalIdMaxLength, required: true);
        status = reader.Choice(StatusField, Statuses);
        return ReadValues(reader);
    }

    /// <summary>Reads the data fields and checks rules across fields.</summary>
    protected abstract TValues ReadValues(FieldReader reader);

    /// <summary>Turns ExternalId references into Ids; missing ones are added as errors on the reader.</summary>
    protected virtual Task<TValues> ResolveAsync(References references, TValues values, CancellationToken cancellationToken) =>
        Task.FromResult(values);

    /// <summary>Extra checks against the database before saving (e.g. duplicates).</summary>
    protected virtual Task CheckAsync(AcademicDbContext db, TEntity? existing, TValues values, FieldReader reader, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected abstract TEntity Create(string externalId, string source, TValues values);

    public override IQueryable<ExchangeableEntity> Query(AcademicDbContext db) => QueryTyped(db);

    protected abstract IQueryable<TEntity> QueryTyped(AcademicDbContext db);

    public override Dictionary<string, string?> ToFields(ExchangeableEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { [KeyField] = entity.ExternalId };
        Fill(fields, (TEntity)entity);
        fields[StatusField] = StatusValue(entity.Status);
        return fields;
    }

    protected abstract void Fill(Dictionary<string, string?> fields, TEntity entity);

    protected abstract void Apply(TEntity entity, TValues values);
}

/// <summary>Looks up references by ExternalId within the same source (design D4).</summary>
internal sealed class References(AcademicDbContext db, string source, FieldReader reader)
{
    public async Task<Guid?> FindAsync<T>(string field, string? externalId, CancellationToken cancellationToken)
        where T : ExchangeableEntity
    {
        if (externalId is null)
        {
            return null;
        }

        var id = await db.Set<T>()
            .Where(e => e.Source == source && e.ExternalId == externalId)
            .Select(e => (Guid?)e.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (id is null)
        {
            reader.AddError(field, $"No {typeof(T).Name.ToLowerInvariant()} '{externalId}' from source '{source}'.");
        }

        return id;
    }
}

/// <summary>All types, in import order.</summary>
internal static class AcademicTypes
{
    public static IReadOnlyList<AcademicType> All { get; } =
    [
        new OrganizationType_(), new AcademicSessionType_(), new CourseType(), new CourseOfferingType(), new PersonType(), new AssociationType(),
    ];

    public static AcademicType? Find(string name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    internal static readonly IReadOnlyDictionary<string, Domain.OrganizationType> OrganizationTypes = new Dictionary<string, Domain.OrganizationType>
    {
        ["root"] = Domain.OrganizationType.Root, ["institution"] = Domain.OrganizationType.Institution,
        ["faculty"] = Domain.OrganizationType.Faculty, ["department"] = Domain.OrganizationType.Department,
        ["branch"] = Domain.OrganizationType.Branch, ["academy"] = Domain.OrganizationType.Academy, ["school"] = Domain.OrganizationType.School,
    };

    internal static readonly IReadOnlyDictionary<string, Domain.AcademicSessionType> SessionTypes = new Dictionary<string, Domain.AcademicSessionType>
    {
        ["academic year"] = Domain.AcademicSessionType.AcademicYear, ["semester"] = Domain.AcademicSessionType.Semester,
        ["trimester"] = Domain.AcademicSessionType.Trimester, ["quarter"] = Domain.AcademicSessionType.Quarter,
        ["testing period"] = Domain.AcademicSessionType.TestingPeriod, ["period"] = Domain.AcademicSessionType.Period,
    };

    internal static readonly IReadOnlyDictionary<string, AssociationRole> Roles = new Dictionary<string, AssociationRole>
    {
        ["student"] = AssociationRole.Student, ["lecturer"] = AssociationRole.Lecturer,
        ["teaching-assistant"] = AssociationRole.TeachingAssistant, ["coordinator"] = AssociationRole.Coordinator,
    };
}

internal static class Max
{
    public const int Name = Lengths.Name;
    public const int Short = Lengths.Short;
    public const int Description = Lengths.Description;
    public const int Key = ExchangeableEntity.ExternalIdMaxLength;
}

internal sealed class OrganizationType_ : AcademicType<Organization, OrganizationType_.Values>
{
    internal sealed record Values(string? Name, string? ShortName, Domain.OrganizationType? Type, string? ParentExternalId, Guid? ParentId = null);

    public override string Name => "organizations";
    public override string EntityName => "Organization";
    public override string KeyField => "organizationId";
    protected override IReadOnlyList<Field> DataFields { get; } =
        [new("name", true), new("shortName", false), new("organizationType", true), new("parentOrganizationId", false)];

    protected override Values ReadValues(FieldReader r) => new(
        r.Text("name", Max.Name, true), r.Text("shortName", Max.Short),
        r.Choice("organizationType", AcademicTypes.OrganizationTypes, true), r.Text("parentOrganizationId", Max.Key));

    protected override async Task<Values> ResolveAsync(References refs, Values v, CancellationToken ct) =>
        v with { ParentId = await refs.FindAsync<Organization>("parentOrganizationId", v.ParentExternalId, ct) };

    protected override Organization Create(string externalId, string source, Values v)
    {
        var e = new Organization { ExternalId = externalId, Source = source, Name = v.Name! };
        Apply(e, v);
        return e;
    }

    protected override IQueryable<Organization> QueryTyped(AcademicDbContext db) => db.Organizations.Include(o => o.Parent);

    protected override void Fill(Dictionary<string, string?> f, Organization e)
    {
        f["name"] = e.Name;
        f["shortName"] = e.ShortName;
        f["organizationType"] = ValueOf(AcademicTypes.OrganizationTypes, e.OrganizationType);
        f["parentOrganizationId"] = e.Parent?.ExternalId;
    }

    protected override void Apply(Organization e, Values v)
    {
        e.Name = v.Name!;
        e.ShortName = v.ShortName;
        e.OrganizationType = v.Type!.Value;
        e.ParentId = v.ParentId;
    }
}

internal sealed class AcademicSessionType_ : AcademicType<AcademicSession, AcademicSessionType_.Values>
{
    internal sealed record Values(string? Name, Domain.AcademicSessionType? Type, DateOnly? Start, DateOnly? End, string? ParentExternalId, Guid? ParentId = null);

    public override string Name => "academic-sessions";
    public override string EntityName => "AcademicSession";
    public override string KeyField => "academicSessionId";
    protected override IReadOnlyList<Field> DataFields { get; } =
        [new("name", true), new("academicSessionType", true), new("startDate", true), new("endDate", true), new("parentAcademicSessionId", false)];

    protected override Values ReadValues(FieldReader r)
    {
        var v = new Values(
            r.Text("name", Max.Name, true), r.Choice("academicSessionType", AcademicTypes.SessionTypes, true),
            r.Date("startDate", true), r.Date("endDate", true), r.Text("parentAcademicSessionId", Max.Key));
        if (v.Start > v.End)
        {
            r.AddError("endDate", "Must not be before startDate.");
        }

        return v;
    }

    protected override async Task<Values> ResolveAsync(References refs, Values v, CancellationToken ct) =>
        v with { ParentId = await refs.FindAsync<AcademicSession>("parentAcademicSessionId", v.ParentExternalId, ct) };

    protected override AcademicSession Create(string externalId, string source, Values v)
    {
        var e = new AcademicSession { ExternalId = externalId, Source = source, Name = v.Name! };
        Apply(e, v);
        return e;
    }

    protected override IQueryable<AcademicSession> QueryTyped(AcademicDbContext db) => db.AcademicSessions.Include(s => s.Parent);

    protected override void Fill(Dictionary<string, string?> f, AcademicSession e)
    {
        f["name"] = e.Name;
        f["academicSessionType"] = ValueOf(AcademicTypes.SessionTypes, e.AcademicSessionType);
        f["startDate"] = Date(e.StartDate);
        f["endDate"] = Date(e.EndDate);
        f["parentAcademicSessionId"] = e.Parent?.ExternalId;
    }

    protected override void Apply(AcademicSession e, Values v)
    {
        e.Name = v.Name!;
        e.AcademicSessionType = v.Type!.Value;
        e.StartDate = v.Start!.Value;
        e.EndDate = v.End!.Value;
        e.ParentId = v.ParentId;
    }
}

internal sealed class CourseType : AcademicType<Course, CourseType.Values>
{
    internal sealed record Values(string? Name, string? Abbreviation, string? Description, decimal? StudyLoad, string? OrganizationExternalId, Guid? OrganizationId = null);

    public override string Name => "courses";
    public override string EntityName => "Course";
    public override string KeyField => "courseId";
    protected override IReadOnlyList<Field> DataFields { get; } =
        [new("name", true), new("abbreviation", false), new("description", false), new("studyLoad", false), new("organizationId", false)];

    protected override Values ReadValues(FieldReader r) => new(
        r.Text("name", Max.Name, true), r.Text("abbreviation", Max.Short), r.Text("description", Max.Description),
        r.Decimal("studyLoad", 0m), r.Text("organizationId", Max.Key));

    protected override async Task<Values> ResolveAsync(References refs, Values v, CancellationToken ct) =>
        v with { OrganizationId = await refs.FindAsync<Organization>("organizationId", v.OrganizationExternalId, ct) };

    protected override Course Create(string externalId, string source, Values v)
    {
        var e = new Course { ExternalId = externalId, Source = source, Name = v.Name! };
        Apply(e, v);
        return e;
    }

    protected override IQueryable<Course> QueryTyped(AcademicDbContext db) => db.Courses.Include(c => c.Organization);

    protected override void Fill(Dictionary<string, string?> f, Course e)
    {
        f["name"] = e.Name;
        f["abbreviation"] = e.Abbreviation;
        f["description"] = e.Description;
        f["studyLoad"] = e.StudyLoad?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        f["organizationId"] = e.Organization?.ExternalId;
    }

    protected override void Apply(Course e, Values v)
    {
        e.Name = v.Name!;
        e.Abbreviation = v.Abbreviation;
        e.Description = v.Description;
        e.StudyLoad = v.StudyLoad;
        e.OrganizationId = v.OrganizationId;
    }
}

internal sealed class CourseOfferingType : AcademicType<CourseOffering, CourseOfferingType.Values>
{
    internal sealed record Values(
        string? Name, DateOnly? Start, DateOnly? End, string? CourseExternalId, string? SessionExternalId,
        Guid? CourseId = null, Guid? SessionId = null);

    public override string Name => "course-offerings";
    public override string EntityName => "CourseOffering";
    public override string KeyField => "offeringId";
    protected override IReadOnlyList<Field> DataFields { get; } =
        [new("name", true), new("courseId", true), new("academicSessionId", true), new("startDate", false), new("endDate", false)];

    protected override Values ReadValues(FieldReader r)
    {
        var v = new Values(
            r.Text("name", Max.Name, true), r.Date("startDate"), r.Date("endDate"),
            r.Text("courseId", Max.Key, true), r.Text("academicSessionId", Max.Key, true));
        if (v.Start > v.End)
        {
            r.AddError("endDate", "Must not be before startDate.");
        }

        return v;
    }

    protected override async Task<Values> ResolveAsync(References refs, Values v, CancellationToken ct) => v with
    {
        CourseId = await refs.FindAsync<Course>("courseId", v.CourseExternalId, ct),
        SessionId = await refs.FindAsync<AcademicSession>("academicSessionId", v.SessionExternalId, ct),
    };

    protected override CourseOffering Create(string externalId, string source, Values v)
    {
        var e = new CourseOffering { ExternalId = externalId, Source = source, Name = v.Name! };
        Apply(e, v);
        return e;
    }

    protected override IQueryable<CourseOffering> QueryTyped(AcademicDbContext db) => db.CourseOfferings.Include(o => o.Course).Include(o => o.AcademicSession);

    protected override void Fill(Dictionary<string, string?> f, CourseOffering e)
    {
        f["name"] = e.Name;
        f["courseId"] = e.Course.ExternalId;
        f["academicSessionId"] = e.AcademicSession.ExternalId;
        f["startDate"] = Date(e.StartDate);
        f["endDate"] = Date(e.EndDate);
    }

    protected override void Apply(CourseOffering e, Values v)
    {
        e.Name = v.Name!;
        e.StartDate = v.Start;
        e.EndDate = v.End;
        e.CourseId = v.CourseId!.Value;
        e.AcademicSessionId = v.SessionId!.Value;
    }
}

internal sealed class PersonType : AcademicType<Person, PersonType.Values>
{
    internal sealed record Values(string? GivenName, string? SurnamePrefix, string? Surname, string? DisplayName, string? Mail);

    public override string Name => "persons";
    public override string EntityName => "Person";
    public override string KeyField => "personId";
    protected override IReadOnlyList<Field> DataFields { get; } =
        [new("givenName", true), new("surnamePrefix", false), new("surname", true), new("displayName", false), new("mail", false)];

    protected override Values ReadValues(FieldReader r)
    {
        var v = new Values(
            r.Text("givenName", Max.Name, true), r.Text("surnamePrefix", Max.Short), r.Text("surname", Max.Name, true),
            r.Text("displayName", Max.Name), r.Text("mail", Max.Name));
        if (v.Mail is not null && !v.Mail.Contains('@', StringComparison.Ordinal))
        {
            r.AddError("mail", "Not a valid email address.");
        }

        return v;
    }

    protected override Person Create(string externalId, string source, Values v)
    {
        var e = new Person { ExternalId = externalId, Source = source, GivenName = v.GivenName!, Surname = v.Surname! };
        Apply(e, v);
        return e;
    }

    protected override IQueryable<Person> QueryTyped(AcademicDbContext db) => db.Persons;

    protected override void Fill(Dictionary<string, string?> f, Person e)
    {
        f["givenName"] = e.GivenName;
        f["surnamePrefix"] = e.SurnamePrefix;
        f["surname"] = e.Surname;
        f["displayName"] = e.DisplayName;
        f["mail"] = e.Mail;
    }

    protected override void Apply(Person e, Values v)
    {
        e.GivenName = v.GivenName!;
        e.SurnamePrefix = v.SurnamePrefix;
        e.Surname = v.Surname!;
        e.DisplayName = v.DisplayName;
        e.Mail = v.Mail;
    }
}

internal sealed class AssociationType : AcademicType<Association, AssociationType.Values>
{
    internal sealed record Values(AssociationRole? Role, string? PersonExternalId, string? OfferingExternalId, Guid? PersonId = null, Guid? OfferingId = null);

    public override string Name => "associations";
    public override string EntityName => "Association";
    public override string KeyField => "associationId";
    protected override IReadOnlyList<Field> DataFields { get; } =
        [new("personId", true), new("offeringId", true), new("role", true)];

    protected override Values ReadValues(FieldReader r) => new(
        r.Choice("role", AcademicTypes.Roles, true), r.Text("personId", Max.Key, true), r.Text("offeringId", Max.Key, true));

    protected override async Task<Values> ResolveAsync(References refs, Values v, CancellationToken ct) => v with
    {
        PersonId = await refs.FindAsync<Person>("personId", v.PersonExternalId, ct),
        OfferingId = await refs.FindAsync<CourseOffering>("offeringId", v.OfferingExternalId, ct),
    };

    // One association per person, offering and role (design D2).
    protected override async Task CheckAsync(AcademicDbContext db, Association? existing, Values v, FieldReader r, CancellationToken ct)
    {
        var existingId = existing?.Id;
        if (await db.Associations.AnyAsync(
            a => a.PersonId == v.PersonId && a.CourseOfferingId == v.OfferingId && a.Role == v.Role && a.Id != existingId, ct))
        {
            r.AddError("role", "This person already has this role in this offering (under another associationId).");
        }
    }

    protected override Association Create(string externalId, string source, Values v)
    {
        var e = new Association { ExternalId = externalId, Source = source };
        Apply(e, v);
        return e;
    }

    protected override IQueryable<Association> QueryTyped(AcademicDbContext db) => db.Associations.Include(a => a.Person).Include(a => a.CourseOffering);

    protected override void Fill(Dictionary<string, string?> f, Association e)
    {
        f["personId"] = e.Person.ExternalId;
        f["offeringId"] = e.CourseOffering.ExternalId;
        f["role"] = ValueOf(AcademicTypes.Roles, e.Role);
    }

    protected override void Apply(Association e, Values v)
    {
        e.Role = v.Role!.Value;
        e.PersonId = v.PersonId!.Value;
        e.CourseOfferingId = v.OfferingId!.Value;
    }
}
