# Simple Version Update Script for y0daii IRC Client
# Usage: .\update-version-simple.ps1 [version] [build]
# Example: .\update-version-simple.ps1 1.0.6 20251022

param(
    [string]$Version = "",
    [string]$Build = ""
)

# Get current date for build if not provided
if ([string]::IsNullOrEmpty($Build)) {
    $Build = Get-Date -Format "yyyyMMdd"
}

# Auto-increment version if not provided
if ([string]::IsNullOrEmpty($Version)) {
    # Read current version from project file
    $projectFile = "Y0daiiIRC.csproj"
    if (Test-Path $projectFile) {
        $content = Get-Content $projectFile -Raw
        if ($content -match '<AssemblyVersion>(\d+)\.(\d+)\.(\d+)\.(\d+)</AssemblyVersion>') {
            $major = [int]$matches[1]
            $minor = [int]$matches[2]
            $patch = [int]$matches[3]
            $revision = [int]$matches[4]
            
            # Increment patch version
            $patch++
            $Version = "$major.$minor.$patch.0"
        } else {
            $Version = "1.0.1.0"
        }
    } else {
        $Version = "1.0.1.0"
    }
}

Write-Host "Updating version to: $Version (Build: $Build)" -ForegroundColor Green

# Update the project file
$projectFile = "Y0daiiIRC.csproj"
if (Test-Path $projectFile) {
    $content = Get-Content $projectFile -Raw
    
    # Update version properties
    $content = $content -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$Version</AssemblyVersion>"
    $content = $content -replace '<FileVersion>.*?</FileVersion>', "<FileVersion>$Version</FileVersion>"
    $content = $content -replace '<Version>.*?</Version>', "<Version>$Version.$Build</Version>"
    
    Set-Content $projectFile $content -Encoding UTF8
    Write-Host "✅ Project file updated successfully!" -ForegroundColor Green
} else {
    Write-Host "❌ Project file not found: $projectFile" -ForegroundColor Red
    exit 1
}

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
$buildResult = dotnet build Y0daiiIRC.csproj --configuration Release --verbosity quiet

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Build successful!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Version Information:" -ForegroundColor Cyan
    Write-Host "  Assembly Version: $Version" -ForegroundColor White
    Write-Host "  File Version: $Version" -ForegroundColor White
    Write-Host "  Full Version: $Version.$Build" -ForegroundColor White
    Write-Host "  Build Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor White
} else {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    Write-Host $buildResult -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🎉 Version update complete!" -ForegroundColor Green
Write-Host "The about dialog will now show the new version information." -ForegroundColor Cyan
