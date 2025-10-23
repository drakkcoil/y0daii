@echo off
echo Building y0daii IRC Client Installer with WiX v6.0...
echo.

REM Set WiX path
set WIX_PATH="C:\Program Files\WiX Toolset v6.0\bin"

REM Check if WiX is available
if not exist %WIX_PATH%\wix.exe (
    echo WiX Toolset not found at %WIX_PATH%
    echo Please check your WiX installation.
    pause
    exit /b 1
)

echo WiX Toolset found. Building installer...
echo.

REM Clean previous builds
if exist "Y0daiiIRC.Installer.msi" del "Y0daiiIRC.Installer.msi"

REM Build installer using WiX v6.0
echo Building MSI installer...
%WIX_PATH%\wix.exe build Y0daiiIRC.Simple.WiX6.wxs -o Y0daiiIRC.Installer.msi
if %errorlevel% neq 0 (
    echo Error building installer
    pause
    exit /b 1
)

echo.
echo ✅ Installer built successfully: Y0daiiIRC.Installer.msi
echo.
echo You can now:
echo 1. Test the installer by double-clicking Y0daiiIRC.Installer.msi
echo 2. Upload it to GitHub releases
echo 3. Distribute to users
echo.
echo Installer features:
echo - Professional Windows Installer (MSI)
echo - Start Menu shortcuts
echo - Desktop shortcut (optional)
echo - Proper uninstall support
echo - Registry entries for proper Windows integration
echo.
pause
