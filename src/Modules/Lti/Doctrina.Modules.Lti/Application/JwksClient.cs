using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Doctrina.Modules.Lti.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Doctrina.Modules.Lti.Application;

/// <summary>
/// Fetches the JWKS of a tool from the <c>PublicKeysUrl</c> a beheerder registered (design D6 of add-lti-deep-linking):
/// only https (or http for localhost), a short timeout, no redirects, at most 64 kB and JSON. The keys are cached for
/// 5 minutes; a refresh (for an unknown <c>kid</c>) is honoured at most once a minute per tool.
/// </summary>
internal sealed class JwksClient(HttpClient http, IMemoryCache cache, TimeProvider time)
{
    public const int MaxBytes = 64 * 1024;
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MinimumRefreshInterval = TimeSpan.FromMinutes(1);

    private sealed record Entry(JsonWebKeySet Keys, DateTimeOffset FetchedAt);

    /// <summary>The keys of the tool, or <see langword="null"/> if they cannot be fetched or are not valid.</summary>
    public async Task<JsonWebKeySet?> GetAsync(LtiTool tool, bool refresh, CancellationToken ct)
    {
        if (!LtiUrl.IsValid(tool.PublicKeysUrl))
        {
            return null;
        }

        var key = "lti-jwks:" + tool.Id.ToString("N");
        var now = time.GetUtcNow();
        var cached = cache.TryGetValue(key, out Entry? entry) ? entry : null;
        if (cached is not null && (!refresh || now - cached.FetchedAt < MinimumRefreshInterval))
        {
            return cached.Keys;
        }

        var fetched = await FetchAsync(tool.PublicKeysUrl!, ct);
        if (fetched is null)
        {
            return cached?.Keys;
        }

        cache.Set(key, new Entry(fetched, now), CacheLifetime);
        return fetched;
    }

    private async Task<JsonWebKeySet?> FetchAsync(string url, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType is not ("application/json" or "application/jwk-set+json"))
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[MaxBytes + 1];
            var length = 0;
            int read;
            while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), ct)) > 0)
            {
                length += read;
            }

            return length > MaxBytes ? null : new JsonWebKeySet(Encoding.UTF8.GetString(buffer, 0, length));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null; // the timeout of the HttpClient
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
