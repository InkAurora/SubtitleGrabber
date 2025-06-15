using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
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
            DebugLog("[DEBUG] OpenSubtitlesProvider static constructor called");
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
            
            DebugLog("[DEBUG] ==========================================");
            DebugLog("[DEBUG] OPENSUBTITLES PROVIDER CREATED!");
            DebugLog("[DEBUG] Constructor called - OpenSubtitles Grabber");
            DebugLog("[DEBUG] ==========================================");
            System.Diagnostics.Debug.WriteLine("[DEBUG] OpenSubtitlesProvider constructor called");
            _logger.LogInformation("OpenSubtitles Grabber subtitle provider initialized");        }        /// <inheritdoc />
        public string Name => "OpenSubtitles Grabber";

        private static void DebugLog(string message)
        {
            var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
            if (config?.EnableDebugLogging == true)
            {
                Console.WriteLine(message);
            }
        }

        /// <inheritdoc />
        public IEnumerable<VideoContentType> SupportedMediaTypes
        {
            get
            {
                DebugLog("[DEBUG] OpenSubtitlesProvider.SupportedMediaTypes accessed");
                return new[] { VideoContentType.Episode, VideoContentType.Movie };
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            DebugLog($"[DEBUG] ==========================================");
            DebugLog($"[DEBUG] SUBTITLE SEARCH STARTED");
            DebugLog($"[DEBUG] OpenSubtitlesProvider.Search called for: {request.MediaPath}");
            DebugLog($"[DEBUG] Language: {request.Language}");
            DebugLog($"[DEBUG] ==========================================");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] OpenSubtitlesProvider.Search called for: {request.MediaPath}");
            
            try
            {
                var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
                var searchResults = new List<RemoteSubtitleInfo>();

                // Get search text based on media type
                var searchText = GetSearchText(request);
                if (!string.IsNullOrEmpty(searchText))
                {
                    DebugLog($"[DEBUG] Searching for: {searchText}");
                    searchResults.AddRange(await SearchByText(searchText, request.Language, config, cancellationToken).ConfigureAwait(false));
                }

                // Remove duplicates and limit results
                var finalResults = searchResults
                    .GroupBy(s => s.Id)
                    .Select(g => g.First())
                    .Take(config.MaxSearchResults)
                    .ToList();

                DebugLog($"[DEBUG] ==========================================");
                DebugLog($"[DEBUG] SUBTITLE SEARCH COMPLETED");
                DebugLog($"[DEBUG] Total found: {searchResults.Count}");
                DebugLog($"[DEBUG] After deduplication: {finalResults.Count}");
                DebugLog($"[DEBUG] Final results:");
                for (int i = 0; i < finalResults.Count; i++)
                {
                    DebugLog($"[DEBUG] {i + 1}. ID: {finalResults[i].Id}");
                    DebugLog($"[DEBUG]    Name: {finalResults[i].Name}");
                    DebugLog($"[DEBUG]    Provider: {finalResults[i].ProviderName}");
                }
                DebugLog($"[DEBUG] ==========================================");

                return finalResults;
            }
            catch (Exception ex)
            {
                DebugLog($"[DEBUG] ==========================================");
                DebugLog($"[DEBUG] SUBTITLE SEARCH FAILED");
                DebugLog($"[DEBUG] ERROR: {ex.GetType().Name}: {ex.Message}");
                DebugLog($"[DEBUG] Stack Trace: {ex.StackTrace}");
                DebugLog($"[DEBUG] ==========================================");
                _logger.LogError(ex, "Error searching for subtitles");
                return new List<RemoteSubtitleInfo>();
            }
        }

        /// <inheritdoc />
        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            DebugLog($"[DEBUG] ==========================================");
            DebugLog($"[DEBUG] DOWNLOAD BUTTON CLICKED!");
            DebugLog($"[DEBUG] OpenSubtitlesProvider.GetSubtitles called for ID: {id}");
            DebugLog($"[DEBUG] ==========================================");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] OpenSubtitlesProvider.GetSubtitles called for ID: {id}");
            
            try
            {
                // Decode the simple ID format back to a download URL
                string downloadUrl;
                string subtitleLanguage = "en"; // default
                
                if (id.StartsWith("srt-") && id.Contains("-"))
                {
                    // Parse simple ID format: "srt-en-123456"
                    var parts = id.Split('-');
                    if (parts.Length >= 3)
                    {
                        subtitleLanguage = parts[1];
                        var subtitleId = parts[2];
                        downloadUrl = $"https://dl.opensubtitles.org/en/download/sub/{subtitleId}";
                        DebugLog($"[DEBUG] Decoded simple ID:");
                        DebugLog($"[DEBUG] - Language: {subtitleLanguage}");
                        DebugLog($"[DEBUG] - Subtitle ID: {subtitleId}");
                        DebugLog($"[DEBUG] - Download URL: {downloadUrl}");
                    }
                    else
                    {
                        DebugLog($"[DEBUG] ERROR: Invalid simple ID format: {id}");
                        throw new ArgumentException($"Invalid subtitle ID format: {id}");
                    }
                }
                else if (id.StartsWith("http"))
                {
                    // If it's already a full URL, use it directly
                    downloadUrl = id;
                    subtitleLanguage = "en"; // default when no language in URL
                    DebugLog($"[DEBUG] Using full URL directly: {downloadUrl}");
                }
                else
                {
                    DebugLog($"[DEBUG] ERROR: Unrecognized ID format: {id}");
                    throw new ArgumentException($"Unrecognized subtitle ID format: {id}");
                }
            
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                httpClient.DefaultRequestHeaders.Add("Referer", "https://www.opensubtitles.org/");
                
                DebugLog($"[DEBUG] Step 1: HTTP client configured");
                DebugLog($"[DEBUG] Step 2: Attempting to download from URL: {downloadUrl}");
                
                var response = await httpClient.GetAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
                
                DebugLog($"[DEBUG] Step 3: HTTP Response received");
                DebugLog($"[DEBUG] - Status Code: {response.StatusCode}");
                DebugLog($"[DEBUG] - Status: {(response.IsSuccessStatusCode ? "SUCCESS" : "FAILED")}");
                DebugLog($"[DEBUG] - Reason Phrase: {response.ReasonPhrase}");
                
                if (!response.IsSuccessStatusCode)
                {
                    DebugLog($"[DEBUG] ERROR: Download failed with status: {response.StatusCode}");
                    throw new HttpRequestException($"Failed to download subtitle: {response.StatusCode} - {response.ReasonPhrase}");
                }

                var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                DebugLog($"[DEBUG] Step 4: Content downloaded - size: {content.Length} bytes");
                
                // Check if this is a ZIP file and extract subtitle content
                byte[] subtitleContent;
                if (IsZipFile(content))
                {
                    DebugLog($"[DEBUG] Step 5a: Content is ZIP file - extracting subtitle");
                    var extractedContent = ExtractSubtitleFromZip(content);
                    if (extractedContent == null)
                    {
                        DebugLog($"[DEBUG] ERROR: Failed to extract subtitle from ZIP");
                        throw new InvalidOperationException("Failed to extract subtitle from ZIP file");
                    }
                    subtitleContent = extractedContent;
                    DebugLog($"[DEBUG] Step 5b: Successfully extracted subtitle from ZIP - size: {subtitleContent.Length} bytes");
                }
                else
                {
                    DebugLog($"[DEBUG] Step 5a: Content is direct subtitle file");
                    subtitleContent = content;
                }
                
                var format = DetectSubtitleFormat(subtitleContent);
                DebugLog($"[DEBUG] Step 6: Detected subtitle format: {format}");

                DebugLog($"[DEBUG] ==========================================");
                DebugLog($"[DEBUG] DOWNLOAD COMPLETED SUCCESSFULLY!");
                DebugLog($"[DEBUG] - Format: {format}");
                DebugLog($"[DEBUG] - Size: {subtitleContent.Length} bytes");
                DebugLog($"[DEBUG] - Language: {subtitleLanguage}");
                DebugLog($"[DEBUG] - Was ZIP: {IsZipFile(content)}");
                DebugLog($"[DEBUG] ==========================================");

                return new SubtitleResponse
                {
                    Language = subtitleLanguage,
                    Stream = new MemoryStream(subtitleContent),
                    Format = format
                };
            }
            catch (Exception ex)
            {
                DebugLog($"[DEBUG] ==========================================");
                DebugLog($"[DEBUG] DOWNLOAD FAILED!");
                DebugLog($"[DEBUG] ERROR: {ex.GetType().Name}: {ex.Message}");
                DebugLog($"[DEBUG] Stack Trace: {ex.StackTrace}");
                DebugLog($"[DEBUG] ==========================================");
                _logger.LogError(ex, "Error downloading subtitle with ID {Id}", id);
                throw;
            }
        }

        private string DetectSubtitleFormat(byte[] content)
        {
            var text = Encoding.UTF8.GetString(content, 0, Math.Min(1000, content.Length));
            
            if (text.Contains("-->") && Regex.IsMatch(text, @"\d{2}:\d{2}:\d{2}"))
            {
                return "srt";
            }
            if (text.Contains("[Script Info]") || text.Contains("Dialogue:"))
            {
                return "ass";
            }
            if (text.Contains("{") && text.Contains("}") && Regex.IsMatch(text, @"\{\d+\}"))
            {
                return "sub";
            }
            
            return "srt"; // default
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

                DebugLog($"[DEBUG] Searching URL: {searchUrl}");

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var response = await httpClient.GetAsync(searchUrl, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    DebugLog($"[DEBUG] HTTP error: {response.StatusCode}");
                    return results;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Parse search results to get subtitle IDs
                var subtitleRows = doc.DocumentNode.SelectNodes("//tr[@onclick]") ?? new HtmlNodeCollection(null);
                DebugLog($"[DEBUG] Found {subtitleRows.Count} potential subtitle rows");
                
                var subtitleIds = new List<string>();
                
                foreach (var row in subtitleRows.Take(config.MaxSearchResults))
                {
                    try
                    {
                        // Look for subtitle page links to extract subtitle ID
                        var subtitleLinkNode = row.SelectSingleNode(".//a[contains(@href, '/subtitles/')]");
                        if (subtitleLinkNode == null) continue;

                        var subtitlePageUrl = subtitleLinkNode.GetAttributeValue("href", "");
                        if (string.IsNullOrEmpty(subtitlePageUrl)) continue;

                        // Extract subtitle ID from URL like /subtitles/123456/subtitle-name
                        var idMatch = Regex.Match(subtitlePageUrl, @"/subtitles/(\d+)");
                        if (!idMatch.Success) continue;
                        
                        var subtitleId = idMatch.Groups[1].Value;
                        subtitleIds.Add(subtitleId);
                        
                        DebugLog($"[DEBUG] Found subtitle ID: {subtitleId}");
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"[DEBUG] Error parsing subtitle row: {ex.Message}");
                        continue;
                    }
                }
                
                DebugLog($"[DEBUG] Collected {subtitleIds.Count} subtitle IDs, now fetching individual pages...");
                
                // Now fetch individual subtitle pages to get the actual filenames
                foreach (var subtitleId in subtitleIds)
                {
                    try
                    {
                        var subtitleName = await GetSubtitleNameFromPage(subtitleId, httpClient, cancellationToken);
                        if (string.IsNullOrEmpty(subtitleName))
                        {
                            DebugLog($"[DEBUG] Could not get subtitle name for ID {subtitleId}, skipping");
                            continue;
                        }
                        
                        // Create simple ID format that Jellyfin can handle
                        var simpleId = $"srt-{language}-{subtitleId}";
                        
                        DebugLog($"[DEBUG] Found subtitle:");
                        DebugLog($"[DEBUG] - Name: {subtitleName}");
                        DebugLog($"[DEBUG] - Subtitle ID: {subtitleId}");
                        DebugLog($"[DEBUG] - Simple ID: {simpleId}");

                        var result = new RemoteSubtitleInfo
                        {
                            Id = simpleId,
                            Name = subtitleName,
                            ProviderName = Name,
                            ThreeLetterISOLanguageName = MapLanguageCode(language),
                            Format = "srt",
                            IsHashMatch = false
                        };

                        results.Add(result);
                        DebugLog($"[DEBUG] Added subtitle: {subtitleName} - ID: {simpleId}");
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"[DEBUG] Error processing subtitle ID {subtitleId}: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"[DEBUG] Error in SearchByText: {ex.Message}");
                _logger.LogError(ex, "Error searching for subtitles with text {SearchText}", searchText);
            }

            return results;
        }

        private async Task<string?> GetSubtitleNameFromPage(string subtitleId, HttpClient httpClient, CancellationToken cancellationToken)
        {
            try
            {
                var pageUrl = $"https://www.opensubtitles.org/en/subtitles/{subtitleId}";
                DebugLog($"[DEBUG] Fetching subtitle page: {pageUrl}");
                
                // Configure HttpClient to follow redirects
                var request = new HttpRequestMessage(HttpMethod.Get, pageUrl);
                var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                
                // If we get a redirect, follow it manually
                if (response.StatusCode == HttpStatusCode.MovedPermanently || 
                    response.StatusCode == HttpStatusCode.Found || 
                    response.StatusCode == HttpStatusCode.SeeOther)
                {
                    var location = response.Headers.Location?.ToString();
                    if (!string.IsNullOrEmpty(location))
                    {
                        // Make sure it's an absolute URL
                        if (!location.StartsWith("http"))
                        {
                            location = $"https://www.opensubtitles.org{location}";
                        }
                        
                        DebugLog($"[DEBUG] Following redirect to: {location}");
                        response = await httpClient.GetAsync(location, cancellationToken).ConfigureAwait(false);
                    }
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    DebugLog($"[DEBUG] Failed to fetch subtitle page {subtitleId}: {response.StatusCode}");
                    return null;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Look for the subtitle filename in the download link
                var downloadLink = doc.DocumentNode.SelectSingleNode("//a[contains(@href, '/download/file/')]");
                if (downloadLink != null)
                {
                    var linkText = downloadLink.InnerText?.Trim();
                    DebugLog($"[DEBUG] Found download link text: '{linkText}'");
                    
                    if (!string.IsNullOrEmpty(linkText))
                    {
                        // Remove file size info and extension
                        var cleanedText = Regex.Replace(linkText, @"\s*\(\d+bytes?\)\s*", "", RegexOptions.IgnoreCase);
                        cleanedText = Regex.Replace(cleanedText, @"\.srt$", "", RegexOptions.IgnoreCase);
                        cleanedText = cleanedText.Trim();
                        
                        if (!string.IsNullOrEmpty(cleanedText) && cleanedText.Length > 5)
                        {
                            DebugLog($"[DEBUG] Extracted subtitle name: '{cleanedText}'");
                            return cleanedText;
                        }
                    }
                }
                
                DebugLog($"[DEBUG] Could not find subtitle filename on page {subtitleId}");
                return null;
            }
            catch (Exception ex)
            {
                DebugLog($"[DEBUG] Error fetching subtitle page {subtitleId}: {ex.Message}");
                return null;
            }
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
                return language.ToLowerInvariant();            // Default to English
            return "eng";
        }

        private bool IsZipFile(byte[] content)
        {
            if (content == null || content.Length < 4)
                return false;

            // Check for ZIP file magic bytes: PK (0x50, 0x4B)
            return content[0] == 0x50 && content[1] == 0x4B;
        }

        private byte[]? ExtractSubtitleFromZip(byte[] zipContent)
        {
            try
            {
                using var zipStream = new MemoryStream(zipContent);
                using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);

                DebugLog($"[DEBUG] ZIP contains {archive.Entries.Count} entries");

                // Look for subtitle files in the ZIP
                var subtitleExtensions = new[] { ".srt", ".ass", ".ssa", ".sub", ".vtt", ".txt" };
                
                foreach (var entry in archive.Entries)
                {
                    DebugLog($"[DEBUG] ZIP entry: {entry.Name} (size: {entry.Length})");
                    
                    if (subtitleExtensions.Any(ext => entry.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    {
                        DebugLog($"[DEBUG] Extracting subtitle file: {entry.Name}");
                        
                        using var entryStream = entry.Open();
                        using var memoryStream = new MemoryStream();
                        entryStream.CopyTo(memoryStream);
                        
                        var content = memoryStream.ToArray();
                        DebugLog($"[DEBUG] Extracted {content.Length} bytes from {entry.Name}");
                        
                        return content;
                    }
                }

                DebugLog($"[DEBUG] No subtitle files found in ZIP");
                return null;
            }
            catch (Exception ex)
            {
                DebugLog($"[DEBUG] Error extracting from ZIP: {ex.Message}");
                return null;
            }
        }
    }
}
