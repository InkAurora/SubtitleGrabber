@echo off
REM OpenSubtitles Grabber Plugin Deployment Script (Batch Version)
echo === OpenSubtitles Grabber Plugin Deployment ===

REM Set variables
set JELLYFIN_PLUGINS_PATH=C:\jellyfin\data\plugins\OpenSubtitlesGrabber
set PROJECT_FILE=OpenSubtitlesGrabber.csproj
set JELLYFIN_BATCH=C:\jellyfin\jellyfin.bat

echo Stopping Jellyfin server...
taskkill /f /im jellyfin.exe >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo Jellyfin process terminated
) else (
    echo No Jellyfin process found running
)

REM Wait a moment for the process to fully stop
timeout /t 2 /nobreak >nul

echo Building project...
dotnet build %PROJECT_FILE% -c Release --no-restore
if %ERRORLEVEL% neq 0 (
    echo Build failed!
    echo.
    echo Attempting to restart Jellyfin...
    cd /d "C:\jellyfin"
    start "" "jellyfin.bat"
    cd /d "%~dp0"
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
echo Starting Jellyfin server...
cd /d "C:\jellyfin"
start "" "jellyfin.bat"
cd /d "%~dp0"

echo.
echo === Deployment Complete! ===
echo Jellyfin is starting up with the updated plugin
echo Wait a few moments for Jellyfin to fully start, then:
echo 1. Go to Dashboard ^> Plugins to verify the plugin is loaded
echo 2. Configure the plugin if needed
echo 3. Test subtitle search and download functionality
echo.
