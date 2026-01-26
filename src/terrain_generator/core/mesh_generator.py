"""
Mesh Generator Module
Generates 3D mesh from terrain heightfield data.
"""

import numpy as np
from typing import Tuple, List, Optional
from dataclasses import dataclass


@dataclass
class MeshData:
    """Container for 3D mesh data."""
    vertices: np.ndarray  # Nx3 array of vertex positions
    faces: np.ndarray     # Mx3 array of face indices
    normals: Optional[np.ndarray] = None  # Nx3 array of vertex normals
    
    @property
    def vertex_count(self) -> int:
        return len(self.vertices)
        
    @property
    def face_count(self) -> int:
        return len(self.faces)
        
    def validate(self) -> Tuple[bool, List[str]]:
        """
        Validate mesh for 3D printing compatibility.
        
        Returns:
            Tuple of (is_valid, list of issues)
        """
        issues = []
        
        # Check for degenerate triangles (vectorized for performance)
        if self.faces.size > 0:
            # Gather triangle vertices for all faces
            v0 = self.vertices[self.faces[:, 0]]
            v1 = self.vertices[self.faces[:, 1]]
            v2 = self.vertices[self.faces[:, 2]]

            # Compute edge vectors and triangle areas
            edge1 = v1 - v0
            edge2 = v2 - v0
            cross_products = np.cross(edge1, edge2)
            areas = np.linalg.norm(cross_products, axis=1) / 2.0

            # Identify degenerate triangles
            degenerate_indices = np.nonzero(areas < 1e-10)[0]
            for i in degenerate_indices:
                issues.append(f"Degenerate triangle at face {int(i)}")
                
        # Check for valid normals
        if self.normals is not None:
            invalid_normals = np.sum(np.all(self.normals == 0, axis=1))
            if invalid_normals > 0:
                issues.append(f"{invalid_normals} vertices with zero normals")
                
        # Check for non-manifold edges (simplified check)
        # A proper check would count edge usage
        
        return len(issues) == 0, issues


class MeshGenerator:
    """Generates watertight 3D meshes from heightfield data."""
    
    def __init__(self):
        """Initialize mesh generator."""
        pass
        
    def generate_terrain_mesh(self, X: np.ndarray, Y: np.ndarray, 
                              Z: np.ndarray, base_z: float = 0.0) -> MeshData:
        """
        Generate a watertight terrain mesh from heightfield grids.
        
        Creates a solid mesh with:
        - Top surface from heightfield
        - Flat bottom at base_z
        - Side walls connecting top and bottom
        
        Args:
            X: 2D grid of X coordinates
            Y: 2D grid of Y coordinates  
            Z: 2D grid of Z coordinates (heights)
            base_z: Z coordinate for the flat bottom
            
        Returns:
            MeshData containing the complete mesh
        """
        # Generate top surface vertices and faces
        top_vertices, top_faces = self._generate_surface(X, Y, Z)
        
        # Generate bottom surface (flat at base_z)
        bottom_vertices, bottom_faces = self._generate_surface(
            X, Y, np.full_like(Z, base_z)
        )
        # Flip bottom faces to point downward
        bottom_faces = bottom_faces[:, ::-1]
        
        # Offset bottom face indices
        bottom_faces = bottom_faces + len(top_vertices)
        
        # Generate side walls
        side_vertices, side_faces = self._generate_walls(
            X, Y, Z, base_z, len(top_vertices) + len(bottom_vertices)
        )
        
        # Combine all vertices and faces
        all_vertices = np.vstack([top_vertices, bottom_vertices, side_vertices])
        all_faces = np.vstack([top_faces, bottom_faces, side_faces])
        
        # Calculate normals
        normals = self._calculate_vertex_normals(all_vertices, all_faces)
        
        return MeshData(
            vertices=all_vertices,
            faces=all_faces,
            normals=normals
        )
        
    def _generate_surface(self, X: np.ndarray, Y: np.ndarray, 
                          Z: np.ndarray) -> Tuple[np.ndarray, np.ndarray]:
        """Generate vertices and faces for a height surface."""
        rows, cols = Z.shape
        
        # Create vertices
        vertices = np.zeros((rows * cols, 3), dtype=np.float32)
        vertices[:, 0] = X.flatten()
        vertices[:, 1] = Y.flatten()
        vertices[:, 2] = Z.flatten()
        
        # Create faces (two triangles per grid cell) - pre-allocated for performance
        num_cells = (rows - 1) * (cols - 1)
        faces = np.zeros((num_cells * 2, 3), dtype=np.int32)
        
        # Vectorized face generation
        i_indices, j_indices = np.meshgrid(
            np.arange(rows - 1), np.arange(cols - 1), indexing='ij'
        )
        i_flat = i_indices.flatten()
        j_flat = j_indices.flatten()
        
        v00 = i_flat * cols + j_flat
        v01 = i_flat * cols + (j_flat + 1)
        v10 = (i_flat + 1) * cols + j_flat
        v11 = (i_flat + 1) * cols + (j_flat + 1)
        
        # Lower-left triangles
        faces[0::2, 0] = v00
        faces[0::2, 1] = v10
        faces[0::2, 2] = v01
        
        # Upper-right triangles
        faces[1::2, 0] = v01
        faces[1::2, 1] = v10
        faces[1::2, 2] = v11
                
        return vertices, faces
        
    def _generate_walls(self, X: np.ndarray, Y: np.ndarray, Z: np.ndarray,
                        base_z: float, vertex_offset: int) -> Tuple[np.ndarray, np.ndarray]:
        """Generate side wall vertices and faces using pre-allocated arrays."""
        rows, cols = Z.shape
        
        # Pre-calculate total vertices and faces needed
        # Each wall segment: 4 vertices, 2 faces
        # North + South walls: (cols-1) * 2 segments
        # East + West walls: (rows-1) * 2 segments
        num_segments = 2 * (cols - 1) + 2 * (rows - 1)
        num_vertices = num_segments * 4
        num_faces = num_segments * 2
        
        if num_segments == 0:
            return np.empty((0, 3), dtype=np.float32), np.empty((0, 3), dtype=np.int32)
        
        vertices = np.zeros((num_vertices, 3), dtype=np.float32)
        faces = np.zeros((num_faces, 3), dtype=np.int32)
        
        v_idx = 0
        f_idx = 0
        
        # North wall (y = max)
        i = rows - 1
        for j in range(cols - 1):
            x0, x1 = X[i, j], X[i, j + 1]
            y = Y[i, j]
            z0_top, z1_top = Z[i, j], Z[i, j + 1]
            
            v_base = v_idx + vertex_offset
            vertices[v_idx:v_idx + 4] = [
                [x0, y, z0_top],
                [x1, y, z1_top],
                [x1, y, base_z],
                [x0, y, base_z],
            ]
            faces[f_idx] = [v_base, v_base + 1, v_base + 2]
            faces[f_idx + 1] = [v_base, v_base + 2, v_base + 3]
            v_idx += 4
            f_idx += 2
            
        # South wall (y = 0)
        i = 0
        for j in range(cols - 1):
            x0, x1 = X[i, j], X[i, j + 1]
            y = Y[i, j]
            z0_top, z1_top = Z[i, j], Z[i, j + 1]
            
            v_base = v_idx + vertex_offset
            vertices[v_idx:v_idx + 4] = [
                [x0, y, z0_top],
                [x0, y, base_z],
                [x1, y, base_z],
                [x1, y, z1_top],
            ]
            faces[f_idx] = [v_base, v_base + 1, v_base + 2]
            faces[f_idx + 1] = [v_base, v_base + 2, v_base + 3]
            v_idx += 4
            f_idx += 2
            
        # East wall (x = max)
        j = cols - 1
        for i in range(rows - 1):
            x = X[i, j]
            y0, y1 = Y[i, j], Y[i + 1, j]
            z0_top, z1_top = Z[i, j], Z[i + 1, j]
            
            v_base = v_idx + vertex_offset
            vertices[v_idx:v_idx + 4] = [
                [x, y0, z0_top],
                [x, y0, base_z],
                [x, y1, base_z],
                [x, y1, z1_top],
            ]
            faces[f_idx] = [v_base, v_base + 1, v_base + 2]
            faces[f_idx + 1] = [v_base, v_base + 2, v_base + 3]
            v_idx += 4
            f_idx += 2
            
        # West wall (x = 0)
        j = 0
        for i in range(rows - 1):
            x = X[i, j]
            y0, y1 = Y[i, j], Y[i + 1, j]
            z0_top, z1_top = Z[i, j], Z[i + 1, j]
            
            v_base = v_idx + vertex_offset
            vertices[v_idx:v_idx + 4] = [
                [x, y0, z0_top],
                [x, y1, z1_top],
                [x, y1, base_z],
                [x, y0, base_z],
            ]
            faces[f_idx] = [v_base, v_base + 1, v_base + 2]
            faces[f_idx + 1] = [v_base, v_base + 2, v_base + 3]
            v_idx += 4
            f_idx += 2
            
        return vertices, faces
            
    def _calculate_vertex_normals(self, vertices: np.ndarray, 
                                   faces: np.ndarray) -> np.ndarray:
        """Calculate smooth vertex normals from face normals using vectorized operations."""
        # Initialize vertex normals
        normals = np.zeros_like(vertices)

        if faces.size == 0:
            return normals

        # Compute per-face normals in a vectorized way
        v0 = vertices[faces[:, 0]]
        v1 = vertices[faces[:, 1]]
        v2 = vertices[faces[:, 2]]

        edge1 = v1 - v0
        edge2 = v2 - v0
        face_normals = np.cross(edge1, edge2)

        # Normalize face normals, avoiding division by zero
        face_norms = np.linalg.norm(face_normals, axis=1, keepdims=True)
        nonzero_mask = (face_norms > 0).flatten()
        face_normals[nonzero_mask] = (
            face_normals[nonzero_mask] / face_norms[nonzero_mask]
        )

        # Accumulate face normals to vertices using advanced indexing
        flat_faces = faces.flatten()
        repeated_face_normals = np.repeat(face_normals, 3, axis=0)
        np.add.at(normals, flat_faces, repeated_face_normals)

        # Normalize vertex normals, avoiding division by zero
        norms = np.linalg.norm(normals, axis=1, keepdims=True)
        norms[norms == 0] = 1  # Avoid division by zero
        normals = normals / norms
        
        return normals
        
    def optimize_mesh(self, mesh: MeshData, target_faces: int) -> MeshData:
        """
        Optimize mesh by reducing face count while preserving features.
        
        Uses edge collapse decimation to reduce complexity.
        
        Args:
            mesh: Input mesh
            target_faces: Target number of faces
            
        Returns:
            Optimized mesh
        """
        # For now, return the original mesh
        # Full implementation would use quadric error metrics
        # This is a placeholder for the decimation algorithm
        return mesh
