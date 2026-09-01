using Jellyfin.Plugin.SubsRo.Api;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubsRo;

/// <summary>
/// Subtitle provider Jellyfin calls into to search subs.ro for Romanian subtitles.
/// Lookup precedence is IMDb id, then TMDb id, then title, because a bare title
/// search can return dozens of unrelated results spanning decades.
/// </summary>
public class SubsRoSubtitleProvider : ISubtitleProvider
{
    private readonly SubsRoApiClient _client;
    private readonly ILogger<SubsRoSubtitleProvider> _logger;
    private readonly IMemoryCache _searchCache;
    private readonly ArchiveCache _archives;

    // ApplicationPaths is protected on BasePlugin, so it cannot be reached
    // through Plugin.Instance from here. It is injected instead.
    /// <summary>
    /// Initializes a new instance of the <see cref="SubsRoSubtitleProvider"/> class.
    /// </summary>
    /// <param name="client">The subs.ro API client used to search for and download subtitles.</param>
    /// <param name="logger">Logger used to record non-fatal search failures; this provider never throws from <see cref="Search"/>.</param>
    /// <param name="searchCache">In-memory cache used to avoid repeating identical searches within a short window.</param>
    /// <param name="applicationPaths">Jellyfin's application paths, used to locate the on-disk archive cache directory.</param>
    public SubsRoSubtitleProvider(
        SubsRoApiClient client,
        ILogger<SubsRoSubtitleProvider> logger,
        IMemoryCache searchCache,
        IApplicationPaths applicationPaths)
    {
        _client = client;
        _logger = logger;
        _searchCache = searchCache;
        _archives = new ArchiveCache(Path.Combine(applicationPaths.CachePath, "subsro"));
    }

    /// <summary>Gets the name shown for this provider in Jellyfin's subtitle search UI.</summary>
    public string Name => "Subs.ro";

    /// <summary>Gets the media types this provider can search subtitles for: movies and episodes.</summary>
    public IEnumerable<VideoContentType> SupportedMediaTypes =>
        [VideoContentType.Movie, VideoContentType.Episode];

    /// <summary>
    /// Picks which subs.ro search field to use for a request, preferring IMDb id, then TMDb id,
    /// then title, in that order.
    /// </summary>
    /// <param name="request">The subtitle search request from Jellyfin.</param>
    /// <param name="field">On return, the subs.ro search field to use ("imdbid", "tmdbid", or "title"); empty when nothing usable was found.</param>
    /// <returns>The lookup value to search for, or null if the request has neither a provider id nor a usable title.</returns>
    public static string? SelectLookup(SubtitleSearchRequest request, out string field)
    {
        if (request.ProviderIds is not null)
        {
            if (request.ProviderIds.TryGetValue("Imdb", out var imdb) && !string.IsNullOrWhiteSpace(imdb))
            {
                field = "imdbid";
                return imdb;
            }

            if (request.ProviderIds.TryGetValue("Tmdb", out var tmdb) && !string.IsNullOrWhiteSpace(tmdb))
            {
                field = "tmdbid";
                return tmdb;
            }
        }

        // VideoContentType.Episode is the enum's default value (0), so a request that never sets
        // ContentType explicitly must not be misread as an episode lookup. SeriesName is only ever
        // populated for episode requests, so prefer it when present and fall back to Name otherwise,
        // instead of branching on ContentType.
        var title = !string.IsNullOrWhiteSpace(request.SeriesName) ? request.SeriesName : request.Name;
        if (!string.IsNullOrWhiteSpace(title))
        {
            field = "title";
            return title;
        }

        field = string.Empty;
        return null;
    }

    /// <summary>
    /// Searches subs.ro for Romanian subtitles matching the request. Never throws: a missing
    /// API key, an unusable request, or a transport failure all result in an empty result and
    /// a log entry, so that a subs.ro outage never breaks Jellyfin's subtitle search for every
    /// installed provider.
    /// </summary>
    /// <param name="request">The subtitle search request from Jellyfin.</param>
    /// <param name="cancellationToken">A token used to cancel the search.</param>
    /// <returns>The matching remote subtitles, or an empty collection if none were found or the search could not be performed.</returns>
    public async Task<IEnumerable<RemoteSubtitleInfo>> Search(
        SubtitleSearchRequest request, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            _logger.LogWarning("Subs.ro API key is not configured; skipping search");
            return [];
        }

        if (request.ContentType == VideoContentType.Episode && !config.EnableSeries)
        {
            return [];
        }

        var value = SelectLookup(request, out var field);
        if (value is null)
        {
            return [];
        }

        var items = await _client
            .SearchAsync(field, value, config.ApiKey, cancellationToken)
            .ConfigureAwait(false);

        var wantedType = request.ContentType == VideoContentType.Episode ? "series" : "movie";

        return items
            .Where(i => string.Equals(i.Type, wantedType, StringComparison.OrdinalIgnoreCase))
            .Select(i => new RemoteSubtitleInfo
            {
                Id = SubtitleId.Encode(i.Id, null),
                ProviderName = Name,
                Name = $"{i.Title} ({i.Year}) — {i.Translator}",
                Format = "srt",
                Author = i.Translator,
                Comment = i.Description,
                ThreeLetterISOLanguageName = "ron"
            })
            .ToList();
    }

    /// <summary>
    /// Downloads and, for archives, extracts the subtitle identified by the given opaque id.
    /// </summary>
    /// <param name="id">The opaque subtitle id previously returned by <see cref="Search"/>.</param>
    /// <param name="cancellationToken">A token used to cancel the download.</param>
    /// <returns>Never returns; always throws until implemented.</returns>
    public Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        => throw new NotImplementedException("Task 9");
}
