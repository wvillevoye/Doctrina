using Doctrina.Modules.Academic.Application;
using Doctrina.Modules.Academic.Infrastructure;

namespace Doctrina.Modules.Academic.Tests;

/// <summary>Builders for input records and a few ready-made rows.</summary>
internal static class Fields
{
    public static Dictionary<string, string?> Of(params (string Name, string? Value)[] fields) =>
        fields.ToDictionary(f => f.Name, f => f.Value, StringComparer.OrdinalIgnoreCase);

    public static AcademicType Type(string name) => AcademicTypes.Find(name)!;

    public static Dictionary<string, string?> Course(string id = "BIO101", string name = "Celbiologie") =>
        Of(("courseId", id), ("name", name));

    public static Dictionary<string, string?> Session(string id = "2026-P1") =>
        Of(("academicSessionId", id), ("name", "Period 1"), ("academicSessionType", "period"), ("startDate", "2026-09-01"), ("endDate", "2026-10-31"));

    public static Dictionary<string, string?> Offering(string id = "BIO101-2026-P1", string course = "BIO101", string session = "2026-P1") =>
        Of(("offeringId", id), ("name", id), ("courseId", course), ("academicSessionId", session));

    public static Dictionary<string, string?> Person(string id = "I6000001") =>
        Of(("personId", id), ("givenName", "Sanne"), ("surname", "Student"));

    public static Dictionary<string, string?> Association(string id = "A1", string person = "I6000001", string offering = "BIO101-2026-P1", string role = "student", string? status = null) =>
        Of(("associationId", id), ("personId", person), ("offeringId", offering), ("role", role), ("status", status));

    /// <summary>Upserts and asserts success.</summary>
    public static async Task<UpsertOutcome> UpsertAsync(this AcademicDbContext db, string type, Dictionary<string, string?> fields, CancellationToken ct, string source = "SIS")
    {
        var result = await Type(type).UpsertAsync(db, source, fields, ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    /// <summary>Course, session and offering BIO101-2026-P1 plus person I6000001, all from SIS.</summary>
    public static async Task SeedOfferingAndPersonAsync(this AcademicDbContext db, CancellationToken ct)
    {
        await db.UpsertAsync("courses", Course(), ct);
        await db.UpsertAsync("academic-sessions", Session(), ct);
        await db.UpsertAsync("course-offerings", Offering(), ct);
        await db.UpsertAsync("persons", Person(), ct);
    }
}
