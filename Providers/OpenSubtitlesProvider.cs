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
            
            Console.WriteLine("[DEBUG] OpenSubtitlesProvider constructor called");
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
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                httpClient.DefaultRequestHeaders.Add("Referer", "https://www.opensubtitles.org/");
                
                Console.WriteLine($"[DEBUG] Attempting to download: {id}");
                var response = await httpClient.GetAsync(id, cancellationToken).ConfigureAwait(false);
                Console.WriteLine($"[DEBUG] Download response status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[DEBUG] Download failed with status: {response.StatusCode}");
                    throw new HttpRequestException($"Failed to download subtitle: {response.StatusCode}");
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                var contentLength = response.Content.Headers.ContentLength ?? 0;
                Console.WriteLine($"[DEBUG] Content type: {contentType}, Length: {contentLength}");

                // Read the response content
                var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                Console.WriteLine($"[DEBUG] Downloaded content size: {content.Length}");

                // Check if this is a zip file
                if (contentType.Contains("zip") || id.Contains(".zip") || IsZipFile(content))
                {
                    Console.WriteLine("[DEBUG] Processing zip file");
                    
                    // Extract subtitle from zip
                    var subtitleContent = ExtractSubtitleFromZip(content);
                    if (subtitleContent != null)
                    {
                        Console.WriteLine($"[DEBUG] Extracted subtitle from zip, size: {subtitleContent.Length}");
                        return new SubtitleResponse
                        {
                            Language = "en", // This should be detected from the subtitle request
                            Stream = new MemoryStream(subtitleContent),
                            Format = DetectSubtitleFormat(subtitleContent)
                        };
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] Failed to extract subtitle from zip");
                        throw new InvalidOperationException("Failed to extract subtitle from zip file");
                    }
                }
                else
                {
                    // Direct subtitle file
                    Console.WriteLine("[DEBUG] Processing direct subtitle file");
                    
                    // If it's HTML content, it might be a download page - extract the actual download link
                    if (contentType.Contains("html"))
                    {
                        var htmlContent = Encoding.UTF8.GetString(content);
                        var actualDownloadUrl = ExtractActualDownloadUrl(htmlContent, id);
                        
                        if (!string.IsNullOrEmpty(actualDownloadUrl))
                        {
                            Console.WriteLine($"[DEBUG] Found actual download URL: {actualDownloadUrl}");
                            return await GetSubtitles(actualDownloadUrl, cancellationToken);
                        }
                    }
                    
                    return new SubtitleResponse
                    {
                        Language = "en", // This should be detected from the subtitle request
                        Stream = new MemoryStream(content),
                        Format = DetectSubtitleFormat(content)
                    };
                }
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

                // The new approach: look for table rows that contain subtitle information
                // Based on the actual HTML structure, we need to find rows with download links
                var subtitleRows = doc.DocumentNode.SelectNodes("//tr[contains(., 'Download Subtitles Searcher')]") ??
                                  doc.DocumentNode.SelectNodes("//tr[td and contains(., 'srt')]") ??
                                  doc.DocumentNode.SelectNodes("//tr[td]");
                
                if (subtitleRows == null)
                {
                    Console.WriteLine("[DEBUG] No subtitle rows found");
                    return results;
                }

                Console.WriteLine($"[DEBUG] Found {subtitleRows.Count} potential subtitle rows");
                
                var processedCount = 0;
                foreach (var row in subtitleRows)
                {
                    if (processedCount >= config.MaxSearchResults) break;

                    try
                    {
                        var rowText = row.InnerText ?? "";
                        
                        // Skip rows that don't look like subtitle entries
                        if (!rowText.Contains("srt") && !rowText.Contains("sub") && !rowText.Contains("vtt")) 
                            continue;
                            
                        if (!rowText.Contains("Download Subtitles Searcher") && !rowText.Contains("Watch online"))
                            continue;

                        // Debug: log the raw row text to understand the format
                        Console.WriteLine($"[DEBUG] Raw row text: '{rowText}'");

                        // Extract the movie title (first part before "Watch online", "Download", or other markers)
                        var titleMatch = Regex.Match(rowText, @"^([^|]+?)(?:\s*Watch\s*online(?:\s*Download)?|\s*Download|\s*Subtitles|\s*\d+CD)", RegexOptions.IgnoreCase);
                        var movieTitle = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : "Unknown Movie";
                        
                        Console.WriteLine($"[DEBUG] Title before cleanup: '{movieTitle}'");
                        
                        // Clean up the title - remove any trailing "Watch online", "Download", etc. (multiple passes)
                        movieTitle = Regex.Replace(movieTitle, @"\s*(Watch\s*online|Download|Subtitles|Watch\s*onlineDownload)\s*$", "", RegexOptions.IgnoreCase);
                        movieTitle = Regex.Replace(movieTitle, @"(Watch\s*online|Download|Subtitles)", "", RegexOptions.IgnoreCase);
                        movieTitle = Regex.Replace(movieTitle, @"\s+", " ").Trim();
                        if (movieTitle.Length > 100) movieTitle = movieTitle.Substring(0, 100) + "...";

                        Console.WriteLine($"[DEBUG] Title after cleanup: '{movieTitle}'");

                        // Look for subtitle details in the row
                        var isHearingImpaired = rowText.Contains("hearing impaired");
                        var isHighDef = rowText.Contains("high-definition");
                        var isTrusted = rowText.Contains("trusted") || rowText.Contains("gold member") || rowText.Contains("platinum member");
                        
                        // Extract uploader info
                        var uploaderMatch = Regex.Match(rowText, @"(\w+)\s+(gold|platinum|trusted)\s+member", RegexOptions.IgnoreCase);
                        var uploader = uploaderMatch.Success ? uploaderMatch.Groups[1].Value : "";

                        // Extract language info (should be in one of the cells)
                        var cells = row.SelectNodes(".//td");
                        var detectedLanguage = "English"; // Default
                        if (cells != null && cells.Count > 1)
                        {
                            for (int i = 1; i < Math.Min(cells.Count, 4); i++)
                            {
                                var cellText = cells[i].InnerText?.Trim() ?? "";
                                if (cellText.Equals("English", StringComparison.OrdinalIgnoreCase) ||
                                    cellText.Equals("Spanish", StringComparison.OrdinalIgnoreCase) ||
                                    cellText.Equals("French", StringComparison.OrdinalIgnoreCase) ||
                                    cellText.Length == 2 || cellText.Length == 3)
                                {
                                    detectedLanguage = cellText;
                                    break;
                                }
                            }
                        }

                        // Build subtitle display name with metadata
                        var displayName = movieTitle;
                        var nameDetails = new List<string>();
                        
                        if (isHearingImpaired) nameDetails.Add("HI");
                        if (isHighDef) nameDetails.Add("HD");
                        if (isTrusted) nameDetails.Add("Trusted");
                        if (!string.IsNullOrEmpty(uploader)) nameDetails.Add($"by {uploader}");
                        
                        if (nameDetails.Count > 0)
                        {
                            displayName += $" [{string.Join(", ", nameDetails)}]";
                        }

                        // For the download URL, we need to construct it based on the OpenSubtitles pattern
                        // Since we can't find direct download links in the HTML, we'll try to extract 
                        // subtitle IDs and construct the download URL
                        
                        // Look for any links that might lead to subtitle pages
                        var linkNodes = row.SelectNodes(".//a[@href]");
                        string downloadUrl = "";
                        
                        if (linkNodes != null)
                        {
                            foreach (var link in linkNodes)
                            {
                                var href = link.GetAttributeValue("href", "");
                                if (href.Contains("/subtitles/"))
                                {
                                    // Extract subtitle ID from URL like /subtitles/123456/subtitle-name
                                    var idMatch = Regex.Match(href, @"/subtitles/(\d+)");
                                    if (idMatch.Success)
                                    {
                                        var subtitleId = idMatch.Groups[1].Value;
                                        downloadUrl = $"https://www.opensubtitles.org/en/download/sub/{subtitleId}";
                                        break;
                                    }
                                }
                                else if (href.Contains("/download/"))
                                {
                                    downloadUrl = href.StartsWith("http") ? href : "https://www.opensubtitles.org" + href;
                                    break;
                                }
                            }
                        }

                        // If we still don't have a download URL, skip this entry
                        if (string.IsNullOrEmpty(downloadUrl))
                        {
                            Console.WriteLine($"[DEBUG] No download URL found for: {movieTitle}");
                            continue;
                        }

                        var result = new RemoteSubtitleInfo
                        {
                            Id = downloadUrl,
                            Name = displayName,
                            ProviderName = Name,
                            ThreeLetterISOLanguageName = MapLanguageToThreeLetterCode(detectedLanguage) ?? language,
                            Format = "srt",
                            IsHashMatch = false
                        };

                        results.Add(result);
                        processedCount++;
                        Console.WriteLine($"[DEBUG] Found subtitle: {displayName} - {downloadUrl}");
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

            Console.WriteLine($"[DEBUG] Returning {results.Count} results");
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

        private static string? MapLanguageToThreeLetterCode(string language)
        {
            if (string.IsNullOrEmpty(language))
                return null;

            // Handle common language names
            var langLower = language.ToLowerInvariant();
            return langLower switch
            {
                "english" => "eng",
                "spanish" => "spa",
                "french" => "fre",
                "german" => "ger",
                "italian" => "ita",
                "portuguese" => "por",
                "russian" => "rus",
                "japanese" => "jpn",
                "korean" => "kor",
                "chinese" => "chi",
                "arabic" => "ara",
                "hindi" => "hin",
                "dutch" => "dut",
                "swedish" => "swe",
                "norwegian" => "nor",
                "danish" => "dan",
                "finnish" => "fin",
                "polish" => "pol",
                "czech" => "cze",
                "hungarian" => "hun",
                "turkish" => "tur",
                _ => language.Length == 3 ? language.ToLowerInvariant() : null
            };
        }

        private static bool IsZipFile(byte[] content)
        {
            // Check for ZIP file magic number
            return content.Length >= 4 && 
                   content[0] == 0x50 && content[1] == 0x4B && 
                   (content[2] == 0x03 || content[2] == 0x05 || content[2] == 0x07) && 
                   (content[3] == 0x04 || content[3] == 0x06 || content[3] == 0x08);
        }

        private static byte[]? ExtractSubtitleFromZip(byte[] zipContent)
        {
            try
            {
                using var zipStream = new MemoryStream(zipContent);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                
                // Find the first subtitle file
                var subtitleEntry = archive.Entries.FirstOrDefault(e => 
                    e.Name.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) ||
                    e.Name.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) ||
                    e.Name.EndsWith(".sub", StringComparison.OrdinalIgnoreCase) ||
                    e.Name.EndsWith(".ass", StringComparison.OrdinalIgnoreCase));

                if (subtitleEntry == null)
                {
                    Console.WriteLine("[DEBUG] No subtitle file found in zip");
                    return null;
                }

                using var entryStream = subtitleEntry.Open();
                using var ms = new MemoryStream();
                entryStream.CopyTo(ms);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error extracting from zip: {ex.Message}");
                return null;
            }
        }

        private static string DetectSubtitleFormat(byte[] content)
        {
            var text = Encoding.UTF8.GetString(content, 0, Math.Min(content.Length, 1000));
            
            if (text.Contains("WEBVTT"))
                return "vtt";
            if (text.Contains("[Script Info]"))
                return "ass";
            if (Regex.IsMatch(text, @"^\d+\s*$", RegexOptions.Multiline))
                return "srt";
            
            // Default to SRT
            return "srt";
        }

        private string? ExtractActualDownloadUrl(string htmlContent, string currentUrl)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);
            
            // Look for direct download links
            var downloadLink = doc.DocumentNode.SelectSingleNode("//a[contains(@href, '/subtitleserve/')]") ??
                              doc.DocumentNode.SelectSingleNode("//a[contains(@href, '/download/')]") ??
                              doc.DocumentNode.SelectSingleNode("//a[contains(text(), 'Download')]");

            if (downloadLink != null)
            {
                var href = downloadLink.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(href))
                {
                    if (!href.StartsWith("http"))
                    {
                        href = "https://www.opensubtitles.org" + href;
                    }
                    return href;
                }
            }

            return null;
        }
    }
}
