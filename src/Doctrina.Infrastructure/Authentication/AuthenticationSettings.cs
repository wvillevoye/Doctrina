namespace Doctrina.Infrastructure.Authentication;

/// <summary>Instellingen voor het inloggen van gebruikers (sectie <c>Authentication:Oidc</c>).</summary>
public sealed class OidcSettings
{
    public const string SectionName = "Authentication:Oidc";

    /// <summary>Issuer-URL van de OIDC-provider, bv. <c>http://localhost:8080/realms/doctrina</c>.</summary>
    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; } = true;

    public IList<string> Scopes { get; } = new List<string>();

    /// <summary>Optioneel: identity provider van een brokerende provider (Keycloak <c>kc_idp_hint</c>), bv. <c>entra</c>.</summary>
    public string? IdpHint { get; set; }

    /// <summary>Maximale duur van een Doctrina-sessie.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(8);

    internal IEnumerable<string> EffectiveScopes => Scopes.Count > 0 ? Scopes : ["openid", "profile", "email"];
}

/// <summary>Instellingen voor bearer-tokens op <c>/api/v1</c> (sectie <c>Authentication:Api</c>).</summary>
public sealed class ApiAuthenticationSettings
{
    public const string SectionName = "Authentication:Api";

    /// <summary>Issuer van access tokens; standaard gelijk aan <see cref="OidcSettings.Authority"/>.</summary>
    public string? Authority { get; set; }

    /// <summary>Verplichte audience (<c>aud</c>), bv. <c>doctrina-api</c>.</summary>
    public string Audience { get; set; } = string.Empty;

    public bool? RequireHttpsMetadata { get; set; }
}
