using System.Net.Sockets;

namespace Doctrina.Infrastructure.Authentication;

/// <summary>
/// Decides whether an exception means "the identity provider can't be reached" (design D2): the exception chain holds a
/// network error and an exception that comes from the authentication stack.
/// </summary>
public static class IdentityProviderFailure
{
    private static readonly string[] AuthenticationSources = ["Microsoft.IdentityModel", "Microsoft.AspNetCore.Authentication"];

    public static bool IsUnreachable(Exception exception) =>
        NetworkError(exception) is not null && Chain(exception).Any(FromAuthentication);

    /// <summary>The innermost (most specific) network error in the chain, or <see langword="null"/>.</summary>
    public static Exception? NetworkError(Exception exception) =>
        Chain(exception).LastOrDefault(e => e is HttpRequestException or SocketException or IOException or TimeoutException);

    private static IEnumerable<Exception> Chain(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            yield return exception;
        }
    }

    // Source is the assembly that threw; the type's namespace also covers exceptions that were created but not thrown
    // (AuthenticationFailureException is thrown by the remote handler, but its Source is only set once it is thrown).
    private static bool FromAuthentication(Exception exception) =>
        AuthenticationSources.Any(source =>
            (exception.Source?.StartsWith(source, StringComparison.Ordinal) ?? false)
            || (exception.GetType().Namespace?.StartsWith(source, StringComparison.Ordinal) ?? false));
}
