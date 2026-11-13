using Doctrina.Core.Entities;
using Doctrina.Modules.Academic.Domain;
using Doctrina.Modules.Academic.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Academic.Application;

/// <summary>A row in an admin list; <see cref="Summary"/> is a short type-specific description.</summary>
/// <remarks>Init properties (not a positional record), so EF Core can filter and sort on the projection.</remarks>
internal sealed record ListRow
{
    public Guid Id { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public EntityStatus Status { get; init; }
    public string Source { get; init; } = string.Empty;
}

internal sealed record ListPage(IReadOnlyList<ListRow> Rows, int Page, int PageCount, int Total);

/// <summary>Read-only queries for the admin pages (spec academic-admin, design D8).</summary>
internal sealed class AcademicQueries(AcademicDbContext db)
{
    public const int PageSize = 25;

    public async Task<ListPage?> ListAsync(string typeName, string? search, int page, CancellationToken cancellationToken)
    {
        var rows = Rows(typeName);
        if (rows is null)
        {
            return null;
        }

        search = search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            rows = rows.Where(r => r.Name.Contains(search) || r.ExternalId.Contains(search) || (r.Summary != null && r.Summary.Contains(search)));
        }

        var total = await rows.CountAsync(cancellationToken);
        var pageCount = Math.Max(1, (total + PageSize - 1) / PageSize);
        page = Math.Clamp(page, 1, pageCount);
        var items = await rows.OrderBy(r => r.Name).ThenBy(r => r.ExternalId)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(cancellationToken);
        return new ListPage(items, page, pageCount, total);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountsAsync(CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var type in AcademicTypes.All)
        {
            counts[type.Name] = await Rows(type.Name)!.CountAsync(cancellationToken);
        }

        return counts;
    }

    private IQueryable<ListRow>? Rows(string typeName) => typeName switch
    {
        "organizations" => db.Organizations.Select(o => new ListRow { Id = o.Id, ExternalId = o.ExternalId, Name = o.Name, Summary = o.ShortName, Status = o.Status, Source = o.Source }),
        "academic-sessions" => db.AcademicSessions.Select(s => new ListRow { Id = s.Id, ExternalId = s.ExternalId, Name = s.Name, Summary = null, Status = s.Status, Source = s.Source }),
        "courses" => db.Courses.Select(c => new ListRow { Id = c.Id, ExternalId = c.ExternalId, Name = c.Name, Summary = c.Abbreviation, Status = c.Status, Source = c.Source }),
        "course-offerings" => db.CourseOfferings.Select(o =>
            new ListRow { Id = o.Id, ExternalId = o.ExternalId, Name = o.Name, Summary = o.Course.Name + " · " + o.AcademicSession.Name, Status = o.Status, Source = o.Source }),
        "persons" => db.Persons.Select(p => new ListRow
        {
            Id = p.Id, ExternalId = p.ExternalId,
            Name = p.DisplayName ?? (p.GivenName + " " + (p.SurnamePrefix == null ? "" : p.SurnamePrefix + " ") + p.Surname),
            Summary = p.Mail, Status = p.Status, Source = p.Source,
        }),
        "associations" => db.Associations.Select(a => new ListRow
        {
            Id = a.Id, ExternalId = a.ExternalId, Name = a.Person.DisplayName ?? (a.Person.GivenName + " " + a.Person.Surname),
            Summary = a.CourseOffering.Name, Status = a.Status, Source = a.Source,
        }),
        _ => null,
    };

    public Task<Organization?> OrganizationAsync(Guid id, CancellationToken ct) =>
        db.Organizations.AsNoTracking().Include(o => o.Parent).SingleOrDefaultAsync(o => o.Id == id, ct);

    public Task<AcademicSession?> SessionAsync(Guid id, CancellationToken ct) =>
        db.AcademicSessions.AsNoTracking().Include(s => s.Parent).SingleOrDefaultAsync(s => s.Id == id, ct);

    public Task<Course?> CourseAsync(Guid id, CancellationToken ct) =>
        db.Courses.AsNoTracking().Include(c => c.Organization).Include(c => c.Offerings).ThenInclude(o => o.AcademicSession)
            .SingleOrDefaultAsync(c => c.Id == id, ct);

    public Task<CourseOffering?> OfferingAsync(Guid id, CancellationToken ct) =>
        db.CourseOfferings.AsNoTracking().Include(o => o.Course).Include(o => o.AcademicSession)
            .Include(o => o.Associations).ThenInclude(a => a.Person)
            .SingleOrDefaultAsync(o => o.Id == id, ct);

    public Task<Person?> PersonAsync(Guid id, CancellationToken ct) =>
        db.Persons.AsNoTracking().Include(p => p.Logins)
            .Include(p => p.Associations).ThenInclude(a => a.CourseOffering).ThenInclude(o => o.AcademicSession)
            .SingleOrDefaultAsync(p => p.Id == id, ct);

    public Task<Association?> AssociationAsync(Guid id, CancellationToken ct) =>
        db.Associations.AsNoTracking().Include(a => a.Person).Include(a => a.CourseOffering)
            .SingleOrDefaultAsync(a => a.Id == id, ct);
}
