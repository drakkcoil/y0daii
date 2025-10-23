# Create a simple installer package for y0daii IRC Client
# This creates a self-extracting archive with installation script

Write-Host "Creating y0daii IRC Client Installer Package..." -ForegroundColor Green

# Create installer directory
$installerDir = "Y0daiiIRC-Installer"
if (Test-Path $installerDir) {
    Remove-Item $installerDir -Recurse -Force
}
New-Item -ItemType Directory -Path $installerDir | Out-Null

# Copy application files
Write-Host "Copying application files..." -ForegroundColor Yellow
Copy-Item "publish\*" -Destination $installerDir -Recurse

# Copy the custom icon
Write-Host "Copying application icon..." -ForegroundColor Yellow
Copy-Item "y0daii.ico" -Destination $installerDir

# Create installation script
$installScript = @"
@echo off
echo Installing y0daii IRC Client v1.0.7...
echo.

REM Check if running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This installer requires administrator privileges.
    echo Please run as administrator.
    pause
    exit /b 1
)

REM Create installation directory
set "INSTALL_DIR=%ProgramFiles%\y0daiiIRC"
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

REM Copy files
echo Copying files...
xcopy /E /I /Y . "%INSTALL_DIR%"

REM Create Start Menu shortcut
set "START_MENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs"
if not exist "%START_MENU%\y0daii IRC Client" mkdir "%START_MENU%\y0daii IRC Client"
echo [InternetShortcut] > "%START_MENU%\y0daii IRC Client\y0daii IRC Client.url"
echo URL=file:///%INSTALL_DIR%/Y0daiiIRC.exe >> "%START_MENU%\y0daii IRC Client\y0daii IRC Client.url"
echo IconFile=%INSTALL_DIR%/Y0daiiIRC.exe >> "%START_MENU%\y0daii IRC Client\y0daii IRC Client.url"
echo IconIndex=0 >> "%START_MENU%\y0daii IRC Client\y0daii IRC Client.url"

REM Create Desktop shortcut (optional)
set /p "CREATE_DESKTOP=Create Desktop shortcut? (y/n): "
if /i "%CREATE_DESKTOP%"=="y" (
    echo [InternetShortcut] > "%USERPROFILE%\Desktop\y0daii IRC Client.url"
    echo URL=file:///%INSTALL_DIR%/Y0daiiIRC.exe >> "%USERPROFILE%\Desktop\y0daii IRC Client.url"
    echo IconFile=%INSTALL_DIR%/Y0daiiIRC.exe >> "%USERPROFILE%\Desktop\y0daii IRC Client.url"
    echo IconIndex=0 >> "%USERPROFILE%\Desktop\y0daii IRC Client.url"
)

REM Add to Add/Remove Programs
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /v "DisplayName" /t REG_SZ /d "y0daii IRC Client" /f
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /v "DisplayVersion" /t REG_SZ /d "1.0.7" /f
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /v "Publisher" /t REG_SZ /d "Y0daii" /f
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /v "InstallLocation" /t REG_SZ /d "%INSTALL_DIR%" /f
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /v "UninstallString" /t REG_SZ /d "%INSTALL_DIR%\uninstall.bat" /f

echo.
echo ✅ y0daii IRC Client installed successfully!
echo Location: %INSTALL_DIR%
echo.
echo You can now:
echo 1. Find the application in Start Menu
echo 2. Run it from: %INSTALL_DIR%\Y0daiiIRC.exe
echo 3. Uninstall from Control Panel > Programs
echo.
pause
"@

$installScript | Out-File -FilePath "$installerDir\install.bat" -Encoding ASCII

# Create uninstall script
$uninstallScript = @"
@echo off
echo Uninstalling y0daii IRC Client...
echo.

REM Check if running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This uninstaller requires administrator privileges.
    echo Please run as administrator.
    pause
    exit /b 1
)

set "INSTALL_DIR=%ProgramFiles%\y0daiiIRC"

REM Remove files
if exist "%INSTALL_DIR%" (
    echo Removing application files...
    rmdir /S /Q "%INSTALL_DIR%"
)

REM Remove Start Menu shortcut
set "START_MENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs"
if exist "%START_MENU%\y0daii IRC Client" rmdir /S /Q "%START_MENU%\y0daii IRC Client"

REM Remove Desktop shortcut
if exist "%USERPROFILE%\Desktop\y0daii IRC Client.url" del "%USERPROFILE%\Desktop\y0daii IRC Client.url"

REM Remove from Add/Remove Programs
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /f

echo.
echo ✅ y0daii IRC Client uninstalled successfully!
echo.
pause
"@

$uninstallScript | Out-File -FilePath "$installerDir\uninstall.bat" -Encoding ASCII

# Create README
$readme = @"
# y0daii IRC Client v1.0.7

## Installation Instructions

1. **Run as Administrator**: Right-click on `install.bat` and select "Run as administrator"
2. **Follow the prompts**: The installer will guide you through the process
3. **Optional Desktop Shortcut**: Choose whether to create a desktop shortcut
4. **Complete**: The application will be installed to Program Files

## Features

- Modern Material Design UI
- Comprehensive IRC protocol support
- Built-in update system
- Professional architecture
- Self-contained (no additional dependencies required)

## Uninstallation

To uninstall:
1. Go to Control Panel > Programs and Features
2. Find "y0daii IRC Client" and click Uninstall
3. Or run `uninstall.bat` as administrator

## System Requirements

- Windows 10/11 (x64)
- No additional software required

## Support

For support and updates, visit: https://github.com/drakkcoil/y0daii
"@

$readme | Out-File -FilePath "$installerDir\README.txt" -Encoding UTF8

# Create zip package
Write-Host "Creating installer package..." -ForegroundColor Yellow
$zipPath = "Y0daiiIRC-Installer-v1.0.7.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$installerDir\*" -DestinationPath $zipPath -Force

# Cleanup
Remove-Item $installerDir -Recurse -Force

Write-Host "✅ Installer package created successfully!" -ForegroundColor Green
Write-Host "File: $zipPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "This package includes:" -ForegroundColor Yellow
Write-Host "- Complete application files" -ForegroundColor White
Write-Host "- Installation script (install.bat)" -ForegroundColor White
Write-Host "- Uninstallation script (uninstall.bat)" -ForegroundColor White
Write-Host "- README with instructions" -ForegroundColor White
Write-Host ""
Write-Host "To use:" -ForegroundColor Yellow
Write-Host "1. Extract the zip file" -ForegroundColor White
Write-Host "2. Run install.bat as administrator" -ForegroundColor White
Write-Host "3. Follow the installation prompts" -ForegroundColor White
Write-Host ""
Write-Host "You can now upload this to GitHub releases!" -ForegroundColor Green
