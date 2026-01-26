"""
Terrain Processor Module
Handles elevation data processing, smoothing, and mesh generation.
"""

import numpy as np
from scipy import ndimage
from scipy.interpolate import RectBivariateSpline
from typing import Tuple, Optional
from dataclasses import dataclass


@dataclass
class TerrainSettings:
    """Settings for terrain processing and mesh generation."""
    # Vertical exaggeration
    vertical_exaggeration: float = 2.0
    
    # Elevation range
    min_elevation: Optional[float] = None  # None = auto from data
    max_elevation: Optional[float] = None  # None = auto from data
    normalize_to_height: Optional[float] = None  # Target height in mm
    
    # Smoothing
    smoothing_radius: float = 1.0
    use_adaptive_smoothing: bool = False
    
    # Resolution
    mesh_resolution: int = 256  # Grid resolution (e.g., 256x256)
    
    # Base settings
    base_type: str = "flat"  # flat, tapered, contoured, minimal, floating
    base_thickness: float = 3.0  # mm
    
    # Edge treatment
    edge_type: str = "vertical"  # vertical, beveled, rounded, natural
    bevel_angle: float = 45.0  # degrees
    
    # Map dimensions
    map_width: float = 150.0  # mm
    map_height: float = 150.0  # mm
    max_print_height: float = 30.0  # mm


class TerrainProcessor:
    """Processes elevation data and generates 3D terrain meshes."""
    
    def __init__(self, settings: Optional[TerrainSettings] = None):
        """Initialize terrain processor with settings."""
        self.settings = settings or TerrainSettings()
        self.elevation_data: Optional[np.ndarray] = None
        self.processed_data: Optional[np.ndarray] = None
        self.bounds: Optional[Tuple[float, float, float, float]] = None
        
    def load_elevation_data(self, data: np.ndarray, 
                           bounds: Tuple[float, float, float, float]) -> None:
        """
        Load elevation data from numpy array.
        
        Args:
            data: 2D numpy array of elevation values
            bounds: (min_lon, min_lat, max_lon, max_lat) bounding box
        """
        self.elevation_data = data.astype(np.float32)
        self.bounds = bounds
        self._process_elevation()
        
    def _process_elevation(self) -> None:
        """Process elevation data with current settings."""
        if self.elevation_data is None:
            return
            
        data = self.elevation_data.copy()
        
        # Handle nodata values
        nodata_mask = ~np.isfinite(data) | (data < -1000)
        
        # Check if all values are nodata
        if np.all(nodata_mask):
            raise ValueError(
                "All elevation data values are invalid (NaN or nodata). "
                "Cannot process terrain with no valid data points."
            )
        
        if np.any(nodata_mask):
            # Fill nodata with nearest neighbor interpolation
            indices = ndimage.distance_transform_edt(
                nodata_mask, return_distances=False, return_indices=True
            )
            data = data[tuple(indices)]
        
        # Apply elevation range limits
        min_elev = self.settings.min_elevation
        max_elev = self.settings.max_elevation
        
        if min_elev is None:
            min_elev = np.nanmin(data)
        if max_elev is None:
            max_elev = np.nanmax(data)
            
        # Clip to range
        data = np.clip(data, min_elev, max_elev)
        
        # Normalize to 0-1 range
        elev_range = max_elev - min_elev
        if elev_range > 0:
            data = (data - min_elev) / elev_range
        else:
            data = np.zeros_like(data)
            
        # Apply vertical exaggeration (affects the final scaling)
        # This is stored in the normalized data for now
        
        # Apply smoothing
        if self.settings.smoothing_radius > 0:
            if self.settings.use_adaptive_smoothing:
                data = self._adaptive_smooth(data)
            else:
                sigma = self.settings.smoothing_radius
                data = ndimage.gaussian_filter(data, sigma=sigma)
                
        self.processed_data = data
        
    def _adaptive_smooth(self, data: np.ndarray) -> np.ndarray:
        """
        Apply adaptive smoothing - less on ridges/features, more on flat areas.
        """
        # Calculate local slope magnitude
        grad_y, grad_x = np.gradient(data)
        slope = np.sqrt(grad_x**2 + grad_y**2)
        
        # Normalize slope to 0-1
        max_slope = np.percentile(slope, 99)
        if max_slope > 0:
            slope_norm = np.clip(slope / max_slope, 0, 1)
        else:
            slope_norm = np.zeros_like(slope)
            
        # Create adaptive sigma: more smoothing (higher sigma) in flat areas
        base_sigma = self.settings.smoothing_radius
        min_sigma = base_sigma * 0.2
        max_sigma = base_sigma * 2.0
        
        # Use multiple passes with varying sigma
        result = data.copy()
        for i in range(3):
            local_sigma = min_sigma + (max_sigma - min_sigma) * (1 - slope_norm)
            # Use uniform filter as approximation for varying sigma
            avg_sigma = np.mean(local_sigma)
            smoothed = ndimage.gaussian_filter(result, sigma=avg_sigma)
            # Blend based on slope
            alpha = 1 - slope_norm
            result = result * (1 - alpha) + smoothed * alpha
            
        return result
        
    def resample_to_resolution(self, resolution: Optional[int] = None) -> np.ndarray:
        """
        Resample processed data to target mesh resolution.
        
        Args:
            resolution: Target grid resolution (NxN). Uses settings if None.
            
        Returns:
            Resampled elevation data as 2D numpy array
        """
        if self.processed_data is None:
            raise ValueError("No elevation data loaded")
            
        target_res = resolution or self.settings.mesh_resolution
        
        # Use spline interpolation for smooth resampling
        h, w = self.processed_data.shape
        x_old = np.linspace(0, 1, w)
        y_old = np.linspace(0, 1, h)
        
        spline = RectBivariateSpline(y_old, x_old, self.processed_data, kx=3, ky=3)
        
        x_new = np.linspace(0, 1, target_res)
        y_new = np.linspace(0, 1, target_res)
        
        return spline(y_new, x_new)
        
    def generate_heightfield(self) -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
        """
        Generate X, Y, Z coordinate grids for the terrain mesh.
        
        Returns:
            Tuple of (X, Y, Z) coordinate grids
        """
        if self.processed_data is None:
            raise ValueError("No elevation data loaded")
            
        # Resample to mesh resolution
        data = self.resample_to_resolution()
        resolution = data.shape[0]
        
        # Create coordinate grids
        x = np.linspace(0, self.settings.map_width, resolution)
        y = np.linspace(0, self.settings.map_height, resolution)
        X, Y = np.meshgrid(x, y)
        
        # Scale Z values
        max_z = self.settings.max_print_height * self.settings.vertical_exaggeration
        Z = data * max_z
        
        # Add base
        Z = self._apply_base(X, Y, Z)
        
        return X, Y, Z
        
    def _apply_base(self, X: np.ndarray, Y: np.ndarray, 
                    Z: np.ndarray) -> np.ndarray:
        """Apply base to the terrain based on settings."""
        base_type = self.settings.base_type
        base_thickness = self.settings.base_thickness
        
        if base_type == "floating":
            # No base, just terrain
            return Z
            
        elif base_type == "flat":
            # Flat base at minimum elevation
            Z = Z + base_thickness
            
        elif base_type == "contoured":
            # Base follows terrain with elevation-dependent thickness
            min_z = np.nanmin(Z)
            max_z = np.nanmax(Z)
            # Avoid division by zero if terrain is perfectly flat
            denom = max_z - min_z if max_z != min_z else 1.0
            norm = (Z - min_z) / denom
            # Thicker base in lower areas, thinner in higher areas
            thickness_map = base_thickness * (1.0 - 0.5 * norm)
            Z = Z + thickness_map
            
        elif base_type == "tapered":
            # Thicker at edges, thinner in center
            cx = self.settings.map_width / 2
            cy = self.settings.map_height / 2
            dist = np.sqrt((X - cx)**2 + (Y - cy)**2)
            max_dist = np.sqrt(cx**2 + cy**2)
            taper = 0.5 + 0.5 * (dist / max_dist)  # 0.5 to 1.0
            Z = Z + base_thickness * taper
            
        elif base_type == "minimal":
            # Just enough for structure (thinner base)
            Z = Z + base_thickness * 0.5
            
        return Z
        
    def get_statistics(self) -> dict:
        """Get statistics about the loaded terrain data."""
        if self.elevation_data is None:
            return {}
            
        return {
            "raw_min_elevation": float(np.nanmin(self.elevation_data)),
            "raw_max_elevation": float(np.nanmax(self.elevation_data)),
            "raw_mean_elevation": float(np.nanmean(self.elevation_data)),
            "elevation_range": float(np.nanmax(self.elevation_data) - np.nanmin(self.elevation_data)),
            "data_shape": self.elevation_data.shape,
            "bounds": self.bounds,
        }
