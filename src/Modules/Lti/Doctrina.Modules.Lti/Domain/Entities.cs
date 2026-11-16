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

    /// <summary>Where the deep linking request is sent (null: the tool has no deep linking).</summary>
    public string? DeepLinkingUrl { get; set; }

    /// <summary>The tool's JWKS, to check its deep linking response; set together with <see cref="DeepLinkingUrl"/>.</summary>
    public string? PublicKeysUrl { get; set; }

    /// <summary>The tool may use Assignment and Grade Services (line items, scores, results); needs <see cref="PublicKeysUrl"/>.</summary>
    public bool GradeServices { get; set; }

    /// <summary>The tool may read the course membership (NRPS); needs <see cref="PublicKeysUrl"/>.</summary>
    public bool MembershipServices { get; set; }

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

/// <summary>
/// The items a tool chose in a deep linking response, kept until the teacher confirms them (design D4 of
/// add-lti-deep-linking). Internal and short-lived: never exchanged with other systems.
/// </summary>
public sealed class DeepLinkSelection : Entity
{
    public Guid ToolId { get; set; }

    public Guid SectionId { get; set; }

    /// <summary>The opaque user id from the deep linking data; only that user may confirm.</summary>
    public required string Subject { get; set; }

    /// <summary>The chosen items (title and address) as JSON.</summary>
    public required string ItemsJson { get; set; }

    public DateTimeOffset Expires { get; set; }
}

/// <summary>
/// A <c>nonce</c> or <c>jti</c> a tool used in a signed message (a deep linking response, a client assertion), to refuse a
/// replay (design D3 of add-lti-deep-linking, D1 of add-lti-ags). One row per tool, purpose and value.
/// </summary>
public sealed class JwtNonce : Entity
{
    public Guid ToolId { get; set; }

    /// <summary>What the value was used for, see <see cref="NoncePurpose"/>.</summary>
    public required string Purpose { get; set; }

    public required string Nonce { get; set; }

    public DateTimeOffset Expires { get; set; }
}

/// <summary>The kinds of message a <see cref="JwtNonce"/> belongs to.</summary>
public static class NoncePurpose
{
    public const string DeepLinking = "deeplink";
    public const string ClientAssertion = "assertion";
}

/// <summary>An Assignment and Grade Services line item: a gradable column a tool keeps in an offering (design D5 of add-lti-ags).</summary>
public sealed class LineItem : ExchangeableEntity
{
    public Guid ToolId { get; set; }

    public Guid OfferingId { get; set; }

    /// <summary>The tool link (content item) this line item belongs to, if any.</summary>
    public Guid? ResourceLinkId { get; set; }

    public required string Label { get; set; }

    public decimal ScoreMaximum { get; set; }

    public string? ResourceId { get; set; }

    public string? Tag { get; set; }

    public DateTimeOffset? StartDateTime { get; set; }

    public DateTimeOffset? EndDateTime { get; set; }
}

/// <summary>The score of one person on a line item; a newer timestamp replaces it. Internal: shown to the person and the teacher only.</summary>
public sealed class Score : Entity
{
    public Guid LineItemId { get; set; }

    /// <summary>The person (the <c>userId</c> of the tool is the person id).</summary>
    public Guid PersonId { get; set; }

    public decimal? ScoreGiven { get; set; }

    public decimal ScoreMaximum { get; set; }

    public string? Comment { get; set; }

    public ActivityProgress ActivityProgress { get; set; }

    public GradingProgress GradingProgress { get; set; }

    /// <summary>When the tool says the score was given; decides which of two scores is newer.</summary>
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>How far the student is in the activity (AGS <c>activityProgress</c>).</summary>
public enum ActivityProgress { Initialized, Started, InProgress, Submitted, Completed }

/// <summary>How far grading is (AGS <c>gradingProgress</c>).</summary>
public enum GradingProgress { FullyGraded, Pending, PendingManual, Failed, NotReady }
