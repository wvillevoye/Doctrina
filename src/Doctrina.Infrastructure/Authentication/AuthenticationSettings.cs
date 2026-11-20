namespace Doctrina.Infrastructure.Authentication;

/// <summary>Settings for user sign-in (section <c>Authentication:Oidc</c>).</summary>
public sealed class OidcSettings
{
    public const string SectionName = "Authentication:Oidc";

    /// <summary>Issuer URL of the OIDC provider, e.g. <c>http://localhost:8080/realms/doctrina</c>.</summary>
    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; } = true;

    public IList<string> Scopes { get; } = new List<string>();

    /// <summary>Optional: identity provider of a brokering provider (Keycloak <c>kc_idp_hint</c>), e.g. <c>entra</c>.</summary>
    public string? IdpHint { get; set; }

    /// <summary>Maximum duration of a Doctrina session.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(8);

    internal IEnumerable<string> EffectiveScopes => Scopes.Count > 0 ? Scopes : ["openid", "profile", "email"];
}

/// <summary>Settings for bearer tokens on <c>/api/v1</c> (section <c>Authentication:Api</c>).</summary>
public sealed class ApiAuthenticationSettings
{
    public const string SectionName = "Authentication:Api";

    /// <summary>Issuer of access tokens; by default equal to <see cref="OidcSettings.Authority"/>.</summary>
    public string? Authority { get; set; }

    /// <summary>Required audience (<c>aud</c>), e.g. <c>doctrina-api</c>.</summary>
    public string Audience { get; set; } = string.Empty;

    public bool? RequireHttpsMetadata { get; set; }
}
