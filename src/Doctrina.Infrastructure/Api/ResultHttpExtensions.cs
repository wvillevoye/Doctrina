using Doctrina.Core.Results;
using Microsoft.AspNetCore.Http;

namespace Doctrina.Infrastructure.Api;

/// <summary>
/// Vertaalt <see cref="Result"/> naar een HTTP-antwoord (proposal §3.6, spec api-conventions): succes → 200/204,
/// fout → RFC 9457 ProblemDetails met de status van het <see cref="ErrorType"/>, de foutcode in <c>code</c> en bij
/// validatie de fouten per veld in <c>errors</c>. De <c>title</c> wordt vertaald op basis van <c>code</c>.
/// </summary>
public static class ResultHttpExtensions
{
    /// <summary>De HTTP-status bij een fouttype.</summary>
    public static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Succes → 204 No Content; fout → ProblemDetails.</summary>
    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }

    /// <summary>Succes → 200 OK met de waarde; fout → ProblemDetails.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>Succes → <paramref name="onSuccess"/> (bv. 201 Created); fout → ProblemDetails.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.IsSuccess ? onSuccess(result.Value!) : result.Error.ToProblem();
    }

    /// <summary>De fout als ProblemDetails-antwoord.</summary>
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
