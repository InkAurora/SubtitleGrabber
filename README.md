# OpenSubtitles Grabber Plugin for Jellyfin

A Jellyfin plugin that downloads subtitles from OpenSubtitles.org by scraping their website, without requiring API registration or keys.

## Features

- 🔍 **No API Required**: Scrapes OpenSubtitles.org directly without needing API keys
- 🌍 **Multi-language Support**: Supports 24+ languages including English, Spanish, French, German, and more
- 🎬 **Movie & TV Show Support**: Works with both movies and TV episodes
- ⚙️ **Configurable**: Customizable search results limit, timeout settings, and format preferences
- 🎯 **Smart Search**: Searches by title, IMDB ID, and episode information
- 📝 **Multiple Formats**: Supports SRT, VTT, and ASS subtitle formats
- ♿ **Accessibility**: Option to prefer hearing-impaired subtitles

## Installation

### Manual Installation

1. Download the latest release from the [Releases](https://github.com/InkAurora/SubtitleGrabber/releases) page
2. Extract the files to your Jellyfin plugins directory:
   - **Windows**: `%ProgramData%\Jellyfin\Server\plugins\OpenSubtitlesGrabber\`
   - **Linux**: `/var/lib/jellyfin/plugins/OpenSubtitlesGrabber/`
   - **Docker**: `/config/plugins/OpenSubtitlesGrabber/`
3. Restart Jellyfin
4. Go to **Dashboard → Plugins → OpenSubtitles Grabber** to configure

### Build from Source

1. Clone this repository
2. Ensure you have .NET 8.0 SDK installed
3. Build the project:
   ```bash
   dotnet build --configuration Release
   ```
4. Copy the built files to your Jellyfin plugins directory

## Configuration

The plugin can be configured through the Jellyfin web interface:

1. Navigate to **Dashboard → Plugins → OpenSubtitles Grabber**
2. Configure the following options:
   - **Enable Debug Logging**: Enable detailed logging for troubleshooting
   - **Maximum Search Results**: Number of subtitle results to process (1-50)
   - **Request Timeout**: Timeout for web requests in seconds (5-120)
   - **Prefer Hearing Impaired**: Prefer subtitles for hearing-impaired viewers
   - **Preferred Format**: Choose between SRT, VTT, or ASS formats

## Usage

1. Play a movie or TV episode in Jellyfin
2. Click the subtitle button (CC icon)
3. Select "Edit subtitles"
4. The plugin will automatically search OpenSubtitles.org
5. Choose from the available subtitle options

## Supported Languages

The plugin supports the following languages:

- English (en)
- Spanish (es)
- French (fr)
- German (de)
- Italian (it)
- Portuguese (pt)
- Russian (ru)
- Japanese (ja)
- Korean (ko)
- Chinese (zh)
- Arabic (ar)
- Hindi (hi)
- Dutch (nl)
- Swedish (sv)
- Norwegian (no)
- Danish (da)
- Finnish (fi)
- Polish (pl)
- Czech (cs)
- Hungarian (hu)
- Turkish (tr)
- Hebrew (he)
- Thai (th)
- Vietnamese (vi)

## How It Works

This plugin works by:

1. **Web Scraping**: Directly scrapes OpenSubtitles.org search results
2. **HTML Parsing**: Uses HtmlAgilityPack to parse subtitle listings
3. **Smart Matching**: Matches subtitles based on movie/show titles and episode information
4. **Direct Download**: Downloads subtitle files directly from OpenSubtitles.org
5. **Format Detection**: Automatically detects subtitle format (SRT, VTT, ASS)

## Limitations

- **Rate Limiting**: May be subject to OpenSubtitles.org rate limiting
- **Website Changes**: Functionality depends on OpenSubtitles.org website structure
- **No User Ratings**: Cannot access user-specific ratings or preferences
- **Limited Metadata**: Less metadata compared to official API

## Troubleshooting

### Common Issues

1. **No subtitles found**:

   - Check if the movie/show exists on OpenSubtitles.org
   - Try enabling debug logging for more information
   - Verify your internet connection

2. **Download failures**:

   - Increase the request timeout in settings
   - Check Jellyfin logs for specific error messages

3. **Plugin not appearing**:
   - Ensure files are in the correct plugins directory
   - Restart Jellyfin completely
   - Check Jellyfin logs for plugin loading errors

### Debug Logging

Enable debug logging in the plugin configuration to get detailed information about:

- Search queries and results
- Download attempts
- HTML parsing issues
- Network errors

## Legal Notice

This plugin scrapes public content from OpenSubtitles.org. Please:

- Respect OpenSubtitles.org's terms of service
- Use reasonable request intervals to avoid overloading their servers
- Consider supporting OpenSubtitles.org if you find their service valuable

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT license. See the [LICENSE](LICENSE) file for details.

## Disclaimer

This plugin is not affiliated with or endorsed by OpenSubtitles.org. It is a community-created tool that interacts with their public website. Use at your own discretion and in accordance with OpenSubtitles.org's terms of service.
