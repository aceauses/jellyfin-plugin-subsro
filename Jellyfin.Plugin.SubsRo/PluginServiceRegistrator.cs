using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SubsRo;

/// <summary>
/// Wires the plugin's services into Jellyfin's dependency injection container at startup.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <summary>
    /// Registers the subs.ro API client (with its managed <see cref="System.Net.Http.HttpClient"/>)
    /// so it can be injected into the configuration quota endpoint and, once available, the
    /// subtitle provider.
    /// </summary>
    /// <param name="serviceCollection">The DI container to register services into.</param>
    /// <param name="applicationHost">The running Jellyfin server host; unused here but required by the interface.</param>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient<Api.SubsRoApiClient>();

        // TODO(Task 7): SubsRoSubtitleProvider does not exist yet. Restore this registration
        // once it is implemented.
        // serviceCollection.AddSingleton<ISubtitleProvider, SubsRoSubtitleProvider>();
    }
}
