using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.OpenSubtitlesGrabber.Configuration
{
    /// <summary>
    /// Plugin configuration class for OpenSubtitles Grabber.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether to enable debug logging.
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum number of search results to process.
        /// </summary>
        public int MaxSearchResults { get; set; } = 10;

        /// <summary>
        /// Gets or sets the request timeout in seconds.
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether to prefer hearing impaired subtitles.
        /// </summary>
        public bool PreferHearingImpaired { get; set; } = false;

        /// <summary>
        /// Gets or sets the preferred subtitle format.
        /// </summary>
        public string PreferredFormat { get; set; } = "srt";
    }
}
