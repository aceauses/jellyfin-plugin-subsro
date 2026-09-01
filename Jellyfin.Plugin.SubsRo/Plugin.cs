using System.Globalization;
using Jellyfin.Plugin.SubsRo.Configuration;
using Jellyfin.Plugin.SubsRo.Text;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.SubsRo;

/// <summary>
/// Entry point Jellyfin loads to activate the Subs.ro plugin: registers the legacy
/// text-encoding providers subtitle conversion relies on and exposes the dashboard
/// configuration page.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin's application paths, used by the base class to locate the plugin's configuration file.</param>
    /// <param name="xmlSerializer">The serializer the base class uses to persist <see cref="PluginConfiguration"/>.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        SubtitleEncodingConverter.RegisterProviders();
    }

    /// <summary>Gets the singleton instance of the plugin, set once Jellyfin constructs it. Used by <see cref="Api.SubsRoController"/> to read the configured API key.</summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>Gets the name shown for this plugin in the Jellyfin dashboard's plugin list.</summary>
    public override string Name => "Subs.ro";

    /// <summary>Gets the plugin's fixed unique identifier, used by Jellyfin to persist its configuration file and to build the embedded-resource path for its dashboard page.</summary>
    public override Guid Id => Guid.Parse("6f1d5a72-9c34-4e0b-9a55-2f8c1d7b4e90");

    /// <summary>Gets the Romanian-language description shown for this plugin in the Jellyfin dashboard.</summary>
    public override string Description => "Subtitrări în română de pe subs.ro.";

    /// <summary>
    /// Declares the plugin's dashboard configuration page, pointing at the HTML embedded
    /// under the <c>Configuration</c> namespace.
    /// </summary>
    /// <returns>A single-page collection describing the configuration page shown in the Jellyfin dashboard.</returns>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.Configuration.configPage.html",
                GetType().Namespace)
        };
    }
}
