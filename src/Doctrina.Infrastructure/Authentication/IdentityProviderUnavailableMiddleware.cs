using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doctrina.Infrastructure.Authentication;

/// <summary>
/// Turns "identity provider unreachable" during sign-in, the callback or sign-out into a redirect to
/// <see cref="AuthenticationEndpoints.UnavailablePath"/> instead of the generic error page (design D1, D3–D6).
/// Any other exception is rethrown, so the normal error handling still applies.
/// </summary>
public sealed partial class IdentityProviderUnavailableMiddleware(
    RequestDelegate next,
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
    ILogger<IdentityProviderUnavailableMiddleware> logger)
{
    public const string ReturnUrlParameter = "returnUrl";
    public const string SignedOutParameter = "uitgelogd";
    public const string TraceIdParameter = "traceId";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted && IdentityProviderFailure.IsUnreachable(exception))
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        var request = context.Request;
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        string action;
        string returnUrl;
        if (HttpMethods.IsPost(request.Method) && request.Path == AuthenticationEndpoints.SignOutPath)
        {
            action = "sign-out";
            returnUrl = "/";
        }
        else if (request.Path == options.CallbackPath)
        {
            // The original address is inside the encrypted state; the home page is a good enough fallback (D4).
            action = "callback";
            returnUrl = "/";
        }
        else
        {
            action = "sign-in";
            returnUrl = request.Path == AuthenticationEndpoints.SignInPath
                ? request.Query[ReturnUrlParameter].ToString()
                : $"{request.PathBase}{request.Path}{request.QueryString}";
        }

        LogUnreachable(logger, exception, action, Authority(options.Authority), request.Path.Value, traceId,
            IdentityProviderFailure.NetworkError(exception)?.GetType().Name);

        // Throws away whatever the handlers already set, including the cookie handler's sign-out (D5).
        context.Response.Clear();
        var query = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [ReturnUrlParameter] = AuthenticationEndpoints.LocalPathOrRoot(returnUrl),
            [TraceIdParameter] = traceId,
        };
        if (action == "sign-out")
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            query[SignedOutParameter] = "1";
        }

        context.Response.Redirect(QueryHelpers.AddQueryString(AuthenticationEndpoints.UnavailablePath, query));
    }

    // Host (with a non-default port) and path only: never a query string.
    private static string Authority(string? authority) =>
        Uri.TryCreate(authority, UriKind.Absolute, out var uri) ? $"{uri.Authority}{uri.AbsolutePath}" : "(not configured)";

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning,
        Message = "Identity provider {Authority} can't be reached during {Action} of {RequestPath} (trace {TraceId}, {NetworkError}).")]
    private static partial void LogUnreachable(
        ILogger logger, Exception exception, string action, string authority, string? requestPath, string traceId, string? networkError);
}

public static class IdentityProviderUnavailableExtensions
{
    /// <summary>Register after <c>UseExceptionHandler</c> and before <c>UseAuthentication</c>.</summary>
    public static IApplicationBuilder UseDoctrinaIdentityProviderUnavailable(this IApplicationBuilder app) =>
        app.UseMiddleware<IdentityProviderUnavailableMiddleware>();
}
