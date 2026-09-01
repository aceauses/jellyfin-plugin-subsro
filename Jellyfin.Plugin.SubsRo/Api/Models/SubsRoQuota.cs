using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SubsRo.Api.Models;

public sealed class SubsRoQuota
{
    [JsonPropertyName("total_quota")] public int Total { get; set; }
    [JsonPropertyName("used_quota")] public int Used { get; set; }
    [JsonPropertyName("remaining_quota")] public int Remaining { get; set; }
}

public sealed class SubsRoQuotaResponse
{
    [JsonPropertyName("quota")] public SubsRoQuota? Quota { get; set; }
}
