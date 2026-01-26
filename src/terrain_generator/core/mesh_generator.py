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
        
        # Check for degenerate triangles
        for i, face in enumerate(self.faces):
            v0, v1, v2 = self.vertices[face]
            edge1 = v1 - v0
            edge2 = v2 - v0
            area = np.linalg.norm(np.cross(edge1, edge2)) / 2
            if area < 1e-10:
                issues.append(f"Degenerate triangle at face {i}")
                
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
        rows, cols = Z.shape
        
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
        
        # Create faces (two triangles per grid cell)
        faces = []
        for i in range(rows - 1):
            for j in range(cols - 1):
                # Vertex indices
                v00 = i * cols + j
                v01 = i * cols + (j + 1)
                v10 = (i + 1) * cols + j
                v11 = (i + 1) * cols + (j + 1)
                
                # Two triangles per cell
                faces.append([v00, v10, v01])  # Lower-left triangle
                faces.append([v01, v10, v11])  # Upper-right triangle
                
        return vertices, np.array(faces, dtype=np.int32)
        
    def _generate_walls(self, X: np.ndarray, Y: np.ndarray, Z: np.ndarray,
                        base_z: float, vertex_offset: int) -> Tuple[np.ndarray, np.ndarray]:
        """Generate side wall vertices and faces."""
        rows, cols = Z.shape
        vertices = []
        faces = []
        
        # North wall (y = max)
        for j in range(cols - 1):
            i = rows - 1
            x0, x1 = X[i, j], X[i, j + 1]
            y = Y[i, j]
            z0_top, z1_top = Z[i, j], Z[i, j + 1]
            
            v_base = len(vertices) + vertex_offset
            vertices.extend([
                [x0, y, z0_top],
                [x1, y, z1_top],
                [x1, y, base_z],
                [x0, y, base_z],
            ])
            faces.append([v_base, v_base + 1, v_base + 2])
            faces.append([v_base, v_base + 2, v_base + 3])
            
        # South wall (y = 0)
        for j in range(cols - 1):
            i = 0
            x0, x1 = X[i, j], X[i, j + 1]
            y = Y[i, j]
            z0_top, z1_top = Z[i, j], Z[i, j + 1]
            
            v_base = len(vertices) + vertex_offset
            vertices.extend([
                [x0, y, z0_top],
                [x0, y, base_z],
                [x1, y, base_z],
                [x1, y, z1_top],
            ])
            faces.append([v_base, v_base + 1, v_base + 2])
            faces.append([v_base, v_base + 2, v_base + 3])
            
        # East wall (x = max)
        for i in range(rows - 1):
            j = cols - 1
            x = X[i, j]
            y0, y1 = Y[i, j], Y[i + 1, j]
            z0_top, z1_top = Z[i, j], Z[i + 1, j]
            
            v_base = len(vertices) + vertex_offset
            vertices.extend([
                [x, y0, z0_top],
                [x, y0, base_z],
                [x, y1, base_z],
                [x, y1, z1_top],
            ])
            faces.append([v_base, v_base + 1, v_base + 2])
            faces.append([v_base, v_base + 2, v_base + 3])
            
        # West wall (x = 0)
        for i in range(rows - 1):
            j = 0
            x = X[i, j]
            y0, y1 = Y[i, j], Y[i + 1, j]
            z0_top, z1_top = Z[i, j], Z[i + 1, j]
            
            v_base = len(vertices) + vertex_offset
            vertices.extend([
                [x, y0, z0_top],
                [x, y1, z1_top],
                [x, y1, base_z],
                [x, y0, base_z],
            ])
            faces.append([v_base, v_base + 1, v_base + 2])
            faces.append([v_base, v_base + 2, v_base + 3])
            
        if vertices:
            return np.array(vertices, dtype=np.float32), np.array(faces, dtype=np.int32)
        else:
            return np.empty((0, 3), dtype=np.float32), np.empty((0, 3), dtype=np.int32)
            
    def _calculate_vertex_normals(self, vertices: np.ndarray, 
                                   faces: np.ndarray) -> np.ndarray:
        """Calculate smooth vertex normals from face normals."""
        normals = np.zeros_like(vertices)
        
        for face in faces:
            v0, v1, v2 = vertices[face]
            edge1 = v1 - v0
            edge2 = v2 - v0
            face_normal = np.cross(edge1, edge2)
            norm = np.linalg.norm(face_normal)
            if norm > 0:
                face_normal = face_normal / norm
                
            # Add to vertex normals
            for idx in face:
                normals[idx] += face_normal
                
        # Normalize
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
