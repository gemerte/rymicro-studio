@echo off
setlocal
cd /d "%~dp0"

if not exist "RYMICRO Studio.exe" (
    echo Building RYMICRO Studio...
    powershell -NoProfile -ExecutionPolicy Bypass -File "build.ps1"
    if errorlevel 1 (
        echo Build failed.
        pause
        exit /b 1
    )
)

start "" "RYMICRO Studio.exe"
