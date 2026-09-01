using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SubsRo;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient<Api.SubsRoApiClient>();

        // TODO(Task 7): SubsRoSubtitleProvider does not exist yet. Restore this registration
        // once it is implemented.
        // serviceCollection.AddSingleton<ISubtitleProvider, SubsRoSubtitleProvider>();
    }
}
