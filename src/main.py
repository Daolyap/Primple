#!/usr/bin/env python3
"""
3D Terrain Map Generator
Main entry point for the application.
"""

import sys
import os

# Add the src directory to the path so terrain_generator package can be found
src_dir = os.path.dirname(os.path.abspath(__file__))
if src_dir not in sys.path:
    sys.path.insert(0, src_dir)

from terrain_generator.ui.main_window import main

if __name__ == "__main__":
    main()
