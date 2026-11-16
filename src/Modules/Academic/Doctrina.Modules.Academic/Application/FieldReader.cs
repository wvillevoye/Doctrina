using System.Globalization;

namespace Doctrina.Modules.Academic.Application;

/// <summary>
/// Reads and checks the fields of one input record (OOAPI field name → text) and collects errors per field.
/// Field names are case-insensitive; empty text counts as missing.
/// </summary>
internal sealed class FieldReader(IReadOnlyDictionary<string, string?> fields)
{
    private readonly Dictionary<string, string?> _fields = new(fields, StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<string>> Errors { get; } = new(StringComparer.Ordinal);

    public bool IsValid => Errors.Count == 0;

    public void AddError(string field, string message)
    {
        if (!Errors.TryGetValue(field, out var list))
        {
            Errors[field] = list = [];
        }

        list.Add(message);
    }

    public string? Text(string field, int maxLength, bool required = false)
    {
        var value = _fields.TryGetValue(field, out var raw) ? raw?.Trim() : null;
        if (string.IsNullOrEmpty(value))
        {
            if (required)
            {
                AddError(field, "Required.");
            }

            return null;
        }

        if (value.Length > maxLength)
        {
            AddError(field, $"Must be at most {maxLength} characters.");
        }

        return value;
    }

    public DateOnly? Date(string field, bool required = false)
    {
        var text = Text(field, 10, required);
        if (text is null)
        {
            return null;
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        AddError(field, "Not a valid date; use YYYY-MM-DD.");
        return null;
    }

    public decimal? Decimal(string field, decimal min)
    {
        var text = Text(field, 20);
        if (text is null)
        {
            return null;
        }

        if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) && value >= min)
        {
            return value;
        }

        AddError(field, $"Must be a number of at least {min.ToString(CultureInfo.InvariantCulture)}, with a dot as decimal separator.");
        return null;
    }

    public TEnum? Choice<TEnum>(string field, IReadOnlyDictionary<string, TEnum> values, bool required = false)
        where TEnum : struct
    {
        var text = Text(field, 50, required);
        if (text is null)
        {
            return null;
        }

        var match = values.FirstOrDefault(v => string.Equals(v.Key, text, StringComparison.OrdinalIgnoreCase));
        if (match.Key is not null)
        {
            return match.Value;
        }

        AddError(field, $"Must be one of: {string.Join(", ", values.Keys)}.");
        return null;
    }
}
