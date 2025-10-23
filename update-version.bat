@echo off
REM Simple Version Update for y0daii IRC Client
REM Usage: update-version.bat [version]
REM Example: update-version.bat 1.0.6

if "%1"=="" (
    echo Auto-incrementing version...
    powershell -ExecutionPolicy Bypass -File update-version-simple.ps1
) else (
    echo Updating to version %1...
    powershell -ExecutionPolicy Bypass -File update-version-simple.ps1 -Version %1
)

pause
