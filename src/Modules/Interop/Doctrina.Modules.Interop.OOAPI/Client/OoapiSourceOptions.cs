using Doctrina.Core.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Interop.OOAPI.Client;

/// <summary>
/// One OOAPI v5 source to read (design D4 of add-ooapi-consumer): <c>Ooapi:Sources:&lt;name&gt;</c>. The name becomes the
/// <c>Source</c> of every record read from it. <see cref="ClientSecret"/> belongs in <c>appsettings.Secrets.json</c> only.
/// </summary>
public sealed class OoapiSourceOptions
{
    /// <summary>The OOAPI base, e.g. <c>https://sis.example.org/ooapi/v5</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>OAuth 2.0 token endpoint (client credentials).</summary>
    public string TokenUrl { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string? Scope { get; set; }

    /// <summary>Cron expression (5 fields, UTC); empty means only on request.</summary>
    public string? Schedule { get; set; }

    /// <summary>One of 10, 20, 50, 100 or 250, the page sizes of the standard.</summary>
    public int PageSize { get; set; } = 100;

    public bool Enabled { get; set; } = true;
}

/// <summary>Checks the sources when the host starts, so a wrong source stops the start instead of failing at 03:00.</summary>
internal sealed class OoapiOptionsValidator(IHostEnvironment environment) : IValidateOptions<OoapiOptions>
{
    private static readonly int[] PageSizes = [10, 20, 50, 100, 250];

    public ValidateOptionsResult Validate(string? name, OoapiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        foreach (var (source, o) in options.Sources)
        {
            if (source.Length is 0 or > ExchangeableEntity.SourceMaxLength)
            {
                errors.Add($"Ooapi:Sources:{source}: the name must be 1 to {ExchangeableEntity.SourceMaxLength} characters.");
            }

            if (!PageSizes.Contains(o.PageSize))
            {
                errors.Add($"Ooapi:Sources:{source}:PageSize must be one of {string.Join(", ", PageSizes)}.");
            }

            if (!string.IsNullOrWhiteSpace(o.Schedule) && !IsCron(o.Schedule))
            {
                errors.Add($"Ooapi:Sources:{source}:Schedule must be a cron expression with 5 fields.");
            }

            if (!o.Enabled)
            {
                continue;
            }

            Check(errors, source, "BaseUrl", o.BaseUrl);
            Check(errors, source, "TokenUrl", o.TokenUrl);
            foreach (var (field, value) in new[] { ("ClientId", o.ClientId), ("ClientSecret", o.ClientSecret) })
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add($"Ooapi:Sources:{source}:{field} is required (the secret belongs in appsettings.Secrets.json).");
                }
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    // ponytail: only the shape (5 fields); Hangfire has no public cron parser and rejects a wrong expression when the job is registered at start-up.
    private static bool IsCron(string schedule) => schedule.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 5;

    private void Check(List<string> errors, string source, string field, string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            errors.Add($"Ooapi:Sources:{source}:{field} must be an absolute http(s) URL.");
        }
        else if (uri.Scheme != Uri.UriSchemeHttps && !environment.IsDevelopment())
        {
            errors.Add($"Ooapi:Sources:{source}:{field} must use https outside Development.");
        }
    }
}
