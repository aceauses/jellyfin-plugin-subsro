using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SubsRo.Api.Models;

/// <summary>
/// The account's daily download allowance on subs.ro, as reported by the quota endpoint.
/// </summary>
public sealed class SubsRoQuota
{
    /// <summary>Gets or sets the total number of downloads the account is allowed per day.</summary>
    [JsonPropertyName("total_quota")] public int Total { get; set; }

    /// <summary>Gets or sets the number of downloads already spent today.</summary>
    [JsonPropertyName("used_quota")] public int Used { get; set; }

    /// <summary>Gets or sets the number of downloads still available before the daily limit resets.</summary>
    [JsonPropertyName("remaining_quota")] public int Remaining { get; set; }
}

/// <summary>
/// The top-level envelope returned by the subs.ro quota endpoint.
/// </summary>
public sealed class SubsRoQuotaResponse
{
    /// <summary>Gets or sets the quota figures for the calling account, or null if the API omitted them.</summary>
    [JsonPropertyName("quota")] public SubsRoQuota? Quota { get; set; }
}
