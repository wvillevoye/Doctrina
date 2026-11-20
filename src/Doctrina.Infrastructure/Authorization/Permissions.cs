using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Doctrina.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Doctrina.Infrastructure.Authorization;

/// <summary>Policy names for permissions: <c>Permission:&lt;name&gt;</c> (design D8).</summary>
public static class PermissionPolicy
{
    public const string Prefix = "Permission:";

    public static string For(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return Prefix + permission;
    }

    public static bool TryGetPermission(string policyName, [NotNullWhen(true)] out string? permission)
    {
        ArgumentNullException.ThrowIfNull(policyName);
        permission = policyName.StartsWith(Prefix, StringComparison.Ordinal) ? policyName[Prefix.Length..] : null;
        return !string.IsNullOrEmpty(permission);
    }
}

/// <summary>Requires a permission for a Razor page or component: <c>@attribute [RequirePermission("Sample.Manage")]</c>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute(PermissionPolicy.For(permission))
{
    public string Permission { get; } = permission;
}

/// <summary>Turns <c>Permission:&lt;name&gt;</c> into a policy; other names go to the default provider.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (PermissionPolicy.TryGetPermission(policyName, out var permission))
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(DoctrinaClaimTypes.Permission, permission)
                .Build();
        }

        return await base.GetPolicyAsync(policyName).ConfigureAwait(false);
    }
}

/// <summary>
/// Adds, once per request, the permissions that belong to the <c>roles</c> claim, as
/// <see cref="DoctrinaClaimTypes.Permission"/> claims in a separate identity. Permission claims that come from outside
/// (e.g. from a token) are removed: only Doctrina determines permissions.
/// </summary>
public sealed class PermissionClaimsTransformation(PermissionRegistry registry) : IClaimsTransformation
{
    public const string IdentityType = "Doctrina.Permissions";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identity?.IsAuthenticated != true || principal.Identities.Any(i => i.AuthenticationType == IdentityType))
        {
            return Task.FromResult(principal);
        }

        var transformed = principal.Clone();
        foreach (var identity in transformed.Identities)
        {
            foreach (var claim in identity.FindAll(DoctrinaClaimTypes.Permission).ToList())
            {
                identity.TryRemoveClaim(claim);
            }
        }

        var permissions = registry.PermissionsFor(transformed.FindAll(DoctrinaClaimTypes.Roles).Select(c => c.Value));
        transformed.AddIdentity(new ClaimsIdentity(
            permissions.Select(p => new Claim(DoctrinaClaimTypes.Permission, p)),
            IdentityType));
        return Task.FromResult(transformed);
    }
}

/// <summary>Registration and use of permissions.</summary>
public static class PermissionExtensions
{
    /// <summary>Claims transformation (roles → permissions) and dynamic permission policies.</summary>
    public static IServiceCollection AddDoctrinaAuthorization(this IServiceCollection services)
    {
        services.AddTransient<IClaimsTransformation, PermissionClaimsTransformation>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        return services;
    }

    /// <summary>Requires <paramref name="permission"/> for an endpoint or group.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(PermissionPolicy.For(permission));
}

/// <summary>ProblemDetails for rejected requests.</summary>
public static class AuthorizationProblemDetails
{
    /// <summary>
    /// Stable error codes for bare 401/403 responses (spec access-control: <c>Auth.Forbidden</c>), so that rejected
    /// API calls also get a ProblemDetails body with <c>code</c>.
    /// </summary>
    public static void AddErrorCode(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ProblemDetails.Extensions.ContainsKey("code"))
        {
            return;
        }

        var code = context.HttpContext.Response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => "Auth.Unauthorized",
            StatusCodes.Status403Forbidden => "Auth.Forbidden",
            _ => null,
        };
        if (code is not null)
        {
            context.ProblemDetails.Extensions["code"] = code;
        }
    }
}
