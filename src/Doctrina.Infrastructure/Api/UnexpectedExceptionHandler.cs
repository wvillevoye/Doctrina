using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Doctrina.Infrastructure.Api;

/// <summary>
/// Onverwachte fouten in de API (spec api-conventions "Onverwachte fouten lekken geen details"): status 500 met een
/// generieke ProblemDetails (<c>code</c> <c>Server.Unexpected</c>, <c>traceId</c>). Buiten Development zonder
/// exceptionmelding of stacktrace. De log bevat dezelfde trace-id, het exceptiontype, de route en de stacktrace, maar
/// geen exceptionmelding of URL met waarden: die kunnen persoonsgegevens bevatten (AVG).
/// </summary>
public sealed partial class UnexpectedExceptionHandler(
    IProblemDetailsService problemDetails,
    IHostEnvironment environment,
    ILogger<UnexpectedExceptionHandler> logger) : IExceptionHandler
{
    public const string Code = "Server.Unexpected";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // De exception-middleware wist endpoint en pad (bij een foutpagina is het pad al /error); het oorspronkelijke
        // verzoek staat in de feature. Pagina's krijgen geen JSON maar de foutpagina van de host.
        var feature = httpContext.Features.Get<IExceptionHandlerFeature>();
        var path = feature?.Path ?? httpContext.Request.Path.Value;
        if (path is null || !path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var endpoint = feature?.Endpoint ?? httpContext.GetEndpoint();
        var route = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "(onbekend)";
        LogUnexpected(logger, traceId, httpContext.Request.Method, route, exception.GetType().FullName, exception.StackTrace);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Er is een onverwachte fout opgetreden.",
            Extensions = { ["code"] = Code, ["traceId"] = traceId },
        };
        if (environment.IsDevelopment())
        {
            problem.Detail = exception.ToString();
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        }).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Onverwachte fout (traceId {TraceId}) bij {Method} {Route}: {ExceptionType}\n{StackTrace}")]
    private static partial void LogUnexpected(ILogger logger, string traceId, string method, string route, string? exceptionType, string? stackTrace);
}
