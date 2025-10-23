@echo off
echo Building y0daii IRC Client Installer...
echo.

REM Check if WiX Toolset is installed
where candle >nul 2>&1
if %errorlevel% neq 0 (
    echo WiX Toolset not found. Installing...
    echo Please download and install WiX Toolset from: https://wixtoolset.org/releases/
    echo Or install via winget: winget install Microsoft.WiXToolset
    echo.
    echo After installing WiX Toolset, run this script again.
    pause
    exit /b 1
)

echo WiX Toolset found. Building installer...
echo.

REM Clean previous builds
if exist "Y0daiiIRC.Installer.wixobj" del "Y0daiiIRC.Installer.wixobj"
if exist "Y0daiiIRC.Installer.msi" del "Y0daiiIRC.Installer.msi"

REM Compile WiX source
echo Compiling WiX source...
candle Y0daiiIRC.Simple.Installer.wxs -out Y0daiiIRC.Simple.Installer.wixobj
if %errorlevel% neq 0 (
    echo Error compiling WiX source
    pause
    exit /b 1
)

REM Link the installer
echo Linking installer...
light Y0daiiIRC.Simple.Installer.wixobj -out Y0daiiIRC.Installer.msi
if %errorlevel% neq 0 (
    echo Error linking installer
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
pause
