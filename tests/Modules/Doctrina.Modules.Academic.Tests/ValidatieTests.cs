using Doctrina.Core.Results;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Academic.Tests;

// Spec academic-data, requirement "Invoer wordt gevalideerd met stabiele foutcodes".
public sealed class InvoerWordtGevalideerdMetStabieleFoutcodes
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task VerplichtVeldOntbreekt()
    {
        await using var db = await AcademicDatabase.CreateAsync(Ct);
        await using var context = db.CreateContext();

        var result = await Fields.Type("persons").UpsertAsync(context, "SIS", Fields.Of(("personId", "I6000001"), ("givenName", "Sanne")), Ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("Academic.Person.Invalid", result.Error.Code);
        Assert.Equal(["surname"], result.Error.Details!.Keys);
        Assert.Equal(0, await context.Persons.CountAsync(Ct));
    }

    [Fact]
    public void EinddatumVoorBegindatum()
    {
        var fields = Fields.Session();
        fields["startDate"] = "2026-11-01";
        fields["endDate"] = "2026-09-01";

        var result = Fields.Type("academic-sessions").Validate(fields);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(["endDate"], result.Error.Details!.Keys);
    }

    [Theory]
    [InlineData("organizations", "organizationType", "faculty", true)]
    [InlineData("organizations", "organizationType", "FACULTY", true)]
    [InlineData("organizations", "organizationType", "university", false)]
    [InlineData("academic-sessions", "academicSessionType", "academic year", true)]
    [InlineData("academic-sessions", "academicSessionType", "year", false)]
    [InlineData("courses", "studyLoad", "7.5", true)]
    [InlineData("courses", "studyLoad", "7,5", false)]
    [InlineData("courses", "studyLoad", "-1", false)]
    [InlineData("persons", "mail", "sanne@example.test", true)]
    [InlineData("persons", "mail", "sanne", false)]
    [InlineData("course-offerings", "startDate", "2026-09-01", true)]
    [InlineData("course-offerings", "startDate", "01-09-2026", false)]
    [InlineData("persons", "status", "toBeDeleted", true)]
    [InlineData("persons", "status", "deleted", false)]
    public void VeldwaardenWordenGecontroleerd(string type, string field, string value, bool valid)
    {
        var fields = type switch
        {
            "organizations" => Fields.Of(("organizationId", "UM"), ("name", "Maastricht University"), ("organizationType", "root")),
            "academic-sessions" => Fields.Session(),
            "courses" => Fields.Course(),
            "course-offerings" => Fields.Offering(),
            _ => Fields.Person(),
        };
        fields[field] = value;

        var result = Fields.Type(type).Validate(fields);

        Assert.Equal(valid, result.IsSuccess);
        if (!valid)
        {
            Assert.Equal([field], result.Error!.Details!.Keys);
        }
    }

    [Fact]
    public void TeLangeWaardeWordtGeweigerd()
    {
        var fields = Fields.Course(name: new string('x', 201));

        var result = Fields.Type("courses").Validate(fields);

        Assert.Equal(["name"], result.Error!.Details!.Keys);
    }
}

// Spec academic-data, requirement "Inschrijvingen hebben een rol".
public sealed class InschrijvingenHebbenEenRol
{
    [Fact]
    public void OnbekendeRol()
    {
        var result = Fields.Type("associations").Validate(Fields.Association(role: "teacher"));

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(["role"], result.Error.Details!.Keys);
    }
}
