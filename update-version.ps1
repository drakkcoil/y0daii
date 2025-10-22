# PowerShell script to update version with current date
param(
    [string]$ProjectFile = "Y0daiiIRC.csproj"
)

# Get current date
$now = Get-Date
$year = $now.Year
$month = $now.Month.ToString("00")
$day = $now.Day.ToString("00")
$buildNumber = $now.ToString("yyyyMMdd")

# Create version string: 1.0.2.YYYYMMDD
$version = "1.0.2.$buildNumber"
$assemblyVersion = "1.0.2.0"
$fileVersion = "1.0.2.0"

Write-Host "Updating version to: $version (Build: $buildNumber)" -ForegroundColor Green

# Read the project file
$content = Get-Content $ProjectFile -Raw

# Update version strings
$content = $content -replace '<AssemblyVersion>.*</AssemblyVersion>', "<AssemblyVersion>$assemblyVersion</AssemblyVersion>"
$content = $content -replace '<FileVersion>.*</FileVersion>', "<FileVersion>$fileVersion</FileVersion>"
$content = $content -replace '<Version>.*</Version>', "<Version>$version</Version>"

# Write back to file
Set-Content $ProjectFile -Value $content -NoNewline

Write-Host "Version updated successfully!" -ForegroundColor Green
Write-Host "Assembly Version: $assemblyVersion" -ForegroundColor Yellow
Write-Host "File Version: $fileVersion" -ForegroundColor Yellow
Write-Host "Version: $version" -ForegroundColor Yellow
Write-Host "Build Date: $($now.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Yellow
