namespace Doctrina.Core.Results;

/// <summary>Kind of expected error; determines the HTTP status the API translates to (proposal §3.6).</summary>
public enum ErrorType
{
    /// <summary>The input is invalid (HTTP 400, with errors per field).</summary>
    Validation,

    /// <summary>The requested resource does not exist (HTTP 404).</summary>
    NotFound,

    /// <summary>Conflict with the current state, e.g. a duplicate <c>ExternalId</c> (HTTP 409).</summary>
    Conflict,

    /// <summary>The caller is known but not allowed (HTTP 403).</summary>
    Forbidden,

    /// <summary>The caller is not (validly) authenticated (HTTP 401).</summary>
    Unauthorized,

    /// <summary>Any other expected error (HTTP 500).</summary>
    Failure,
}

/// <summary>
/// An expected error with a stable, module-prefixed code (e.g. <c>Academic.Course.NotFound</c>).
/// </summary>
/// <param name="Code">Stable error code with module prefix; key for translated titles.</param>
/// <param name="Message">Description for developers; fallback text if there is no translation.</param>
/// <param name="Type">Kind of error.</param>
/// <param name="Details">Optional: errors per field (only for <see cref="ErrorType.Validation"/>).</param>
public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    /// <summary>Creates a <see cref="ErrorType.NotFound"/> error.</summary>
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    /// <summary>Creates a <see cref="ErrorType.Validation"/> error, optionally with errors per field.</summary>
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(code, message, ErrorType.Validation, details);

    /// <summary>Creates a <see cref="ErrorType.Conflict"/> error.</summary>
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>Creates a <see cref="ErrorType.Forbidden"/> error.</summary>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    /// <summary>Creates a <see cref="ErrorType.Unauthorized"/> error.</summary>
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    /// <summary>Creates a <see cref="ErrorType.Failure"/> error.</summary>
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
