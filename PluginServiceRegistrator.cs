using Jellyfin.Plugin.OpenSubtitlesGrabber.Providers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Subtitles;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.OpenSubtitlesGrabber
{
    /// <summary>
    /// Plugin service registrator for OpenSubtitles Grabber.
    /// </summary>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddSingleton<ISubtitleProvider, OpenSubtitlesProvider>();
        }
    }
}
