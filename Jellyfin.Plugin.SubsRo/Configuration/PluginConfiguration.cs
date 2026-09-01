using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SubsRo.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Personal subs.ro API key. Supplied per install; never shipped.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Off by default: series search downloads an archive and spends daily quota.</summary>
    public bool EnableSeries { get; set; }
}
