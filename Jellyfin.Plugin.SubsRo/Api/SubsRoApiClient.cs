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

    public SubsRoApiClient(HttpClient client, ILogger<SubsRoApiClient> logger)
    {
        _client = client;
        _logger = logger;
    }

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

    public async Task<byte[]?> DownloadAsync(string downloadLink, string apiKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, downloadLink);
        request.Headers.Add(ApiKeyHeader, apiKey);

        try
        {
            using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("subs.ro download failed with status {Status}", (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "subs.ro download could not be completed");
            return null;
        }
    }

    private async Task<string?> SendAsync(string url, string apiKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(ApiKeyHeader, apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("subs.ro request failed with status {Status}", (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "subs.ro request could not be completed");
            return null;
        }
    }
}
