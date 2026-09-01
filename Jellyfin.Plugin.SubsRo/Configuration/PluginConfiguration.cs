using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SubsRo.Configuration;

/// <summary>
/// Settings for the Subs.ro plugin, edited through its dashboard configuration page.
/// The language is fixed to Romanian, so the only user-facing choices are the API key
/// and whether series search is enabled.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Personal subs.ro API key. Supplied per install; never shipped.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Off by default: series search downloads an archive and spends daily quota.</summary>
    public bool EnableSeries { get; set; }
}
