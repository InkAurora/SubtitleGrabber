@echo off
echo Building Jellyfin OpenSubtitles Grabber Plugin...

REM Check if .NET SDK is installed
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo .NET SDK is not installed or not in PATH
    echo Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo .NET SDK found, building project...
dotnet build --configuration Release

if %errorlevel% equ 0 (
    echo Build completed successfully!
    echo.
    echo To install the plugin:
    echo 1. Copy the contents of bin\Release\net8.0\ to your Jellyfin plugins directory
    echo 2. Create a folder named "OpenSubtitlesGrabber" in the plugins directory
    echo 3. Restart Jellyfin
    echo.
    echo Plugin installation paths:
    echo - Windows: %%ProgramData%%\Jellyfin\Server\plugins\OpenSubtitlesGrabber\
    echo - Linux: /var/lib/jellyfin/plugins/OpenSubtitlesGrabber/
    echo - Docker: /config/plugins/OpenSubtitlesGrabber/
) else (
    echo Build failed with error code %errorlevel%
)

pause
