using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Doctrina.TestKit.Authentication;

/// <summary>Een testgebruiker zoals de provider hem in het ID-token zet.</summary>
public sealed record StubUser(string Subject, string Name, IReadOnlyList<string> Roles, string? Email = null, string? PersonId = null)
{
    public static StubUser Student { get; } = new("sub-student", "Sanne Student", ["student"], "student@example.test", "I6000001");

    public static StubUser Beheerder { get; } = new("sub-beheerder", "Beheer Doctrina", ["admin", "employee"], "beheerder@example.test", "P7000001");
}

/// <summary>
/// Nagebootste OpenID Connect-provider in het geheugen: metadata, autorisatiecodes, token-endpoint en ondertekende tokens.
/// Vormt tokens zoals de dev-stack (Keycloak) en Entra ID: platte <c>roles</c>-claim, <c>name</c>, <c>azp</c>.
/// </summary>
public sealed class StubOidcProvider : IDisposable
{
    public const string Issuer = "https://idp.test/realms/doctrina";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly ConcurrentDictionary<string, PendingSignIn> _codes = new(StringComparer.Ordinal);
    private int _codeCounter;

    public StubOidcProvider()
    {
        SigningKey = new RsaSecurityKey(_rsa) { KeyId = "stub-signing-key" };
        Configuration = new OpenIdConnectConfiguration
        {
            Issuer = Issuer,
            AuthorizationEndpoint = $"{Issuer}/protocol/openid-connect/auth",
            TokenEndpoint = $"{Issuer}/protocol/openid-connect/token",
            EndSessionEndpoint = $"{Issuer}/protocol/openid-connect/logout",
            JwksUri = $"{Issuer}/protocol/openid-connect/certs",
        };
        Configuration.SigningKeys.Add(SigningKey);
        Configuration.ResponseTypesSupported.Add("code");
    }

    public RsaSecurityKey SigningKey { get; }

    public OpenIdConnectConfiguration Configuration { get; }

    /// <summary>Wat de provider doet na een geslaagde login: een code uitgeven voor deze gebruiker.</summary>
    public string IssueAuthorizationCode(StubUser user, string nonce, string clientId)
    {
        var code = $"code-{Interlocked.Increment(ref _codeCounter)}";
        _codes[code] = new PendingSignIn(user, nonce, clientId);
        return code;
    }

    /// <summary>Access token zoals bij client credentials of namens een gebruiker.</summary>
    public string CreateAccessToken(
        string audience,
        string clientId,
        IEnumerable<string> roles,
        string? subject = null,
        string? name = null,
        string issuer = Issuer,
        TimeSpan? lifetime = null)
    {
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["sub"] = subject ?? $"service-account-{clientId}",
            ["azp"] = clientId,
            ["roles"] = roles.ToArray(),
        };
        if (name is not null)
        {
            claims["name"] = name;
        }

        return CreateToken(issuer, audience, claims, lifetime ?? TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Switch for the scenarios where the provider can't be reached (design D7 of add-idp-unavailable-notice): when
    /// <see langword="false"/>, fetching the configuration and calling the token endpoint fail with a network error.
    /// </summary>
    public bool IsReachable { get; set; } = true;

    /// <summary>When <see langword="false"/>, only the token endpoint fails (the callback after signing in at the provider).</summary>
    public bool IsTokenEndpointReachable { get; set; } = true;

    /// <summary>HttpClient voor het backchannel van de OIDC-handler (token-endpoint).</summary>
    public HttpClient CreateBackchannelClient() => new(new TokenEndpointHandler(this), disposeHandler: true);

    /// <summary>
    /// Configuration for the OIDC and bearer handlers: the static configuration while <see cref="IsReachable"/>, otherwise
    /// a real <see cref="ConfigurationManager{T}"/> whose HTTP calls fail, so the exception comes from
    /// Microsoft.IdentityModel just like in production.
    /// </summary>
    public IConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager() => new SwitchableConfigurationManager(this);

    /// <summary>A real configuration manager for this provider that can't reach it.</summary>
    public static ConfigurationManager<OpenIdConnectConfiguration> CreateUnreachableConfigurationManager() =>
        new($"{Issuer}/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever(new HttpClient(new UnreachableHandler())));

    internal static HttpRequestException ConnectionRefused() =>
        new("Connection refused (idp.test:443)", new SocketException((int)SocketError.ConnectionRefused));

    public void Dispose() => _rsa.Dispose();

    private string CreateIdToken(PendingSignIn signIn)
    {
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["sub"] = signIn.User.Subject,
            ["oid"] = signIn.User.Subject,
            ["name"] = signIn.User.Name,
            ["preferred_username"] = signIn.User.Subject,
            ["roles"] = signIn.User.Roles.ToArray(),
            ["nonce"] = signIn.Nonce,
            ["azp"] = signIn.ClientId,
        };
        if (signIn.User.Email is not null)
        {
            claims["email"] = signIn.User.Email;
        }

        if (signIn.User.PersonId is not null)
        {
            claims["personId"] = signIn.User.PersonId;
        }

        return CreateToken(Issuer, signIn.ClientId, claims, TimeSpan.FromMinutes(5));
    }

    private string CreateToken(string issuer, string audience, Dictionary<string, object> claims, TimeSpan lifetime)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now,
            NotBefore = now.AddSeconds(-5),
            Expires = now.Add(lifetime),
            Claims = claims,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256),
        });
    }

    private sealed record PendingSignIn(StubUser User, string Nonce, string ClientId);

    private sealed class UnreachableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw ConnectionRefused();
    }

    private sealed class SwitchableConfigurationManager(StubOidcProvider provider) : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private readonly StaticConfigurationManager<OpenIdConnectConfiguration> _reachable = new(provider.Configuration);
        private readonly ConfigurationManager<OpenIdConnectConfiguration> _unreachable = CreateUnreachableConfigurationManager();

        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) =>
            provider.IsReachable ? _reachable.GetConfigurationAsync(cancel) : _unreachable.GetConfigurationAsync(cancel);

        public void RequestRefresh()
        {
        }
    }

    private sealed class TokenEndpointHandler(StubOidcProvider provider) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!provider.IsReachable || !provider.IsTokenEndpointReachable)
            {
                throw ConnectionRefused();
            }

            if (request.Method != HttpMethod.Post || request.RequestUri?.ToString() != provider.Configuration.TokenEndpoint || request.Content is null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var form = (await request.Content.ReadAsStringAsync(cancellationToken))
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => p.Length > 1 ? Uri.UnescapeDataString(p[1].Replace('+', ' ')) : string.Empty, StringComparer.Ordinal);

            if (!form.TryGetValue("grant_type", out var grant) || grant != "authorization_code"
                || !form.TryGetValue("code", out var code) || !provider._codes.TryRemove(code, out var signIn))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { error = "invalid_grant" }) };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["access_token"] = provider.CreateAccessToken("doctrina-api", signIn.ClientId, signIn.User.Roles, signIn.User.Subject, signIn.User.Name),
                    ["id_token"] = provider.CreateIdToken(signIn),
                    ["token_type"] = "Bearer",
                    ["expires_in"] = 300,
                }),
            };
        }
    }
}
