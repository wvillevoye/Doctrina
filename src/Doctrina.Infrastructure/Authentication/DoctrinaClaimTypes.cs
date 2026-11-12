namespace Doctrina.Infrastructure.Authentication;

/// <summary>
/// Claimnamen zoals de provider ze uitgeeft (Keycloak, Entra ID en SURFconext); er wordt niets hernoemd.
/// </summary>
public static class DoctrinaClaimTypes
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Roles = "roles";
    public const string ObjectId = "oid";
    public const string Email = "email";
    public const string AuthorizedParty = "azp";

    /// <summary>Persoonsnummer van de instelling (studenten <c>I6…</c>, medewerkers <c>P7…</c>); naam volgt OOAPI <c>personId</c>.</summary>
    public const string PersonId = "personId";

    /// <summary>Door Doctrina toegevoegd: een permissie van de aanroeper, afgeleid uit de rollen (design D8).</summary>
    public const string Permission = "doctrina:permission";

    /// <summary>Door Doctrina toegevoegd: einde van de sessie (ISO 8601, UTC).</summary>
    public const string SessionExpires = "doctrina:session_expires";
}

/// <summary>Namen van autorisatiepolicies die de host gebruikt.</summary>
public static class DoctrinaPolicies
{
    /// <summary>Geauthenticeerde aanroeper van <c>/api/v1</c> met een bearer-token (nooit de sessiecookie).</summary>
    public const string ApiCaller = "Doctrina.ApiCaller";
}
