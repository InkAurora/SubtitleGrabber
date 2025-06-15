using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.OpenSubtitlesGrabber.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OpenSubtitlesGrabber
{
    /// <summary>
    /// The main plugin class for OpenSubtitles Grabber.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        /// <summary>
        /// Static constructor for debugging.
        /// </summary>
        static Plugin()
        {
            Console.WriteLine("[DEBUG] Plugin static constructor called");
            System.Diagnostics.Debug.WriteLine("[DEBUG] Plugin static constructor called");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            
            Console.WriteLine("[DEBUG] Plugin constructor called - OpenSubtitles Grabber");
            System.Diagnostics.Debug.WriteLine("[DEBUG] Plugin constructor called - OpenSubtitles Grabber");
        }

        /// <inheritdoc />
        public override string Name => "OpenSubtitles Grabber";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("12345678-1234-1234-1234-123456789012");

        /// <inheritdoc />
        public override string Description => "Downloads subtitles from OpenSubtitles.org without using their API";

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static Plugin? Instance { get; private set; }

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            Console.WriteLine("[DEBUG] Plugin.GetPages() called");
            System.Diagnostics.Debug.WriteLine("[DEBUG] Plugin.GetPages() called");
            
            return new[]
            {
                new PluginPageInfo
                {
                    Name = this.Name,
                    EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.config.html", GetType().Namespace)
                }
            };
        }
    }
}
