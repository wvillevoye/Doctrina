using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Doctrina.Modules.Academic.Contracts;

namespace Doctrina.Modules.Interop.OOAPI.Client;

/// <summary>
/// Reads one OOAPI v5 source (design D4, D8 of add-ooapi-consumer): a client-credentials token, reused until shortly before it
/// expires, and pages followed by <c>hasNextPage</c>. A network error, time-out, 429 or 5xx is tried again, three attempts in
/// all; any other error ends the read. Messages carry the resource and the status code, never a URL, token or secret.
/// One instance per run.
/// </summary>
internal sealed class OoapiClient(
    HttpClient http, OoapiSourceOptions source, TimeProvider time, Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    public const string HttpClientName = "ooapi-sources";

    internal const int Attempts = 3;

    private static readonly TimeSpan TokenMargin = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(1);
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;
    private string? _token;
    private DateTimeOffset _tokenExpires;

    /// <summary>All items of a list, page after page. <paramref name="label"/> names the list in a failure message.</summary>
    public async IAsyncEnumerable<JsonElement> ItemsAsync(
        string path, string label, [EnumeratorCancellation] CancellationToken cancellationToken, params (string Name, string Value)[] query)
    {
        for (var page = 1; ; page++)
        {
            var url = $"{source.BaseUrl.TrimEnd('/')}/{path}?pageNumber={page.ToString(CultureInfo.InvariantCulture)}&pageSize={source.PageSize.ToString(CultureInfo.InvariantCulture)}"
                + string.Concat(query.Select(q => $"&{Uri.EscapeDataString(q.Name)}={Uri.EscapeDataString(q.Value)}"));
            using var document = await SendAsync(label, async ct => await Authorized(url, ct), ReadJsonAsync, cancellationToken);
            var root = document.RootElement;
            var any = false;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    any = true;
                    yield return item.Clone();
                }
            }

            // An empty page ends the list too, so a source that keeps saying "more" cannot keep us here forever.
            if (!any || !root.TryGetProperty("hasNextPage", out var next) || next.ValueKind != JsonValueKind.True)
            {
                yield break;
            }
        }
    }

    private async Task<HttpRequestMessage> Authorized(string url, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(ct));
        return request;
    }

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        if (_token is not null && time.GetUtcNow() < _tokenExpires)
        {
            return _token;
        }

        using var document = await SendAsync("token request", _ => Task.FromResult(TokenRequest()), ReadJsonAsync, ct);
        var root = document.RootElement;
        _token = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("access_token", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()
            : throw new RecordSourceException("token request: the response has no access_token.");
        var lifetime = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds) : DefaultTokenLifetime;
        _tokenExpires = time.GetUtcNow() + lifetime - TokenMargin;
        return _token!;
    }

    private HttpRequestMessage TokenRequest()
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = source.ClientId,
            ["client_secret"] = source.ClientSecret,
        };
        if (!string.IsNullOrWhiteSpace(source.Scope))
        {
            form["scope"] = source.Scope;
        }

        return new HttpRequestMessage(HttpMethod.Post, source.TokenUrl) { Content = new FormUrlEncodedContent(form) };
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        }
        catch (JsonException)
        {
            throw new RecordSourceException("The response is not valid JSON.");
        }
    }

    private async Task<T> SendAsync<T>(
        string label, Func<CancellationToken, Task<HttpRequestMessage>> build, Func<HttpResponseMessage, CancellationToken, Task<T>> read, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            string failure;
            TimeSpan? retryAfter = null;
            try
            {
                using var request = await build(ct);
                using var response = await http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    return await read(response, ct);
                }

                var status = (int)response.StatusCode;
                if (response.StatusCode != HttpStatusCode.TooManyRequests && status < 500)
                {
                    throw new RecordSourceException($"{label}: HTTP {status}.");
                }

                failure = $"HTTP {status}";
                retryAfter = response.Headers.RetryAfter?.Delta;
            }
            catch (HttpRequestException)
            {
                failure = "connection failed";
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                failure = "time-out";
            }

            if (attempt >= Attempts)
            {
                throw new RecordSourceException($"{label}: {failure} after {Attempts} attempts.");
            }

            // 1 s, then 2 s, unless the source says how long to wait.
            await _delay(Min(retryAfter ?? TimeSpan.FromSeconds(attempt), MaxWait), ct);
        }
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
