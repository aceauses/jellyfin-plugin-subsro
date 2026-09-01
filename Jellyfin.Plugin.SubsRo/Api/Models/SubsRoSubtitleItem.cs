using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SubsRo.Api.Models;

public sealed class SubsRoSubtitleItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("year")] public int? Year { get; set; }
    [JsonPropertyName("imdbid")] public string? ImdbId { get; set; }
    [JsonPropertyName("tmdbid")] public string? TmdbId { get; set; }
    [JsonPropertyName("language")] public string? Language { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("translator")] public string? Translator { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("downloadLink")] public string? DownloadLink { get; set; }
}
