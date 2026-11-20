using Doctrina.Core.Results;
using Microsoft.AspNetCore.Http;

namespace Doctrina.Infrastructure.Api;

/// <summary>
/// Translates <see cref="Result"/> into an HTTP response (proposal §3.6, spec api-conventions): success → 200/204,
/// failure → RFC 9457 ProblemDetails with the status of the <see cref="ErrorType"/>, the error code in <c>code</c> and, for
/// validation, the errors per field in <c>errors</c>. The <c>title</c> is translated based on <c>code</c>.
/// </summary>
public static class ResultHttpExtensions
{
    /// <summary>The HTTP status for an error type.</summary>
    public static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Success → 204 No Content; failure → ProblemDetails.</summary>
    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }

    /// <summary>Success → 200 OK with the value; failure → ProblemDetails.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>Success → <paramref name="onSuccess"/> (e.g. 201 Created); failure → ProblemDetails.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.IsSuccess ? onSuccess(result.Value!) : result.Error.ToProblem();
    }

    /// <summary>The error as a ProblemDetails response.</summary>
    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = error.Code };

        if (error.Type == ErrorType.Validation)
        {
            return TypedResults.ValidationProblem(
                error.Details ?? new Dictionary<string, string[]>(StringComparer.Ordinal),
                title: error.Message,
                extensions: extensions);
        }

        return TypedResults.Problem(title: error.Message, statusCode: StatusCodeFor(error.Type), extensions: extensions);
    }
}
