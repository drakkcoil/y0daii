# PowerShell script to create a professional installer package for y0daii IRC Client
# This creates a self-contained installer with proper Windows integration

# Define paths
$appName = "y0daii IRC Client"
$appVersion = "1.0.7"
$installDirName = "y0daiiIRC"
$installPath = Join-Path $env:ProgramFiles $installDirName
$shortcutName = "$appName.lnk"
$desktopShortcutPath = Join-Path $env:Public\Desktop $shortcutName
$startMenuShortcutPath = Join-Path $env:AppData\Microsoft\Windows\Start Menu\Programs $shortcutName
$installerDir = "Y0daiiIRC-Installer-Temp"
$outputZip = "Y0daiiIRC-Installer-v$appVersion.zip"

# Clean up previous temp directory and output zip
if (Test-Path $installerDir) {
    Remove-Item $installerDir -Recurse -Force
}
if (Test-Path $outputZip) {
    Remove-Item $outputZip -Force
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
echo Installing y0daii IRC Client v$appVersion...

REM Check for administrator privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process -FilePath '%~dp0install.bat' -Verb RunAs"
    exit /b 1
)

echo Copying files to %installPath%...
xcopy "%~dp0publish\*" "%installPath%\" /s /e /y /i /q >nul
if %errorlevel% neq 0 (
    echo Error: Failed to copy application files.
    pause
    exit /b 1
)

echo Creating Start Menu shortcut...
powershell -Command "$ws = New-Object -ComObject WScript.Shell; $shortcut = $ws.CreateShortcut('%startMenuShortcutPath%'); $shortcut.TargetPath = '%installPath%\Y0daiiIRC.exe'; $shortcut.IconLocation = '%installPath%\y0daii.ico'; $shortcut.Save()"
if %errorlevel% neq 0 (
    echo Warning: Failed to create Start Menu shortcut.
)

set /p createDesktopShortcut="Create Desktop shortcut? (Y/N): "
if /i "%createDesktopShortcut%"=="Y" (
    echo Creating Desktop shortcut...
    powershell -Command "$ws = New-Object -ComObject WScript.Shell; $shortcut = $ws.CreateShortcut('%desktopShortcutPath%'); $shortcut.TargetPath = '%installPath%\Y0daiiIRC.exe'; $shortcut.IconLocation = '%installPath%\y0daii.ico'; $shortcut.Save()"
    if %errorlevel% neq 0 (
        echo Warning: Failed to create Desktop shortcut.
    )
)

echo Adding to Add/Remove Programs...
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /v "DisplayName" /d "y0daii IRC Client v%appVersion%" /f >nul
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /v "DisplayVersion" /d "%appVersion%" /f >nul
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /v "Publisher" /d "Y0daii" /f >nul
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /v "InstallLocation" /d "%installPath%" /f >nul
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /v "UninstallString" /d "cmd.exe /c ""%installPath%\uninstall.bat""" /f >nul
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /v "NoModify" /d 1 /t REG_DWORD /f >nul
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /v "NoRepair" /d 1 /t REG_DWORD /f >nul
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /v "DisplayIcon" /d "%installPath%\y0daii.ico" /f >nul

echo Installation complete!
pause
exit /b 0
"@
$installScript | Set-Content (Join-Path $installerDir "install.bat")

# Create uninstallation script
$uninstallScript = @"
@echo off
echo Uninstalling y0daii IRC Client...

REM Check for administrator privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process -FilePath '%~dp0uninstall.bat' -Verb RunAs"
    exit /b 1
)

echo Removing application files from %installPath%...
if exist "%installPath%" (
    rmdir /s /q "%installPath%" >nul
    if %errorlevel% neq 0 (
        echo Error: Failed to remove application directory.
    )
)

echo Removing Start Menu shortcut...
if exist "%startMenuShortcutPath%" (
    del "%startMenuShortcutPath%" >nul
)

echo Removing Desktop shortcut...
if exist "%desktopShortcutPath%" (
    del "%desktopShortcutPath%" >nul
)

echo Removing from Add/Remove Programs...
reg delete "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\y0daiiIRC" /f >nul
if %errorlevel% neq 0 (
    echo Warning: Failed to remove registry entries.
)

echo Uninstallation complete!
pause
exit /b 0
"@
$uninstallScript | Set-Content (Join-Path $installerDir "uninstall.bat")

# Create README for the installer package
$readmeContent = @"
# y0daii IRC Client v$appVersion Installer

This package contains the y0daii IRC Client application and scripts for a professional installation experience on Windows.

## Installation Instructions

1.  **Extract** this zip file to a temporary location (e.g., your Downloads folder).
2.  **Run `install.bat` as administrator**: Right-click on `install.bat` and select "Run as administrator".
3.  **Follow the prompts**: The installer will guide you through the process, including an option to create a desktop shortcut.
4.  The application will be installed to `C:\Program Files\y0daiiIRC`.

## Uninstallation Instructions

1.  **Via Control Panel**: Go to "Add or remove programs", find "y0daii IRC Client v$appVersion", and click "Uninstall".
2.  **Via Script**: Navigate to the installation directory (`C:\Program Files\y0daiiIRC`) and run `uninstall.bat` as administrator.

## System Requirements

-   Windows 10/11 (x64)
-   No .NET runtime installation required (self-contained application)
-   Approximately 100MB of disk space
"@
$readmeContent | Set-Content (Join-Path $installerDir "README.md")

# Create the final zip package
Write-Host "Creating installer package..." -ForegroundColor Yellow
Compress-Archive -Path "$installerDir\*" -DestinationPath $outputZip -Force

# Clean up temp directory
Remove-Item $installerDir -Recurse -Force

Write-Host "`n✅ Installer package created successfully!" -ForegroundColor Green
Write-Host "File: $outputZip`n" -ForegroundColor Green
Write-Host "This package includes:" -ForegroundColor Green
Write-Host "- Complete application files" -ForegroundColor Green
Write-Host "- Installation script (install.bat)" -ForegroundColor Green
Write-Host "- Uninstallation script (uninstall.bat)" -ForegroundColor Green
Write-Host "- README with instructions`n" -ForegroundColor Green
Write-Host "To use:" -ForegroundColor Green
Write-Host "1. Extract the zip file" -ForegroundColor Green
Write-Host "2. Run install.bat as administrator" -ForegroundColor Green
Write-Host "3. Follow the installation prompts`n" -ForegroundColor Green
Write-Host "You can now upload this to GitHub releases!" -ForegroundColor Green
