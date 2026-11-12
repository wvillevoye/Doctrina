using System.Globalization;
using Doctrina.Infrastructure.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace Doctrina.Host.Authentication;

/// <summary>
/// Beëindigt interactieve Blazor-circuits waarvan de sessie is verlopen (design D7): de gebruiker moet dan opnieuw inloggen.
/// </summary>
internal sealed class SessionRevalidatingAuthenticationStateProvider(ILoggerFactory loggerFactory, TimeProvider timeProvider)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    protected override Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        var expires = authenticationState.User.FindFirst(DoctrinaClaimTypes.SessionExpires)?.Value;
        var valid = expires is not null
            && DateTimeOffset.TryParse(expires, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt)
            && expiresAt > timeProvider.GetUtcNow();
        return Task.FromResult(valid);
    }
}
