using System.Globalization;
using Jellyfin.Plugin.SubsRo.Configuration;
using Jellyfin.Plugin.SubsRo.Text;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.SubsRo;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        SubtitleEncodingConverter.RegisterProviders();
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Subs.ro";

    public override Guid Id => Guid.Parse("6f1d5a72-9c34-4e0b-9a55-2f8c1d7b4e90");

    public override string Description => "Subtitrări în română de pe subs.ro.";

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
