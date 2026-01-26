#!/bin/bash
# 3D Terrain Map Generator - Linux/macOS Launcher
# Optimized for Bambu Labs Printers

echo "============================================"
echo "  3D Terrain Map Generator"
echo "  Optimized for Bambu Labs Printers"
echo "============================================"
echo ""

# Check if Python is installed
if ! command -v python3 &> /dev/null; then
    echo "Python 3 is not installed. Please install Python 3.10 or later."
    exit 1
fi

# Create virtual environment if it doesn't exist
if [ ! -d "venv" ]; then
    echo "Creating virtual environment..."
    python3 -m venv venv
fi

# Activate virtual environment
source venv/bin/activate

# Install dependencies if needed
if [ ! -f "venv/installed.marker" ]; then
    echo "Installing dependencies..."
    pip install -r requirements.txt
    touch venv/installed.marker
fi

# Run the application
echo "Starting 3D Terrain Map Generator..."
python3 src/main.py
