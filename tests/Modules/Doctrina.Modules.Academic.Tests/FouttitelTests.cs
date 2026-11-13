using System.Globalization;
using System.Resources;
using Doctrina.Modules.Academic.Application;

namespace Doctrina.Modules.Academic.Tests;

// Task 3.3: every error code of the module has an English (UI) and a Dutch (API default) title.
public sealed class ElkeFoutcodeHeeftEenTitel
{
    private static readonly ResourceManager Titles =
        new("Doctrina.Modules.Academic.Resources.AcademicErrors", typeof(AcademicModule).Assembly);

    public static TheoryData<string> Codes() =>
        [
            .. AcademicTypes.All.SelectMany(t => new[] { t.InvalidCode, t.ReferenceNotFoundCode, t.DuplicateCode, t.ConflictCode }),
            CsvReader.EmptyCode, CsvReader.MissingColumnsCode, CsvReader.TooLargeCode, AcademicImporter.InvalidSourceCode,
        ];

    [Theory]
    [MemberData(nameof(Codes))]
    public void FoutcodeHeeftEenEngelseEnNederlandseTitel(string code)
    {
        var english = Titles.GetString(code, CultureInfo.GetCultureInfo("en"));
        var dutch = Titles.GetString(code, CultureInfo.GetCultureInfo("nl"));

        Assert.False(string.IsNullOrEmpty(english), $"{code} has no English title.");
        Assert.False(string.IsNullOrEmpty(dutch), $"{code} has no Dutch title.");
        Assert.NotEqual(dutch, english);
    }
}
