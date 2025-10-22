@echo off
echo Updating version and building y0daii IRC Client...
echo.

REM Update version with current date
powershell -ExecutionPolicy Bypass -File "update-version.ps1"
if %ERRORLEVEL% neq 0 (
    echo Error updating version!
    pause
    exit /b 1
)

echo.
echo Building project...
dotnet build Y0daiiIRC.csproj
if %ERRORLEVEL% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Build completed successfully!
echo Version updated with current date.
pause
