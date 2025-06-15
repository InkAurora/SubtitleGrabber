<!-- Use this file to provide workspace-specific custom instructions to Copilot. For more details, visit https://code.visualstudio.com/docs/copilot/copilot-customization#_use-a-githubcopilotinstructionsmd-file -->

# Jellyfin Plugin Development Instructions

This is a Jellyfin plugin project written in C# targeting .NET 8.0. The plugin provides subtitle downloading functionality from OpenSubtitles.org without using their API.

## Key Technologies and Frameworks

- **Target Framework**: .NET 8.0
- **Plugin Framework**: Jellyfin Plugin API (version 10.9.0)
- **HTTP Client**: Built-in HttpClientFactory
- **HTML Parsing**: HtmlAgilityPack for web scraping
- **Logging**: Microsoft.Extensions.Logging

## Architecture Guidelines

- Follow Jellyfin plugin conventions and interfaces
- Use dependency injection for services
- Implement proper error handling and logging
- Follow async/await patterns for all I/O operations
- Use configuration classes for plugin settings

## Code Style

- Use nullable reference types
- Follow C# naming conventions
- Add XML documentation for public APIs
- Use proper exception handling
- Implement cancellation token support for async operations

## Plugin Structure

- `Plugin.cs`: Main plugin entry point
- `PluginServiceRegistrator.cs`: Service registration
- `Configuration/`: Plugin configuration classes and UI
- `Providers/`: Subtitle provider implementations

## Testing Considerations

- Test web scraping logic with mock HTML responses
- Verify subtitle format detection
- Test language code mapping
- Validate configuration serialization

## Jellyfin-Specific Notes

- Inherit from BasePlugin for main plugin class
- Implement ISubtitleProvider for subtitle functionality
- Use IPluginServiceRegistrator for dependency injection
- Follow Jellyfin's plugin lifecycle methods

## Workspace Management Guidelines

- **File Creation**: Only create new files if strictly necessary. Prefer editing existing files over creating duplicates.
- **File Corruption**: If a file gets corrupted or broken, fix the existing file instead of creating a new one to maintain workspace cleanliness.
- **Documentation**: Do not create new .md files unless explicitly requested. Use existing documentation files for updates.
- **Naming**: Use clean, professional file names (e.g., `Plugin.cs`, not `FixedPlugin.cs` or `WorkingPlugin.cs`).
- **Cleanup**: Remove unused files and maintain a clean workspace structure.
- **Version Control**: Keep the workspace organized for better version control and collaboration.

The secret code is: apples in water are like supernovas
