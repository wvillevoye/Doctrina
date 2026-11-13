using System.Security.Claims;
using System.Text.Json.Nodes;
using Doctrina.Modules.Academic.Api;
using Microsoft.Extensions.Configuration;

namespace Doctrina.Modules.Academic.Tests;

// add-rest-api task 1.2 (design D3, D5): JSON body → fields, and the source per client.
public sealed class ApiInputTests
{
    [Fact]
    public void WaardenWordenTekst()
    {
        var body = JsonNode.Parse("""{ "courseId": "BIO101", "studyLoad": 7.5, "flag": true, "description": null, "colour": "green" }""")!.AsObject();

        var fields = ApiInput.ToFields(body, Fields.Type("courses")).Value!;

        Assert.Equal("BIO101", fields["courseId"]);
        Assert.Equal("7.5", fields["studyLoad"]);
        Assert.Equal("true", fields["flag"]);
        Assert.True(fields.ContainsKey("description"));
        Assert.Null(fields["description"]);
        Assert.Equal("green", fields["colour"]); // kept; the type ignores unknown fields
    }

    [Fact]
    public void LijstAlsWaardeIsEenValidatiefout()
    {
        var body = JsonNode.Parse("""{ "courseId": "BIO101", "name": ["a", "b"] }""")!.AsObject();

        var result = ApiInput.ToFields(body, Fields.Type("courses"));

        Assert.Equal("Academic.Course.Invalid", result.Error!.Code);
        Assert.Equal(["name"], result.Error.Details!.Keys);
    }

    [Theory]
    [InlineData("roosterpakket", "Rooster")]
    [InlineData("doctrina-sis-sync", "SIS")]
    [InlineData(null, "SIS")]
    public void BronPerClient(string? client, string expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Api:Sources:roosterpakket"] = "Rooster" })
            .Build();
        var caller = new ClaimsPrincipal(new ClaimsIdentity(client is null ? [] : [new Claim("azp", client)], "test"));

        Assert.Equal(expected, ApiSource.For(caller, configuration));
    }
}
