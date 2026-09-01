using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Subtitles;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SubsRo;

/// <summary>
/// Wires the plugin's services into Jellyfin's dependency injection container at startup.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <summary>
    /// Registers the subs.ro API client (with its managed <see cref="System.Net.Http.HttpClient"/>),
    /// the in-memory cache it shares with the subtitle provider, and the subtitle provider itself
    /// so Jellyfin can inject them into the configuration quota endpoint and the subtitle search UI.
    /// </summary>
    /// <param name="serviceCollection">The DI container to register services into.</param>
    /// <param name="applicationHost">The running Jellyfin server host; unused here but required by the interface.</param>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient<Api.SubsRoApiClient>();
        serviceCollection.AddMemoryCache();

        serviceCollection.AddSingleton<ISubtitleProvider, SubsRoSubtitleProvider>();
    }
}
