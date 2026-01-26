"""
Elevation Data Sources
Provides access to elevation data from various sources.
"""

import os
import tempfile
import numpy as np
import requests
from typing import Tuple, Optional
from pathlib import Path
from abc import ABC, abstractmethod

try:
    from scipy import ndimage
    HAS_SCIPY = True
except ImportError:
    HAS_SCIPY = False

try:
    import rasterio
    from rasterio.windows import from_bounds
    HAS_RASTERIO = True
except ImportError:
    HAS_RASTERIO = False


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
        if HAS_SCIPY:
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
        if not HAS_RASTERIO:
            raise ImportError("rasterio is required for GeoTIFF support. "
                            "Install with: pip install rasterio")
        self.filepath = Path(filepath)
        if not self.filepath.exists():
            raise FileNotFoundError(f"GeoTIFF file not found: {filepath}")
            
    def get_source_name(self) -> str:
        return f"GeoTIFF: {self.filepath.name}"
        
    def get_elevation_data(self, bounds: Tuple[float, float, float, float],
                          resolution: Optional[int] = None) -> Tuple[np.ndarray, dict]:
        """Load and crop elevation data from GeoTIFF."""
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
        if not tiles:
            return np.array([])
            
        if len(tiles) == 1:
            # Single tile - just crop to bounds
            lat, lon, data = tiles[0]
            return self._crop_to_bounds(data, lat, lon, bounds)
            
        # Multiple tiles - determine grid layout and stitch
        min_lat = min(t[0] for t in tiles)
        max_lat = max(t[0] for t in tiles)
        min_lon = min(t[1] for t in tiles)
        max_lon = max(t[1] for t in tiles)
        
        num_lat = max_lat - min_lat + 1
        num_lon = max_lon - min_lon + 1
        
        # Get tile size from first tile
        tile_size = tiles[0][2].shape[0]
        
        # Create output array (tiles overlap by 1 pixel, so subtract overlaps)
        out_height = num_lat * (tile_size - 1) + 1
        out_width = num_lon * (tile_size - 1) + 1
        result = np.full((out_height, out_width), np.nan, dtype=np.float32)
        
        # Place each tile in the grid
        for lat, lon, data in tiles:
            lat_idx = max_lat - lat  # Top-down indexing
            lon_idx = lon - min_lon
            
            row_start = lat_idx * (tile_size - 1)
            col_start = lon_idx * (tile_size - 1)
            
            result[row_start:row_start + tile_size, 
                   col_start:col_start + tile_size] = data
                   
        return result
        
    def _crop_to_bounds(self, data: np.ndarray, tile_lat: int, tile_lon: int,
                        bounds: Tuple[float, float, float, float]) -> np.ndarray:
        """Crop tile data to the specified bounds."""
        min_lon, min_lat, max_lon, max_lat = bounds
        tile_size = data.shape[0]
        
        # Calculate pixel coordinates for bounds within this tile
        # Each tile covers 1 degree, from tile_lat to tile_lat+1 and tile_lon to tile_lon+1
        row_start = int((tile_lat + 1 - max_lat) * (tile_size - 1))
        row_end = int((tile_lat + 1 - min_lat) * (tile_size - 1))
        col_start = int((min_lon - tile_lon) * (tile_size - 1))
        col_end = int((max_lon - tile_lon) * (tile_size - 1))
        
        # Clamp to valid range
        row_start = max(0, row_start)
        row_end = min(tile_size, row_end)
        col_start = max(0, col_start)
        col_end = min(tile_size, col_end)
        
        return data[row_start:row_end, col_start:col_end]


class OpenTopographySource(ElevationDataSource):
    """
    Fetch elevation data from OpenTopography API.
    Requires API key for access.
    """
    
    BASE_URL = "https://portal.opentopography.org/API/globaldem"
    
    # Valid DEM types supported by OpenTopography API
    VALID_DEM_TYPES = frozenset([
        "SRTMGL3", "SRTMGL1", "SRTMGL1_E", "AW3D30", "AW3D30_E",
        "NASADEM", "COP30", "COP90", "EU_DTM", "GEDI_L3"
    ])
    
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
        
        # Validate bounds
        if not (-180 <= min_lon <= 180 and -180 <= max_lon <= 180):
            raise ValueError(f"Longitude values must be between -180 and 180, got min={min_lon}, max={max_lon}")
        if not (-90 <= min_lat <= 90 and -90 <= max_lat <= 90):
            raise ValueError(f"Latitude values must be between -90 and 90, got min={min_lat}, max={max_lat}")
        if min_lon >= max_lon:
            raise ValueError(f"min_lon ({min_lon}) must be less than max_lon ({max_lon})")
        if min_lat >= max_lat:
            raise ValueError(f"min_lat ({min_lat}) must be less than max_lat ({max_lat})")
            
        # Validate DEM type
        if dem_type not in self.VALID_DEM_TYPES:
            raise ValueError(f"Invalid dem_type '{dem_type}'. Valid types are: {sorted(self.VALID_DEM_TYPES)}")
        
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
            tmp_path = None
            try:
                with tempfile.NamedTemporaryFile(suffix='.tif', delete=False) as tmp:
                    tmp.write(response.content)
                    tmp_path = tmp.name

                if not HAS_RASTERIO:
                    raise ImportError(
                        "rasterio is required to read OpenTopography GTiff data. Please install rasterio."
                    )
                with rasterio.open(tmp_path) as src:
                    data = src.read(1)
                    if src.nodata is not None:
                        data = np.where(data == src.nodata, np.nan, data)
            finally:
                if tmp_path and os.path.exists(tmp_path):
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
