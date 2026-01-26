@echo off
REM 3D Terrain Map Generator - Windows Launcher
REM Optimized for Bambu Labs Printers

echo ============================================
echo   3D Terrain Map Generator
echo   Optimized for Bambu Labs Printers
echo ============================================
echo.

REM Check if Python is installed
python --version >nul 2>&1
if errorlevel 1 (
    echo Python is not installed or not in PATH.
    echo Please install Python 3.10 or later from https://python.org
    pause
    exit /b 1
)

REM Check if virtual environment exists
if not exist "venv" (
    echo Creating virtual environment...
    python -m venv venv
)

REM Activate virtual environment
call venv\Scripts\activate.bat

REM Install dependencies if needed
if not exist "venv\installed.marker" (
    echo Installing dependencies...
    pip install -r requirements.txt
    if errorlevel 1 (
        echo Failed to install dependencies. Please check the error messages above.
        pause
        exit /b 1
    )
    echo. > venv\installed.marker
)

REM Run the application
echo Starting 3D Terrain Map Generator...
python src\main.py

pause
