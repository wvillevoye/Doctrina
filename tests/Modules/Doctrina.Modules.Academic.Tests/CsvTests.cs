using System.Text;
using Doctrina.Modules.Academic.Application;

namespace Doctrina.Modules.Academic.Tests;

internal static class Csv
{
    public static Task<Doctrina.Core.Results.Result<CsvFile>> ReadAsync(string type, string content, bool bom = false, AcademicImportOptions? limits = null)
    {
        var bytes = new UTF8Encoding(bom).GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray();
        return CsvReader.ReadAsync(new MemoryStream(bytes), Fields.Type(type), limits ?? new AcademicImportOptions(), TestContext.Current.CancellationToken);
    }
}

// Spec academic-csv-import, requirement "Het CSV-formaat is vergevingsgezind en voorspelbaar".
public sealed class HetCsvFormaatIsVergevingsgezindEnVoorspelbaar
{
    [Fact]
    public async Task PuntkommaAlsScheidingsteken()
    {
        var comma = await Csv.ReadAsync("persons", "personId,givenName,surname\nI6000001,Sanne,Student\nP7000002,Dirk,Docent\n");
        var semicolon = await Csv.ReadAsync("persons", "personId;givenName;surname\r\nI6000001;Sanne;Student\r\nP7000002;Dirk;Docent\r\n", bom: true);

        Assert.True(semicolon.IsSuccess);
        Assert.Equal(
            comma.Value!.Rows.Select(r => (r.Line, r.Fields["personId"], r.Fields["givenName"], r.Fields["surname"])),
            semicolon.Value!.Rows.Select(r => (r.Line, r.Fields["personId"], r.Fields["givenName"], r.Fields["surname"])));
        Assert.Equal([2, 3], semicolon.Value.Rows.Select(r => r.Line));
    }

    [Fact]
    public async Task VerplichteKolomOntbreekt()
    {
        var result = await Csv.ReadAsync("persons", "personId,givenName\nI6000001,Sanne\n");

        Assert.Equal(CsvReader.MissingColumnsCode, result.Error!.Code);
        Assert.Contains("surname", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnbekendeKolom()
    {
        var result = await Csv.ReadAsync("courses", "courseId,name,colour\nBIO101,Cell biology,green\n");

        var row = Assert.Single(result.Value!.Rows);
        Assert.False(row.Fields.ContainsKey("colour"));
        Assert.Contains(result.Value.Warnings, w => w.Contains("'colour'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AanhalingstekensMetScheidingstekenEnRegeleinde()
    {
        var result = await Csv.ReadAsync("courses", "courseId,name,description\nBIO101,\"Cells, tissues\",\"Line one\nline two\"\nWIS201,Calculus,\n");

        Assert.Equal(["Cells, tissues", "Calculus"], result.Value!.Rows.Select(r => r.Fields["name"]));
        Assert.Equal("Line one\nline two", result.Value.Rows[0].Fields["description"]);
        Assert.Equal([2, 4], result.Value.Rows.Select(r => r.Line));
    }

    [Fact]
    public async Task KolomnamenZijnHoofdletterongevoelig()
    {
        var result = await Csv.ReadAsync("persons", "PersonID,GIVENNAME,Surname\nI6000001,Sanne,Student\n");

        Assert.Equal("Sanne", Assert.Single(result.Value!.Rows).Fields["givenName"]);
    }

    [Fact]
    public async Task LeegBestand()
    {
        var result = await Csv.ReadAsync("persons", "");

        Assert.Equal(CsvReader.EmptyCode, result.Error!.Code);
    }
}

// Spec academic-csv-import, requirement "De import heeft een grens".
public sealed class DeImportHeeftEenGrens
{
    [Fact]
    public async Task TeGrootBestand()
    {
        var content = "courseId,name\n" + string.Concat(Enumerable.Range(1, 101).Select(i => $"C{i},Course {i}\n"));

        var tooMany = await Csv.ReadAsync("courses", content, limits: new AcademicImportOptions { MaxRows = 100 });
        var atLimit = await Csv.ReadAsync("courses", content, limits: new AcademicImportOptions { MaxRows = 101 });

        Assert.Equal(CsvReader.TooLargeCode, tooMany.Error!.Code);
        Assert.Contains("100", tooMany.Error.Message, StringComparison.Ordinal);
        Assert.Equal(101, atLimit.Value!.Rows.Count);
    }

    [Fact]
    public async Task TeGrootBestandInBytes()
    {
        var result = await Csv.ReadAsync("courses", "courseId,name\n" + new string('x', 2000), limits: new AcademicImportOptions { MaxFileSize = 1000 });

        Assert.Equal(CsvReader.TooLargeCode, result.Error!.Code);
    }
}
