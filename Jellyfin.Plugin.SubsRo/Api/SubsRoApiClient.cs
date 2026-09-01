using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Jellyfin.Plugin.SubsRo.Api.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubsRo.Api;

/// <summary>Thin wrapper over the subs.ro API. No business logic; never throws.</summary>
public sealed class SubsRoApiClient
{
    private const string BaseUrl = "https://api.subs.ro/v1.0";
    private const string ApiKeyHeader = "X-Subs-Api-Key";

    private readonly HttpClient _client;
    private readonly ILogger<SubsRoApiClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsRoApiClient"/> class.
    /// </summary>
    /// <param name="client">The HTTP client used to reach subs.ro, expected to be created via <c>AddHttpClient</c> so its lifetime is managed by DI.</param>
    /// <param name="logger">Logger used to record non-fatal request failures; the client never throws for network or parsing errors.</param>
    public SubsRoApiClient(HttpClient client, ILogger<SubsRoApiClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Searches subs.ro for Romanian subtitles matching a single lookup field (for example, an IMDb ID or a title).
    /// </summary>
    /// <param name="field">The subs.ro search field to query, such as "imdbid" or "title".</param>
    /// <param name="value">The value to search for; it is URL-escaped before being sent.</param>
    /// <param name="apiKey">The caller's subs.ro API key, sent via the API key header.</param>
    /// <param name="ct">A token used to cancel the request.</param>
    /// <returns>The matching subtitle entries, or an empty list if the request failed, the response could not be parsed, or subs.ro returned no items.</returns>
    public async Task<IReadOnlyList<SubsRoSubtitleItem>> SearchAsync(
        string field, string value, string apiKey, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"{BaseUrl}/search/{field}/{Uri.EscapeDataString(value)}?language=ro");

        var body = await SendAsync(url, apiKey, ct).ConfigureAwait(false);
        if (body is null)
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<SubsRoSearchResponse>(body);
            return parsed?.Items ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "subs.ro returned a response that could not be parsed");
            return [];
        }
    }

    /// <summary>
    /// Fetches the caller's current daily download quota from subs.ro, used to populate the
    /// read-only quota display on the plugin's configuration page.
    /// </summary>
    /// <param name="apiKey">The caller's subs.ro API key.</param>
    /// <param name="ct">A token used to cancel the request.</param>
    /// <returns>The account's quota figures, or null if the request failed or the response could not be parsed.</returns>
    public async Task<SubsRoQuota?> GetQuotaAsync(string apiKey, CancellationToken ct)
    {
        var body = await SendAsync($"{BaseUrl}/quota", apiKey, ct).ConfigureAwait(false);
        if (body is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SubsRoQuotaResponse>(body)?.Quota;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads a subtitle file or archive from subs.ro. The link may point at a plain subtitle
    /// file or, for series entries, a ZIP archive that the caller must then match against the
    /// requested episode.
    /// </summary>
    /// <param name="downloadLink">The download URL returned by a prior search, as-is.</param>
    /// <param name="apiKey">The caller's subs.ro API key, sent via the API key header.</param>
    /// <param name="ct">A token used to cancel the request.</param>
    /// <returns>The downloaded bytes, or null if the download failed.</returns>
    public async Task<byte[]?> DownloadAsync(string downloadLink, string apiKey, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadLink);
            request.Headers.Add(ApiKeyHeader, apiKey);

            using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("subs.ro download failed with status {Status}", (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }
        catch (FormatException ex)
        {
            // UriFormatException (malformed URL) or FormatException (invalid API key) from Headers.Add
            _logger.LogWarning(ex, "subs.ro download failed due to format error");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "subs.ro download could not be completed");
            return null;
        }
    }

    private async Task<string?> SendAsync(string url, string apiKey, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(ApiKeyHeader, apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("subs.ro request failed with status {Status}", (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (FormatException ex)
        {
            // UriFormatException (malformed URL) or FormatException (invalid API key) from Headers.Add
            _logger.LogWarning(ex, "subs.ro request failed due to format error");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "subs.ro request could not be completed");
            return null;
        }
    }
}
