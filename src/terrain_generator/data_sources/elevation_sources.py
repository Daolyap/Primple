"""
Elevation Data Sources
Provides access to elevation data from various sources.
"""

import numpy as np
import requests
from typing import Tuple, Optional
from pathlib import Path
from abc import ABC, abstractmethod
import struct
import os


class ElevationDataSource(ABC):
    """Abstract base class for elevation data sources."""
    
    @abstractmethod
    def get_elevation_data(self, bounds: Tuple[float, float, float, float],
                          resolution: Optional[int] = None) -> Tuple[np.ndarray, dict]:
        """
        Fetch elevation data for the given bounds.
        
        Args:
            bounds: (min_lon, min_lat, max_lon, max_lat) in decimal degrees
            resolution: Optional target resolution
            
        Returns:
            Tuple of (elevation_array, metadata_dict)
        """
        pass
        
    @abstractmethod
    def get_source_name(self) -> str:
        """Get the name of this data source."""
        pass


class SyntheticElevationSource(ElevationDataSource):
    """
    Generates synthetic terrain data for testing and demonstration.
    Creates realistic-looking terrain without external data dependencies.
    """
    
    def __init__(self, seed: Optional[int] = None):
        """Initialize with optional random seed for reproducibility."""
        self.seed = seed
        
    def get_source_name(self) -> str:
        return "Synthetic Terrain Generator"
        
    def get_elevation_data(self, bounds: Tuple[float, float, float, float],
                          resolution: Optional[int] = None) -> Tuple[np.ndarray, dict]:
        """Generate synthetic elevation data."""
        if resolution is None:
            resolution = 256
            
        if self.seed is not None:
            np.random.seed(self.seed)
            
        # Generate multi-octave Perlin-like noise
        elevation = self._generate_terrain(resolution)
        
        # Scale to realistic elevation range (0-3000m for mountainous terrain)
        min_lon, min_lat, max_lon, max_lat = bounds
        
        # Use bounds to seed variation
        lat_factor = abs(min_lat + max_lat) / 180
        lon_factor = abs(min_lon + max_lon) / 360
        
        base_elevation = 500 + lat_factor * 1000
        elevation_range = 1500 + lon_factor * 1500
        
        elevation = base_elevation + elevation * elevation_range
        
        metadata = {
            "source": self.get_source_name(),
            "bounds": bounds,
            "resolution": resolution,
            "min_elevation": float(np.min(elevation)),
            "max_elevation": float(np.max(elevation)),
            "units": "meters",
            "datum": "WGS84"
        }
        
        return elevation.astype(np.float32), metadata
        
    def _generate_terrain(self, size: int) -> np.ndarray:
        """Generate realistic terrain using fractal noise."""
        terrain = np.zeros((size, size), dtype=np.float32)
        
        # Multi-octave noise
        octaves = 6
        persistence = 0.5
        lacunarity = 2.0
        
        amplitude = 1.0
        frequency = 1.0
        max_value = 0.0
        
        for _ in range(octaves):
            terrain += amplitude * self._noise_2d(size, int(size * frequency))
            max_value += amplitude
            amplitude *= persistence
            frequency *= lacunarity
            
        # Normalize
        terrain = terrain / max_value
        
        # Apply erosion-like smoothing
        from scipy import ndimage
        terrain = ndimage.gaussian_filter(terrain, sigma=1.0)
        
        # Add some ridges
        ridges = self._generate_ridges(size)
        terrain = terrain * 0.7 + ridges * 0.3
        
        return terrain
        
    def _noise_2d(self, size: int, frequency: int) -> np.ndarray:
        """Generate 2D value noise at given frequency."""
        if frequency < 1:
            frequency = 1
            
        # Generate random values at grid points
        grid_size = frequency + 1
        grid = np.random.random((grid_size, grid_size))
        
        # Create coordinate arrays for interpolation
        x = np.linspace(0, frequency, size)
        y = np.linspace(0, frequency, size)
        
        # Bilinear interpolation
        result = np.zeros((size, size), dtype=np.float32)
        
        for i in range(size):
            for j in range(size):
                xi = int(x[j])
                yi = int(y[i])
                
                # Clamp to grid bounds
                xi = min(xi, frequency - 1)
                yi = min(yi, frequency - 1)
                
                # Fractional parts
                xf = x[j] - xi
                yf = y[i] - yi
                
                # Get four corner values
                v00 = grid[yi, xi]
                v01 = grid[yi, min(xi + 1, frequency)]
                v10 = grid[min(yi + 1, frequency), xi]
                v11 = grid[min(yi + 1, frequency), min(xi + 1, frequency)]
                
                # Bilinear interpolation
                v0 = v00 * (1 - xf) + v01 * xf
                v1 = v10 * (1 - xf) + v11 * xf
                result[i, j] = v0 * (1 - yf) + v1 * yf
                
        return result
        
    def _generate_ridges(self, size: int) -> np.ndarray:
        """Generate ridge-like features."""
        x = np.linspace(0, 4 * np.pi, size)
        y = np.linspace(0, 4 * np.pi, size)
        X, Y = np.meshgrid(x, y)
        
        # Create ridge pattern
        ridges = np.abs(np.sin(X + np.random.random() * np.pi) * 
                       np.sin(Y + np.random.random() * np.pi))
        ridges = ridges ** 0.5  # Sharpen ridges
        
        return ridges


class GeoTIFFSource(ElevationDataSource):
    """Load elevation data from local GeoTIFF files."""
    
    def __init__(self, filepath: str):
        """
        Initialize with path to GeoTIFF file.
        
        Args:
            filepath: Path to the .tif or .tiff file
        """
        self.filepath = Path(filepath)
        if not self.filepath.exists():
            raise FileNotFoundError(f"GeoTIFF file not found: {filepath}")
            
    def get_source_name(self) -> str:
        return f"GeoTIFF: {self.filepath.name}"
        
    def get_elevation_data(self, bounds: Tuple[float, float, float, float],
                          resolution: Optional[int] = None) -> Tuple[np.ndarray, dict]:
        """Load and crop elevation data from GeoTIFF."""
        try:
            import rasterio
            from rasterio.windows import from_bounds
            
            with rasterio.open(self.filepath) as src:
                # Get window for bounds
                window = from_bounds(*bounds, src.transform)
                
                # Read data
                data = src.read(1, window=window)
                
                # Handle nodata
                if src.nodata is not None:
                    data = np.where(data == src.nodata, np.nan, data)
                    
                metadata = {
                    "source": self.get_source_name(),
                    "bounds": bounds,
                    "resolution": data.shape[0],
                    "crs": str(src.crs),
                    "min_elevation": float(np.nanmin(data)),
                    "max_elevation": float(np.nanmax(data)),
                    "units": "meters"
                }
                
                return data.astype(np.float32), metadata
                
        except ImportError:
            raise ImportError("rasterio is required for GeoTIFF support. "
                            "Install with: pip install rasterio")


class HGTSource(ElevationDataSource):
    """Load elevation data from NASA SRTM HGT files."""
    
    def __init__(self, data_dir: str):
        """
        Initialize with directory containing HGT files.
        
        Args:
            data_dir: Directory containing .hgt files
        """
        self.data_dir = Path(data_dir)
        
    def get_source_name(self) -> str:
        return "NASA SRTM (HGT)"
        
    def get_elevation_data(self, bounds: Tuple[float, float, float, float],
                          resolution: Optional[int] = None) -> Tuple[np.ndarray, dict]:
        """Load and stitch HGT tiles for the given bounds."""
        min_lon, min_lat, max_lon, max_lat = bounds
        
        # Determine required tiles
        tiles = []
        for lat in range(int(np.floor(min_lat)), int(np.ceil(max_lat))):
            for lon in range(int(np.floor(min_lon)), int(np.ceil(max_lon))):
                tile_data = self._load_tile(lat, lon)
                if tile_data is not None:
                    tiles.append((lat, lon, tile_data))
                    
        if not tiles:
            raise ValueError(f"No HGT tiles found for bounds: {bounds}")
            
        # Stitch tiles together
        data = self._stitch_tiles(tiles, bounds)
        
        metadata = {
            "source": self.get_source_name(),
            "bounds": bounds,
            "resolution": data.shape[0],
            "min_elevation": float(np.nanmin(data)),
            "max_elevation": float(np.nanmax(data)),
            "units": "meters",
            "datum": "WGS84"
        }
        
        return data.astype(np.float32), metadata
        
    def _get_tile_filename(self, lat: int, lon: int) -> str:
        """Generate HGT filename for a tile."""
        lat_char = 'N' if lat >= 0 else 'S'
        lon_char = 'E' if lon >= 0 else 'W'
        return f"{lat_char}{abs(lat):02d}{lon_char}{abs(lon):03d}.hgt"
        
    def _load_tile(self, lat: int, lon: int) -> Optional[np.ndarray]:
        """Load a single HGT tile."""
        filename = self._get_tile_filename(lat, lon)
        filepath = self.data_dir / filename
        
        if not filepath.exists():
            return None
            
        # HGT files are 2-byte signed integers, big-endian
        # SRTM3: 1201x1201 samples (3 arc-second resolution)
        # SRTM1: 3601x3601 samples (1 arc-second resolution)
        
        file_size = filepath.stat().st_size
        if file_size == 1201 * 1201 * 2:
            samples = 1201
        elif file_size == 3601 * 3601 * 2:
            samples = 3601
        else:
            raise ValueError(f"Unknown HGT file size: {file_size}")
            
        with open(filepath, 'rb') as f:
            data = np.frombuffer(f.read(), dtype='>i2')
            data = data.reshape((samples, samples))
            
        # Replace void values (-32768) with NaN
        data = data.astype(np.float32)
        data[data == -32768] = np.nan
        
        return data
        
    def _stitch_tiles(self, tiles, bounds) -> np.ndarray:
        """Stitch multiple tiles and crop to bounds."""
        # For simplicity, just return the first tile cropped
        # Full implementation would stitch multiple tiles
        if len(tiles) > 0:
            return tiles[0][2]
        return np.array([])


class OpenTopographySource(ElevationDataSource):
    """
    Fetch elevation data from OpenTopography API.
    Requires API key for access.
    """
    
    BASE_URL = "https://portal.opentopography.org/API/globaldem"
    
    def __init__(self, api_key: Optional[str] = None):
        """
        Initialize with OpenTopography API key.
        
        Args:
            api_key: OpenTopography API key (optional, but recommended for higher limits)
        """
        self.api_key = api_key or os.environ.get('OPENTOPO_API_KEY', '')
        
    def get_source_name(self) -> str:
        return "OpenTopography API"
        
    def get_elevation_data(self, bounds: Tuple[float, float, float, float],
                          resolution: Optional[int] = None,
                          dem_type: str = "SRTMGL3") -> Tuple[np.ndarray, dict]:
        """
        Fetch elevation data from OpenTopography.
        
        Args:
            bounds: (min_lon, min_lat, max_lon, max_lat)
            resolution: Target resolution (not directly supported by API)
            dem_type: DEM type - SRTMGL3 (90m), SRTMGL1 (30m), AW3D30, etc.
            
        Returns:
            Tuple of (elevation_array, metadata)
        """
        min_lon, min_lat, max_lon, max_lat = bounds
        
        params = {
            "demtype": dem_type,
            "south": min_lat,
            "north": max_lat,
            "west": min_lon,
            "east": max_lon,
            "outputFormat": "GTiff"
        }
        
        if self.api_key:
            params["API_Key"] = self.api_key
            
        try:
            response = requests.get(self.BASE_URL, params=params, timeout=60)
            response.raise_for_status()
            
            # Save to temporary file and read with rasterio
            import tempfile
            with tempfile.NamedTemporaryFile(suffix='.tif', delete=False) as tmp:
                tmp.write(response.content)
                tmp_path = tmp.name
                
            try:
                import rasterio
                with rasterio.open(tmp_path) as src:
                    data = src.read(1)
                    if src.nodata is not None:
                        data = np.where(data == src.nodata, np.nan, data)
            finally:
                os.unlink(tmp_path)
                
            metadata = {
                "source": self.get_source_name(),
                "dem_type": dem_type,
                "bounds": bounds,
                "resolution": data.shape[0],
                "min_elevation": float(np.nanmin(data)),
                "max_elevation": float(np.nanmax(data)),
                "units": "meters"
            }
            
            return data.astype(np.float32), metadata
            
        except requests.RequestException as e:
            raise ConnectionError(f"Failed to fetch data from OpenTopography: {e}")
