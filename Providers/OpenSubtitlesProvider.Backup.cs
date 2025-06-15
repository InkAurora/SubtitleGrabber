using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using Jellyfin.Plugin.OpenSubtitlesGrabber.Configuration;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OpenSubtitlesGrabber.Providers
{
    /// <summary>
    /// OpenSubtitles subtitle provider that scrapes the website without using the API.
    /// </summary>
    public class OpenSubtitlesProvider : ISubtitleProvider
    {
        private readonly ILogger<OpenSubtitlesProvider> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private static readonly Dictionary<string, string> LanguageCodes = new()
        {
            { "en", "eng" },
            { "es", "spa" },
            { "fr", "fre" },
            { "de", "ger" },
            { "it", "ita" },
            { "pt", "por" },
            { "ru", "rus" },
            { "ja", "jpn" },
            { "ko", "kor" },
            { "zh", "chi" },
            { "ar", "ara" },
            { "hi", "hin" },
            { "nl", "dut" },
            { "sv", "swe" },
            { "no", "nor" },
            { "da", "dan" },
            { "fi", "fin" },
            { "pl", "pol" },
            { "cs", "cze" },
            { "hu", "hun" },
            { "tr", "tur" }
        };

        /// <summary>
        /// Static constructor for debugging.
        /// </summary>
        static OpenSubtitlesProvider()
        {
            Console.WriteLine("[DEBUG] OpenSubtitlesProvider static constructor called");
            System.Diagnostics.Debug.WriteLine("[DEBUG] OpenSubtitlesProvider static constructor called");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenSubtitlesProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        public OpenSubtitlesProvider(ILogger<OpenSubtitlesProvider> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            
            Console.WriteLine("[DEBUG] OpenSubtitlesProvider constructor called");
            System.Diagnostics.Debug.WriteLine("[DEBUG] OpenSubtitlesProvider constructor called");
        }

        /// <inheritdoc />
        public string Name => "OpenSubtitles Grabber";

        /// <inheritdoc />
        public IEnumerable<VideoContentType> SupportedMediaTypes
        {
            get
            {
                Console.WriteLine("[DEBUG] OpenSubtitlesProvider.SupportedMediaTypes accessed");
                return new[] { VideoContentType.Episode, VideoContentType.Movie };
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[DEBUG] OpenSubtitlesProvider.Search called for: {request.MediaPath}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] OpenSubtitlesProvider.Search called for: {request.MediaPath}");
            
            try
            {
                var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
                var searchResults = new List<RemoteSubtitleInfo>();

                // Get search text based on media type
                var searchText = GetSearchText(request);
                if (!string.IsNullOrEmpty(searchText))
                {
                    Console.WriteLine($"[DEBUG] Searching for: {searchText}");
                    searchResults.AddRange(await SearchByText(searchText, request.Language, config, cancellationToken).ConfigureAwait(false));
                }

                // Remove duplicates and limit results
                return searchResults
                    .GroupBy(s => s.Id)
                    .Select(g => g.First())
                    .Take(config.MaxSearchResults)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] OpenSubtitlesProvider.Search error: {ex.Message}");
                _logger.LogError(ex, "Error searching for subtitles");
                return new List<RemoteSubtitleInfo>();
            }
        }

        /// <inheritdoc />
        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[DEBUG] OpenSubtitlesProvider.GetSubtitles called for ID: {id}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] OpenSubtitlesProvider.GetSubtitles called for ID: {id}");
            
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.GetAsync(id, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var contentBytes = Encoding.UTF8.GetBytes(content);

                return new SubtitleResponse
                {
                    Language = "en", // This should be extracted from the actual subtitle
                    Stream = new MemoryStream(contentBytes),
                    Format = "srt"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] OpenSubtitlesProvider.GetSubtitles error: {ex.Message}");
                _logger.LogError(ex, "Error downloading subtitle with ID {Id}", id);
                throw;
            }
        }

        private string GetSearchText(SubtitleSearchRequest request)
        {
            if (request.ContentType == VideoContentType.Episode && !string.IsNullOrEmpty(request.SeriesName))
            {
                var season = request.ParentIndexNumber ?? 1;
                var episode = request.IndexNumber ?? 1;
                return $"{request.SeriesName} S{season:D2}E{episode:D2}";
            }

            // For movies or when SeriesName is not available, use the filename
            if (!string.IsNullOrEmpty(request.MediaPath))
            {
                var fileName = Path.GetFileNameWithoutExtension(request.MediaPath);
                // Clean up the filename (remove common patterns)
                fileName = Regex.Replace(fileName, @"\.(720p|1080p|x264|x265|BluRay|WEB-DL|HDTV).*", "", RegexOptions.IgnoreCase);
                fileName = Regex.Replace(fileName, @"\[.*?\]", "", RegexOptions.IgnoreCase);
                fileName = fileName.Replace(".", " ").Replace("_", " ").Trim();
                return fileName;
            }

            return string.Empty;
        }

        private async Task<List<RemoteSubtitleInfo>> SearchByText(string searchText, string language, PluginConfiguration config, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSubtitleInfo>();

            try
            {
                // Map language code
                var languageCode = MapLanguageCode(language);
                
                // Build search URL
                var encodedSearch = HttpUtility.UrlEncode(searchText);
                var searchUrl = $"https://www.opensubtitles.org/en/search/sublanguageid-{languageCode}/moviename-{encodedSearch}";

                Console.WriteLine($"[DEBUG] Searching URL: {searchUrl}");

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var response = await httpClient.GetAsync(searchUrl, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[DEBUG] HTTP error: {response.StatusCode}");
                    return results;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Parse search results
                var subtitleRows = doc.DocumentNode.SelectNodes("//tr[@onclick]") ?? new HtmlNodeCollection(null);
                
                foreach (var row in subtitleRows.Take(config.MaxSearchResults))
                {
                    try
                    {
                        var downloadLinkNode = row.SelectSingleNode(".//a[contains(@href, '/download/')]");
                        if (downloadLinkNode == null) continue;

                        var downloadUrl = downloadLinkNode.GetAttributeValue("href", "");
                        if (string.IsNullOrEmpty(downloadUrl)) continue;

                        if (!downloadUrl.StartsWith("http"))
                        {
                            downloadUrl = "https://www.opensubtitles.org" + downloadUrl;
                        }

                        var nameNode = row.SelectSingleNode(".//a[contains(@href, '/subtitles/')]");
                        var name = nameNode?.InnerText?.Trim() ?? "Unknown";

                        var result = new RemoteSubtitleInfo
                        {
                            Id = downloadUrl,
                            Name = name,
                            ProviderName = Name,
                            ThreeLetterISOLanguageName = language,
                            Format = "srt"
                        };

                        results.Add(result);
                        Console.WriteLine($"[DEBUG] Found subtitle: {name} - {downloadUrl}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] Error parsing subtitle row: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error in SearchByText: {ex.Message}");
                _logger.LogError(ex, "Error searching for subtitles with text {SearchText}", searchText);
            }

            return results;
        }

        private static string MapLanguageCode(string language)
        {
            if (string.IsNullOrEmpty(language))
                return "eng";

            // Try to map 2-letter code to 3-letter code
            if (LanguageCodes.TryGetValue(language.ToLowerInvariant(), out var mapped))
                return mapped;

            // If it's already a 3-letter code, use it as-is
            if (language.Length == 3)
                return language.ToLowerInvariant();

            // Default to English
            return "eng";
        }
    }
}
