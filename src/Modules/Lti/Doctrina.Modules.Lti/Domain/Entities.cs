using Doctrina.Core.Entities;

namespace Doctrina.Modules.Lti.Domain;

// Data model of the Lti module (design D3 of add-lti-platform).

/// <summary>A registered external LTI 1.3 tool.</summary>
public sealed class LtiTool : ExchangeableEntity
{
    public required string Name { get; set; }

    /// <summary>Opaque id Doctrina gives the tool (<c>aud</c> of the launch token).</summary>
    public required string ClientId { get; set; }

    public required string DeploymentId { get; set; }

    /// <summary>OIDC third-party login initiation URL of the tool.</summary>
    public required string LoginUrl { get; set; }

    /// <summary>The tool's launch address (<c>target_link_uri</c>).</summary>
    public required string LaunchUrl { get; set; }

    /// <summary>The addresses the tool may be sent to, one per line.</summary>
    public required string RedirectUris { get; set; }

    /// <summary>Share name, given name and family name with this tool.</summary>
    public bool SendName { get; set; }

    /// <summary>Share the e-mail address with this tool.</summary>
    public bool SendEmail { get; set; }
}

/// <summary>An RSA signing key pair of the platform; the private key is encrypted with Data Protection.</summary>
public sealed class LtiKey : ExchangeableEntity
{
    public required string Kid { get; set; }

    /// <summary>The PKCS#8 private key, encrypted.</summary>
    public required byte[] PrivateKey { get; set; }

    /// <summary>The public key as the JWK members <c>n</c> and <c>e</c> in JSON.</summary>
    public required string PublicKey { get; set; }

    /// <summary>Signs new launches; at most one key is active.</summary>
    public bool Active { get; set; }

    /// <summary>When the key stopped being active (it stays in the JWKS for a while).</summary>
    public DateTimeOffset? RetiredAt { get; set; }
}
