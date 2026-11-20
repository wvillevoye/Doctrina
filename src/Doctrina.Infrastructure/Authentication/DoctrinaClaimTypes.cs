namespace Doctrina.Infrastructure.Authentication;

/// <summary>
/// Claim names as the provider issues them (Keycloak, Entra ID and SURFconext); nothing is renamed.
/// </summary>
public static class DoctrinaClaimTypes
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Roles = "roles";
    public const string ObjectId = "oid";
    public const string Email = "email";
    public const string AuthorizedParty = "azp";

    /// <summary>Person number of the institution (students <c>I6…</c>, staff <c>P7…</c>); the name follows OOAPI <c>personId</c>.</summary>
    public const string PersonId = "personId";

    /// <summary>Added by Doctrina: a permission of the caller, derived from the roles (design D8).</summary>
    public const string Permission = "doctrina:permission";

    /// <summary>Added by Doctrina: end of the session (ISO 8601, UTC).</summary>
    public const string SessionExpires = "doctrina:session_expires";
}

/// <summary>Names of the authorization policies the host uses.</summary>
public static class DoctrinaPolicies
{
    /// <summary>Authenticated caller of <c>/api/v1</c> with a bearer token (never the session cookie).</summary>
    public const string ApiCaller = "Doctrina.ApiCaller";
}
