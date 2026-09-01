using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SubsRo.Api.Models;

public sealed class SubsRoSearchResponse
{
    [JsonPropertyName("status")] public int Status { get; set; }
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("items")] public List<SubsRoSubtitleItem>? Items { get; set; }
}
