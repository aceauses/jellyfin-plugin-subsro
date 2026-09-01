using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SubsRo.Api.Models;

/// <summary>
/// A single subtitle (or subtitle archive) entry as returned by the subs.ro search API.
/// One item can cover a movie or an entire series/season, in which case
/// <see cref="DownloadLink"/> points at a ZIP archive that must be matched against
/// the requested episode.
/// </summary>
public sealed class SubsRoSubtitleItem
{
    /// <summary>Gets or sets the subs.ro identifier for this subtitle entry, used to build the opaque <c>SubtitleId</c> handed to Jellyfin.</summary>
    [JsonPropertyName("id")] public int Id { get; set; }

    /// <summary>Gets or sets the release title associated with this subtitle.</summary>
    [JsonPropertyName("title")] public string? Title { get; set; }

    /// <summary>Gets or sets the release year, when subs.ro reports one.</summary>
    [JsonPropertyName("year")] public int? Year { get; set; }

    /// <summary>Gets or sets the IMDb identifier subs.ro associates with this entry, used to cross-check search results.</summary>
    [JsonPropertyName("imdbid")] public string? ImdbId { get; set; }

    /// <summary>Gets or sets the TMDb identifier subs.ro associates with this entry, used to cross-check search results.</summary>
    [JsonPropertyName("tmdbid")] public string? TmdbId { get; set; }

    /// <summary>Gets or sets the subtitle language code reported by subs.ro.</summary>
    [JsonPropertyName("language")] public string? Language { get; set; }

    /// <summary>Gets or sets the media type reported by subs.ro (for example, movie or series), used to decide whether the download is a plain file or an archive.</summary>
    [JsonPropertyName("type")] public string? Type { get; set; }

    /// <summary>Gets or sets the name or handle of the person credited with the translation.</summary>
    [JsonPropertyName("translator")] public string? Translator { get; set; }

    /// <summary>Gets or sets the free-text description subs.ro shows for this entry.</summary>
    [JsonPropertyName("description")] public string? Description { get; set; }

    /// <summary>Gets or sets the URL used to download the subtitle file or archive for this entry.</summary>
    [JsonPropertyName("downloadLink")] public string? DownloadLink { get; set; }
}
