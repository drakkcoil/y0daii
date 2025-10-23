# Install WiX Toolset for building Windows installers
Write-Host "Installing WiX Toolset..." -ForegroundColor Green

# Check if winget is available
if (Get-Command winget -ErrorAction SilentlyContinue) {
    Write-Host "Installing WiX Toolset via winget..." -ForegroundColor Yellow
    winget install Microsoft.WiXToolset
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ WiX Toolset installed successfully!" -ForegroundColor Green
        Write-Host "You can now run build-installer.bat to create the installer." -ForegroundColor Cyan
    } else {
        Write-Host "❌ Failed to install WiX Toolset via winget" -ForegroundColor Red
        Write-Host "Please install manually from: https://wixtoolset.org/releases/" -ForegroundColor Yellow
    }
} else {
    Write-Host "winget not available. Please install WiX Toolset manually:" -ForegroundColor Yellow
    Write-Host "1. Go to: https://wixtoolset.org/releases/" -ForegroundColor Cyan
    Write-Host "2. Download and install WiX Toolset v3.11 or later" -ForegroundColor Cyan
    Write-Host "3. Run build-installer.bat after installation" -ForegroundColor Cyan
}

Write-Host "`nPress any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
