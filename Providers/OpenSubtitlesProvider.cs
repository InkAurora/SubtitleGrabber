using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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
            
            Console.WriteLine("[DEBUG] ==========================================");
            Console.WriteLine("[DEBUG] OPENSUBTITLES PROVIDER CREATED!");
            Console.WriteLine("[DEBUG] Constructor called - OpenSubtitles Grabber");
            Console.WriteLine("[DEBUG] ==========================================");
            System.Diagnostics.Debug.WriteLine("[DEBUG] OpenSubtitlesProvider constructor called");
            _logger.LogInformation("OpenSubtitles Grabber subtitle provider initialized");
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
            Console.WriteLine($"[DEBUG] ==========================================");
            Console.WriteLine($"[DEBUG] SUBTITLE SEARCH STARTED");
            Console.WriteLine($"[DEBUG] OpenSubtitlesProvider.Search called for: {request.MediaPath}");
            Console.WriteLine($"[DEBUG] Language: {request.Language}");
            Console.WriteLine($"[DEBUG] ==========================================");
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
                var finalResults = searchResults
                    .GroupBy(s => s.Id)
                    .Select(g => g.First())
                    .Take(config.MaxSearchResults)
                    .ToList();

                Console.WriteLine($"[DEBUG] ==========================================");
                Console.WriteLine($"[DEBUG] SUBTITLE SEARCH COMPLETED");
                Console.WriteLine($"[DEBUG] Total found: {searchResults.Count}");
                Console.WriteLine($"[DEBUG] After deduplication: {finalResults.Count}");
                Console.WriteLine($"[DEBUG] Final results:");
                for (int i = 0; i < finalResults.Count; i++)
                {
                    Console.WriteLine($"[DEBUG] {i + 1}. ID: {finalResults[i].Id}");
                    Console.WriteLine($"[DEBUG]    Name: {finalResults[i].Name}");
                    Console.WriteLine($"[DEBUG]    Provider: {finalResults[i].ProviderName}");
                }
                Console.WriteLine($"[DEBUG] ==========================================");

                return finalResults;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] ==========================================");
                Console.WriteLine($"[DEBUG] SUBTITLE SEARCH FAILED");
                Console.WriteLine($"[DEBUG] ERROR: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack Trace: {ex.StackTrace}");
                Console.WriteLine($"[DEBUG] ==========================================");
                _logger.LogError(ex, "Error searching for subtitles");
                return new List<RemoteSubtitleInfo>();
            }
        }

        /// <inheritdoc />
        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[DEBUG] ==========================================");
            Console.WriteLine($"[DEBUG] DOWNLOAD BUTTON CLICKED!");
            Console.WriteLine($"[DEBUG] OpenSubtitlesProvider.GetSubtitles called for ID: {id}");
            Console.WriteLine($"[DEBUG] ==========================================");
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
                        Console.WriteLine($"[DEBUG] Decoded simple ID:");
                        Console.WriteLine($"[DEBUG] - Language: {subtitleLanguage}");
                        Console.WriteLine($"[DEBUG] - Subtitle ID: {subtitleId}");
                        Console.WriteLine($"[DEBUG] - Download URL: {downloadUrl}");
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG] ERROR: Invalid simple ID format: {id}");
                        throw new ArgumentException($"Invalid subtitle ID format: {id}");
                    }
                }
                else if (id.StartsWith("http"))
                {
                    // If it's already a full URL, use it directly
                    downloadUrl = id;
                    subtitleLanguage = "en"; // default when no language in URL
                    Console.WriteLine($"[DEBUG] Using full URL directly: {downloadUrl}");
                }
                else
                {
                    Console.WriteLine($"[DEBUG] ERROR: Unrecognized ID format: {id}");
                    throw new ArgumentException($"Unrecognized subtitle ID format: {id}");
                }
            
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                httpClient.DefaultRequestHeaders.Add("Referer", "https://www.opensubtitles.org/");
                
                Console.WriteLine($"[DEBUG] Step 1: HTTP client configured");
                Console.WriteLine($"[DEBUG] Step 2: Attempting to download from URL: {downloadUrl}");
                
                var response = await httpClient.GetAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
                
                Console.WriteLine($"[DEBUG] Step 3: HTTP Response received");
                Console.WriteLine($"[DEBUG] - Status Code: {response.StatusCode}");
                Console.WriteLine($"[DEBUG] - Status: {(response.IsSuccessStatusCode ? "SUCCESS" : "FAILED")}");
                Console.WriteLine($"[DEBUG] - Reason Phrase: {response.ReasonPhrase}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[DEBUG] ERROR: Download failed with status: {response.StatusCode}");
                    throw new HttpRequestException($"Failed to download subtitle: {response.StatusCode} - {response.ReasonPhrase}");
                }

                var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                Console.WriteLine($"[DEBUG] Step 4: Content downloaded - size: {content.Length} bytes");
                
                // Check if this is a ZIP file and extract subtitle content
                byte[] subtitleContent;
                if (IsZipFile(content))
                {
                    Console.WriteLine($"[DEBUG] Step 5a: Content is ZIP file - extracting subtitle");
                    var extractedContent = ExtractSubtitleFromZip(content);
                    if (extractedContent == null)
                    {
                        Console.WriteLine($"[DEBUG] ERROR: Failed to extract subtitle from ZIP");
                        throw new InvalidOperationException("Failed to extract subtitle from ZIP file");
                    }
                    subtitleContent = extractedContent;
                    Console.WriteLine($"[DEBUG] Step 5b: Successfully extracted subtitle from ZIP - size: {subtitleContent.Length} bytes");
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Step 5a: Content is direct subtitle file");
                    subtitleContent = content;
                }
                
                var format = DetectSubtitleFormat(subtitleContent);
                Console.WriteLine($"[DEBUG] Step 6: Detected subtitle format: {format}");

                Console.WriteLine($"[DEBUG] ==========================================");
                Console.WriteLine($"[DEBUG] DOWNLOAD COMPLETED SUCCESSFULLY!");
                Console.WriteLine($"[DEBUG] - Format: {format}");
                Console.WriteLine($"[DEBUG] - Size: {subtitleContent.Length} bytes");
                Console.WriteLine($"[DEBUG] - Language: {subtitleLanguage}");
                Console.WriteLine($"[DEBUG] - Was ZIP: {IsZipFile(content)}");
                Console.WriteLine($"[DEBUG] ==========================================");

                return new SubtitleResponse
                {
                    Language = subtitleLanguage,
                    Stream = new MemoryStream(subtitleContent),
                    Format = format
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] ==========================================");
                Console.WriteLine($"[DEBUG] DOWNLOAD FAILED!");
                Console.WriteLine($"[DEBUG] ERROR: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack Trace: {ex.StackTrace}");
                Console.WriteLine($"[DEBUG] ==========================================");
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
                Console.WriteLine($"[DEBUG] Found {subtitleRows.Count} potential subtitle rows");
                
                foreach (var row in subtitleRows.Take(config.MaxSearchResults))
                {
                    try
                    {
                        // Get the full row text for parsing
                        var rowText = row.InnerText ?? "";
                        Console.WriteLine($"[DEBUG] Raw row text: '{rowText.Substring(0, Math.Min(200, rowText.Length))}...'");
                        
                        // Look for subtitle page links to extract subtitle ID
                        var subtitleLinkNode = row.SelectSingleNode(".//a[contains(@href, '/subtitles/')]");
                        if (subtitleLinkNode == null) continue;

                        var subtitlePageUrl = subtitleLinkNode.GetAttributeValue("href", "");
                        if (string.IsNullOrEmpty(subtitlePageUrl)) continue;

                        // Extract subtitle ID from URL like /subtitles/123456/subtitle-name
                        var idMatch = Regex.Match(subtitlePageUrl, @"/subtitles/(\d+)");
                        if (!idMatch.Success) continue;
                        
                        var subtitleId = idMatch.Groups[1].Value;
                        
                        // Extract better subtitle name from the row structure
                        var name = ExtractSubtitleName(row, rowText, subtitleLinkNode.InnerText?.Trim() ?? "Unknown");
                        
                        // Create simple ID format that Jellyfin can handle
                        var simpleId = $"srt-{language}-{subtitleId}";
                        
                        Console.WriteLine($"[DEBUG] Found subtitle:");
                        Console.WriteLine($"[DEBUG] - Name: {name}");
                        Console.WriteLine($"[DEBUG] - Subtitle ID: {subtitleId}");
                        Console.WriteLine($"[DEBUG] - Simple ID: {simpleId}");

                        var result = new RemoteSubtitleInfo
                        {
                            Id = simpleId,
                            Name = name,
                            ProviderName = Name,
                            ThreeLetterISOLanguageName = MapLanguageCode(language),
                            Format = "srt",
                            IsHashMatch = false
                        };

                        results.Add(result);
                        Console.WriteLine($"[DEBUG] Added subtitle: {name} - ID: {simpleId}");
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

        private string ExtractSubtitleName(HtmlNode row, string rowText, string fallbackName)
        {
            try
            {
                // Method 1: Try to extract from table cells - look for movie title in first few cells
                var cells = row.SelectNodes(".//td");
                if (cells != null && cells.Count > 0)
                {
                    for (int i = 0; i < Math.Min(3, cells.Count); i++)
                    {
                        var cellText = cells[i].InnerText?.Trim() ?? "";
                        // Skip cells that are just numbers, dates, or common words
                        if (!string.IsNullOrEmpty(cellText) && 
                            !Regex.IsMatch(cellText, @"^\d+$") && // not just numbers
                            !Regex.IsMatch(cellText, @"^\d{4}-\d{2}-\d{2}$") && // not dates
                            !cellText.Equals("English", StringComparison.OrdinalIgnoreCase) &&
                            !cellText.Equals("Spanish", StringComparison.OrdinalIgnoreCase) &&
                            !cellText.Contains("CD") &&
                            cellText.Length > 3)
                        {
                            // Clean up the cell text
                            var cleanName = Regex.Replace(cellText, @"\s*(Watch online|Download|Watch|Online|Subtitles)\s*", "", RegexOptions.IgnoreCase).Trim();
                            if (cleanName.Length > 3)
                            {
                                Console.WriteLine($"[DEBUG] Extracted name from cell {i}: '{cleanName}'");
                                return cleanName;
                            }
                        }
                    }
                }

                // Method 2: Try to parse from the full row text
                // Look for patterns like "Movie Title (Year)" or "Series S01E01" at the beginning
                var titleMatch = Regex.Match(rowText, @"^([^|]+?)(?:\s*\|\s*|\s*Watch\s*online|\s*Download|\s*\d+CD)", RegexOptions.IgnoreCase);
                if (titleMatch.Success)
                {
                    var title = titleMatch.Groups[1].Value.Trim();
                    if (title.Length > 3 && !title.Equals("English", StringComparison.OrdinalIgnoreCase))
                    {
                        // Further clean up
                        title = Regex.Replace(title, @"\s*(Watch online|Download|Watch|Online|Subtitles)\s*", "", RegexOptions.IgnoreCase).Trim();
                        if (title.Length > 3)
                        {
                            Console.WriteLine($"[DEBUG] Extracted name from row text: '{title}'");
                            return title;
                        }
                    }
                }

                // Method 3: Use the link text as fallback, but clean it up
                var cleanFallback = Regex.Replace(fallbackName, @"\s*(Watch online|Download|Watch|Online|Subtitles)\s*", "", RegexOptions.IgnoreCase).Trim();
                Console.WriteLine($"[DEBUG] Using cleaned fallback name: '{cleanFallback}'");
                return string.IsNullOrEmpty(cleanFallback) ? "Unknown Subtitle" : cleanFallback;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error extracting subtitle name: {ex.Message}");
                return fallbackName ?? "Unknown Subtitle";
            }
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

                Console.WriteLine($"[DEBUG] ZIP contains {archive.Entries.Count} entries");

                // Look for subtitle files in the ZIP
                var subtitleExtensions = new[] { ".srt", ".ass", ".ssa", ".sub", ".vtt", ".txt" };
                
                foreach (var entry in archive.Entries)
                {
                    Console.WriteLine($"[DEBUG] ZIP entry: {entry.Name} (size: {entry.Length})");
                    
                    if (subtitleExtensions.Any(ext => entry.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    {
                        Console.WriteLine($"[DEBUG] Extracting subtitle file: {entry.Name}");
                        
                        using var entryStream = entry.Open();
                        using var memoryStream = new MemoryStream();
                        entryStream.CopyTo(memoryStream);
                        
                        var content = memoryStream.ToArray();
                        Console.WriteLine($"[DEBUG] Extracted {content.Length} bytes from {entry.Name}");
                        
                        return content;
                    }
                }

                Console.WriteLine($"[DEBUG] No subtitle files found in ZIP");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error extracting from ZIP: {ex.Message}");
                return null;
            }
        }
    }
}
