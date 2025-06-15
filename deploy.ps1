# OpenSubtitles Grabber Plugin Deployment Script
# This script builds and deploys the plugin to Jellyfin

param(
    [string]$JellyfinPluginsPath = "C:\jellyfin\data\plugins\OpenSubtitlesGrabber",
    [switch]$Clean
)

Write-Host "=== OpenSubtitles Grabber Plugin Deployment ===" -ForegroundColor Green

# Get the project directory
$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ProjectDir "OpenSubtitlesGrabber.csproj"

Write-Host "Project Directory: $ProjectDir" -ForegroundColor Cyan
Write-Host "Target Directory: $JellyfinPluginsPath" -ForegroundColor Cyan

# Check if project file exists
if (-not (Test-Path $ProjectFile)) {
    Write-Error "Project file not found: $ProjectFile"
    exit 1
}

try {
    # Clean previous build if requested
    if ($Clean) {
        Write-Host "Cleaning previous build..." -ForegroundColor Yellow
        dotnet clean $ProjectFile
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Clean failed"
            exit 1
        }
    }

    # Build the project in Release mode
    Write-Host "Building project..." -ForegroundColor Yellow
    dotnet build $ProjectFile -c Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }

    # Create target directory if it doesn't exist
    if (-not (Test-Path $JellyfinPluginsPath)) {
        Write-Host "Creating plugin directory: $JellyfinPluginsPath" -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $JellyfinPluginsPath -Force | Out-Null
    }

    # Source directory for built files
    $SourceDir = Join-Path $ProjectDir "bin\Release\net8.0"
    
    if (-not (Test-Path $SourceDir)) {
        Write-Error "Build output directory not found: $SourceDir"
        exit 1
    }

    # Copy plugin files
    Write-Host "Copying plugin files..." -ForegroundColor Yellow
    
    # Copy main plugin DLL
    $MainDll = Join-Path $SourceDir "OpenSubtitlesGrabber.dll"
    if (Test-Path $MainDll) {
        Copy-Item $MainDll $JellyfinPluginsPath -Force
        Write-Host "  ✓ Copied OpenSubtitlesGrabber.dll" -ForegroundColor Green
    }

    # Copy dependencies if they exist
    $Dependencies = @(
        "HtmlAgilityPack.dll"
    )

    foreach ($dep in $Dependencies) {
        $depPath = Join-Path $SourceDir $dep
        if (Test-Path $depPath) {
            Copy-Item $depPath $JellyfinPluginsPath -Force
            Write-Host "  ✓ Copied $dep" -ForegroundColor Green
        }
    }

    Write-Host ""
    Write-Host "=== Deployment Successful! ===" -ForegroundColor Green
    Write-Host "Plugin deployed to: $JellyfinPluginsPath" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Restart Jellyfin server" -ForegroundColor White
    Write-Host "2. Go to Dashboard > Plugins to verify the plugin is loaded" -ForegroundColor White
    Write-Host "3. Configure the plugin if needed" -ForegroundColor White

} catch {
    Write-Error "Deployment failed: $($_.Exception.Message)"
    exit 1
}
