using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SubsRo.Api.Models;

/// <summary>
/// The top-level envelope returned by the subs.ro search endpoint for a single lookup.
/// </summary>
public sealed class SubsRoSearchResponse
{
    /// <summary>Gets or sets the API's status code for the request (0 typically indicates success; non-zero codes signal an API-level error).</summary>
    [JsonPropertyName("status")] public int Status { get; set; }

    /// <summary>Gets or sets the number of matching subtitle entries reported by the API.</summary>
    [JsonPropertyName("count")] public int Count { get; set; }

    /// <summary>Gets or sets the matching subtitle entries, or null when the API returned no items element.</summary>
    [JsonPropertyName("items")] public List<SubsRoSubtitleItem>? Items { get; set; }
}
