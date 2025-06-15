@echo off
REM OpenSubtitles Grabber Plugin Deployment Script (Batch Version)
echo === OpenSubtitles Grabber Plugin Deployment ===

REM Set variables
set JELLYFIN_PLUGINS_PATH=C:\jellyfin\data\plugins\OpenSubtitlesGrabber
set PROJECT_FILE=OpenSubtitlesGrabber.csproj

echo Building project...
dotnet build %PROJECT_FILE% -c Release --no-restore
if %ERRORLEVEL% neq 0 (
    echo Build failed!
    exit /b 1
)

echo Creating plugin directory...
if not exist "%JELLYFIN_PLUGINS_PATH%" (
    mkdir "%JELLYFIN_PLUGINS_PATH%"
)

echo Copying plugin files...
copy "bin\Release\net8.0\OpenSubtitlesGrabber.dll" "%JELLYFIN_PLUGINS_PATH%\" /Y

REM Copy HtmlAgilityPack if it exists
if exist "bin\Release\net8.0\HtmlAgilityPack.dll" (
    copy "bin\Release\net8.0\HtmlAgilityPack.dll" "%JELLYFIN_PLUGINS_PATH%\" /Y
    echo Copied HtmlAgilityPack.dll
)

echo.
echo === Deployment Successful! ===
echo Plugin deployed to: %JELLYFIN_PLUGINS_PATH%
echo.
echo Next steps:
echo 1. Restart Jellyfin server
echo 2. Go to Dashboard ^> Plugins to verify the plugin is loaded
echo 3. Configure the plugin if needed
echo.
