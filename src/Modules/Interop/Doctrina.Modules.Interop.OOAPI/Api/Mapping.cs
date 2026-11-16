using System.Globalization;
using Doctrina.Modules.Academic.Contracts;

namespace Doctrina.Modules.Interop.OOAPI.Api;

/// <summary>Doctrina records to OOAPI v5 resources (design D5). Absent values are left out of the JSON.</summary>
internal static class Mapping
{
    public static object Organization(ExchangeOrganization o, OoapiOptions opt) => Obj(
        ("organizationId", o.Id), ("primaryCode", Code(o.ExternalId)),
        ("organizationType", o.Kind == OrganizationKind.Institution ? "institute" : o.Kind.ToString().ToLowerInvariant()),
        ("name", Text(o.Name, opt)), ("shortName", o.ShortName ?? o.Name), ("parent", o.ParentId));

    public static object Session(ExchangeSession s, OoapiOptions opt) => Obj(
        ("academicSessionId", s.Id), ("primaryCode", Code(s.ExternalId)), ("academicSessionType", s.Kind switch
        {
            SessionKind.AcademicYear => "academic year",
            SessionKind.TestingPeriod => "testing period",
            var k => k.ToString().ToLowerInvariant(),
        }),
        ("name", Text(s.Name, opt)), ("startDate", Date(s.StartDate)), ("endDate", Date(s.EndDate)), ("parent", s.ParentId));

    public static object Course(ExchangeCourse c, OoapiOptions opt) => Obj(
        ("courseId", c.Id), ("primaryCode", Code(c.ExternalId)), ("name", Text(c.Name, opt)),
        ("abbreviation", c.Abbreviation ?? c.ExternalId), ("description", Text(c.Description ?? c.Name, opt)),
        ("teachingLanguage", opt.TeachingLanguage), ("level", opt.Level), ("organization", c.OrganizationId));

    public static object Offering(ExchangeOffering o, OoapiOptions opt) => Obj(
        ("offeringId", o.Id), ("primaryCode", Code(o.ExternalId)), ("offeringType", "course"), ("name", Text(o.Name, opt)),
        ("description", Text(o.Name, opt)), ("teachingLanguage", opt.TeachingLanguage), ("resultExpected", opt.ResultExpected),
        ("startDate", Date(o.StartDate)), ("endDate", Date(o.EndDate)), ("course", o.CourseId), ("academicSession", o.SessionId));

    // Affiliations come from the roles: a student is a student, any teaching role is an employee, nobody is a guest.
    public static object Person(ExchangePerson p) => Obj(
        ("personId", p.Id), ("primaryCode", Code(p.ExternalId)), ("givenName", p.GivenName), ("surnamePrefix", p.SurnamePrefix), ("surname", p.Surname),
        ("displayName", p.DisplayName), ("mail", p.Mail), ("activeEnrollment", p.Roles.Contains(OfferingRole.Student)),
        ("affiliations", p.Roles.Count == 0 ? Guest : [.. p.Roles.Select(r => r == OfferingRole.Student ? "student" : "employee").Distinct().Order()]));

    public static object Association(ExchangeAssociation a) => Obj(
        ("associationId", a.Id), ("associationType", "courseOfferingAssociation"), ("role", Role(a.Role)), ("state", "associated"),
        ("person", a.PersonId), ("offering", a.OfferingId));

    public static OfferingRole? ParseRole(string? role) => role switch
    {
        "student" => OfferingRole.Student,
        "lecturer" => OfferingRole.Lecturer,
        "teaching assistant" => OfferingRole.TeachingAssistant,
        "coordinator" => OfferingRole.Coordinator,
        _ => null,
    };

    private static string Role(OfferingRole role) => role switch
    {
        OfferingRole.Student => "student",
        OfferingRole.Lecturer => "lecturer",
        OfferingRole.TeachingAssistant => "teaching assistant",
        _ => "coordinator",
    };

    private static readonly string[] Guest = ["guest"];

    private static object Code(string code) => new { codeType = "identifier", code };

    private static object[] Text(string value, OoapiOptions opt) => [new { language = opt.Language, value }];

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Dictionary<string, object?> Obj(params (string Name, object? Value)[] fields) =>
        fields.Where(f => f.Value is not null).ToDictionary(f => f.Name, f => f.Value);
}
