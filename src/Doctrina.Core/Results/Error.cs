namespace Doctrina.Core.Results;

/// <summary>Soort verwachte fout; bepaalt de HTTP-status waarnaar de API vertaalt (proposal §3.6).</summary>
public enum ErrorType
{
    /// <summary>Invoer is ongeldig (HTTP 400, met fouten per veld).</summary>
    Validation,

    /// <summary>De gevraagde resource bestaat niet (HTTP 404).</summary>
    NotFound,

    /// <summary>Conflict met de huidige toestand, bv. een dubbele <c>ExternalId</c> (HTTP 409).</summary>
    Conflict,

    /// <summary>De aanroeper is bekend maar heeft geen recht (HTTP 403).</summary>
    Forbidden,

    /// <summary>De aanroeper is niet (geldig) geauthenticeerd (HTTP 401).</summary>
    Unauthorized,

    /// <summary>Overige verwachte fout (HTTP 500).</summary>
    Failure,
}

/// <summary>
/// Een verwachte fout met een stabiele, module-geprefixte code (bv. <c>Academic.Course.NotFound</c>).
/// </summary>
/// <param name="Code">Stabiele foutcode met module-prefix; sleutel voor vertaalde titels.</param>
/// <param name="Message">Omschrijving voor ontwikkelaars; terugvaltekst als er geen vertaling is.</param>
/// <param name="Type">Soort fout.</param>
/// <param name="Details">Optioneel: fouten per veld (alleen bij <see cref="ErrorType.Validation"/>).</param>
public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    /// <summary>Maakt een <see cref="ErrorType.NotFound"/>-fout.</summary>
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    /// <summary>Maakt een <see cref="ErrorType.Validation"/>-fout, optioneel met fouten per veld.</summary>
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(code, message, ErrorType.Validation, details);

    /// <summary>Maakt een <see cref="ErrorType.Conflict"/>-fout.</summary>
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>Maakt een <see cref="ErrorType.Forbidden"/>-fout.</summary>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    /// <summary>Maakt een <see cref="ErrorType.Unauthorized"/>-fout.</summary>
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    /// <summary>Maakt een <see cref="ErrorType.Failure"/>-fout.</summary>
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
