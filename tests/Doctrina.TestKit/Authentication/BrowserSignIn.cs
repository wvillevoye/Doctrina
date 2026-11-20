using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;

namespace Doctrina.TestKit.Authentication;

/// <summary>Plays the role of browser + provider in the OIDC code flow (without Keycloak).</summary>
public static partial class BrowserSignIn
{
    /// <summary>
    /// Completes a challenge (302 to the provider): the provider issues a code and redirects the browser back to
    /// the redirect URI (form_post or query). Returns Doctrina's response to the callback.
    /// </summary>
    public static async Task<HttpResponseMessage> CompleteSignInAsync(
        this HttpClient browser,
        HttpResponseMessage challenge,
        StubOidcProvider provider,
        StubUser user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(provider);
        var authorize = challenge.Headers.Location
            ?? throw new InvalidOperationException($"Verwachtte een redirect naar de provider, kreeg {(int)challenge.StatusCode}.");
        if (!authorize.ToString().StartsWith(provider.Configuration.AuthorizationEndpoint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Redirect gaat niet naar de provider: {authorize}");
        }

        var query = QueryHelpers.ParseQuery(authorize.Query);
        var code = provider.IssueAuthorizationCode(user, query["nonce"].ToString(), query["client_id"].ToString());
        var callback = new Uri(query["redirect_uri"].ToString());
        var fields = new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = code, ["state"] = query["state"].ToString() };

        if (query["response_mode"] == "form_post")
        {
            using var content = new FormUrlEncodedContent(fields);
            return await browser.PostAsync(new Uri(callback.PathAndQuery, UriKind.Relative), content, cancellationToken);
        }

        return await browser.GetAsync(new Uri(QueryHelpers.AddQueryString(callback.PathAndQuery, fields!), UriKind.Relative), cancellationToken);
    }

    /// <summary>Fetches a page that gives a challenge and signs in immediately; follows the redirect back.</summary>
    public static async Task<string> SignInAndGetAsync(
        this HttpClient browser,
        string path,
        StubOidcProvider provider,
        StubUser user,
        CancellationToken cancellationToken)
    {
        using var challenge = await browser.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
        using var callback = await browser.CompleteSignInAsync(challenge, provider, user, cancellationToken);
        var target = callback.Headers.Location ?? throw new InvalidOperationException("Geen redirect na inloggen.");
        return await browser.GetStringAsync(target.IsAbsoluteUri ? new Uri(target.PathAndQuery, UriKind.Relative) : target, cancellationToken);
    }

    /// <summary>Reads the antiforgery token from a form on the page.</summary>
    public static string ReadAntiforgeryToken(string html)
    {
        var match = AntiforgeryInput().Match(html);
        return match.Success ? match.Groups["value"].Value : throw new InvalidOperationException("Geen antiforgery-token op de pagina.");
    }

    [GeneratedRegex("""<input[^>]*name="__RequestVerificationToken"[^>]*value="(?<value>[^"]+)"|<input[^>]*value="(?<value>[^"]+)"[^>]*name="__RequestVerificationToken" """, RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryInput();
}
