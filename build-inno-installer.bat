@echo off
echo Building y0daii IRC Client with Inno Setup...
echo.

REM Check if Inno Setup is installed
where iscc >nul 2>&1
if %errorlevel% neq 0 (
    echo Inno Setup not found.
    echo.
    echo Please install Inno Setup:
    echo 1. Download from: https://jrsoftware.org/isinfo.php
    echo 2. Install Inno Setup
    echo 3. Run this script again
    echo.
    echo Inno Setup is free and much easier than WiX!
    pause
    exit /b 1
)

echo Inno Setup found. Building installer...
echo.

REM Clean previous builds
if exist "Output\Y0daiiIRC-Setup-v1.0.7.exe" del "Output\Y0daiiIRC-Setup-v1.0.7.exe"

REM Compile installer
echo Compiling installer with Inno Setup...
iscc Y0daiiIRC.InnoSetup.iss
if %errorlevel% neq 0 (
    echo Error building installer
    pause
    exit /b 1
)

echo.
echo ✅ Installer built successfully!
echo Location: Output\Y0daiiIRC-Setup-v1.0.7.exe
echo.
echo You can now:
echo 1. Test the installer by running it
echo 2. Upload it to GitHub releases
echo 3. Distribute to users
echo.
pause
