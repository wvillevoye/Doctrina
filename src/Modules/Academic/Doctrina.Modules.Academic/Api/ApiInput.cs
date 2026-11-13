using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Doctrina.Core.Results;
using Doctrina.Infrastructure.Authentication;
using Doctrina.Modules.Academic.Application;
using Microsoft.Extensions.Configuration;

namespace Doctrina.Modules.Academic.Api;

/// <summary>A JSON body as the same field record a CSV row gives (change add-rest-api, design D3).</summary>
internal static class ApiInput
{
    /// <summary>
    /// Strings stay strings, numbers become invariant text, booleans "true"/"false", <c>null</c> means empty. An array or
    /// object as value is a validation error for that field. Unknown fields are kept; the type ignores them.
    /// </summary>
    public static Result<Dictionary<string, string?>> ToFields(JsonObject body, AcademicType type)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(type);
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (name, node) in body)
        {
            switch (node)
            {
                case null:
                    fields[name] = null;
                    break;
                case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                    fields[name] = value.GetValue<string>();
                    break;
                case JsonValue value when value.GetValueKind() == JsonValueKind.Number:
                    fields[name] = value.GetValue<decimal>().ToString(CultureInfo.InvariantCulture);
                    break;
                case JsonValue value when value.GetValueKind() is JsonValueKind.True or JsonValueKind.False:
                    fields[name] = value.GetValue<bool>() ? "true" : "false";
                    break;
                default:
                    errors[name] = ["Must be a single value, not a list or an object."];
                    break;
            }
        }

        return errors.Count > 0
            ? Error.Validation(type.InvalidCode, $"The {type.EntityName} record is invalid.", errors)
            : fields;
    }
}

/// <summary>The source a system writes with (design D5): <c>Api:Sources:{azp}</c>, otherwise <c>SIS</c>.</summary>
internal static class ApiSource
{
    public const string Default = "SIS";

    public static string For(ClaimsPrincipal caller, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(configuration);
        var client = caller.FindFirst(DoctrinaClaimTypes.AuthorizedParty)?.Value;
        var configured = string.IsNullOrEmpty(client) ? null : configuration[$"Api:Sources:{client}"];
        return string.IsNullOrWhiteSpace(configured) ? Default : configured.Trim();
    }
}
