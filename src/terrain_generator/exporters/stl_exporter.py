"""
STL Exporter Module
Exports terrain meshes to STL format for 3D printing.
"""

import struct
import zipfile
import xml.etree.ElementTree as ET
import numpy as np
from typing import Optional
from pathlib import Path

from ..core.mesh_generator import MeshData


class STLExporter:
    """Exports mesh data to STL format."""
    
    def __init__(self):
        """Initialize STL exporter."""
        pass
        
    def export_binary(self, mesh: MeshData, filepath: str, 
                      header: str = "Terrain Map Generator STL Export") -> None:
        """
        Export mesh to binary STL format.
        
        Binary STL is more compact and faster to read/write.
        
        Args:
            mesh: Mesh data to export
            filepath: Output file path
            header: 80-character header string
        """
        filepath = Path(filepath)
        filepath.parent.mkdir(parents=True, exist_ok=True)
        
        with open(filepath, 'wb') as f:
            # Write header (80 bytes) - use 'replace' error handling for non-ASCII characters
            header_bytes = header.encode('ascii', errors='replace')[:80]
            header_bytes = header_bytes.ljust(80, b'\0')
            f.write(header_bytes)
            
            # Write triangle count (4 bytes, unsigned int)
            f.write(struct.pack('<I', len(mesh.faces)))
            
            # Write triangles
            for face in mesh.faces:
                v0, v1, v2 = mesh.vertices[face]
                
                # Calculate face normal
                edge1 = v1 - v0
                edge2 = v2 - v0
                normal = np.cross(edge1, edge2)
                norm_len = np.linalg.norm(normal)
                if norm_len > 0:
                    normal = normal / norm_len
                else:
                    # Use a default normal for degenerate triangles
                    normal = np.array([0.0, 0.0, 1.0], dtype=float)
                    
                # Write normal (3 floats, 12 bytes)
                f.write(struct.pack('<3f', *normal))
                
                # Write vertices (9 floats, 36 bytes)
                f.write(struct.pack('<3f', *v0))
                f.write(struct.pack('<3f', *v1))
                f.write(struct.pack('<3f', *v2))
                
                # Write attribute byte count (2 bytes, typically 0)
                f.write(struct.pack('<H', 0))
                
    def export_ascii(self, mesh: MeshData, filepath: str,
                     solid_name: str = "terrain") -> None:
        """
        Export mesh to ASCII STL format.
        
        ASCII is human-readable but larger file size.
        
        Args:
            mesh: Mesh data to export
            filepath: Output file path
            solid_name: Name of the solid in STL file
        """
        filepath = Path(filepath)
        filepath.parent.mkdir(parents=True, exist_ok=True)
        
        with open(filepath, 'w') as f:
            f.write(f"solid {solid_name}\n")
            
            for face in mesh.faces:
                v0, v1, v2 = mesh.vertices[face]
                
                # Calculate face normal
                edge1 = v1 - v0
                edge2 = v2 - v0
                normal = np.cross(edge1, edge2)
                norm_len = np.linalg.norm(normal)
                if norm_len > 0:
                    normal = normal / norm_len
                else:
                    # Fallback normal for degenerate triangles to avoid zero-length normals in STL
                    normal = np.array([0.0, 0.0, 1.0], dtype=float)
                    
                f.write(f"  facet normal {normal[0]:.6e} {normal[1]:.6e} {normal[2]:.6e}\n")
                f.write(f"    outer loop\n")
                f.write(f"      vertex {v0[0]:.6e} {v0[1]:.6e} {v0[2]:.6e}\n")
                f.write(f"      vertex {v1[0]:.6e} {v1[1]:.6e} {v1[2]:.6e}\n")
                f.write(f"      vertex {v2[0]:.6e} {v2[1]:.6e} {v2[2]:.6e}\n")
                f.write(f"    endloop\n")
                f.write(f"  endfacet\n")
                
            f.write(f"endsolid {solid_name}\n")
            
    def export(self, mesh: MeshData, filepath: str, binary: bool = True,
               **kwargs) -> None:
        """
        Export mesh to STL format.
        
        Args:
            mesh: Mesh data to export
            filepath: Output file path
            binary: If True, export binary STL; otherwise ASCII
            **kwargs: Additional arguments passed to specific export method
        """
        if binary:
            self.export_binary(mesh, filepath, **kwargs)
        else:
            self.export_ascii(mesh, filepath, **kwargs)
            
    def validate_for_printing(self, mesh: MeshData) -> dict:
        """
        Validate mesh for 3D printing compatibility.
        
        Returns:
            Dictionary with validation results
        """
        results = {
            "is_valid": True,
            "issues": [],
            "warnings": [],
            "statistics": {}
        }
        
        # Check vertex count
        results["statistics"]["vertex_count"] = mesh.vertex_count
        results["statistics"]["face_count"] = mesh.face_count
        
        # Calculate bounding box
        min_coords = mesh.vertices.min(axis=0)
        max_coords = mesh.vertices.max(axis=0)
        dimensions = max_coords - min_coords
        
        results["statistics"]["dimensions_mm"] = {
            "width": float(dimensions[0]),
            "depth": float(dimensions[1]),
            "height": float(dimensions[2])
        }
        results["statistics"]["bounding_box"] = {
            "min": min_coords.tolist(),
            "max": max_coords.tolist()
        }
        
        # Check for degenerate faces
        degenerate_count = 0
        for face in mesh.faces:
            v0, v1, v2 = mesh.vertices[face]
            edge1 = v1 - v0
            edge2 = v2 - v0
            area = np.linalg.norm(np.cross(edge1, edge2)) / 2
            if area < 1e-10:
                degenerate_count += 1
                
        if degenerate_count > 0:
            results["warnings"].append(f"{degenerate_count} degenerate triangles detected")
            
        # Check minimum thickness (simplified)
        min_height = dimensions[2]
        if min_height < 0.8:  # 0.8mm minimum for most printers
            results["issues"].append(f"Model height ({min_height:.2f}mm) below minimum 0.8mm")
            results["is_valid"] = False
            
        # Estimate file size
        # Binary STL: 80 header + 4 count + (50 bytes per triangle)
        binary_size = 84 + len(mesh.faces) * 50
        results["statistics"]["estimated_file_size_bytes"] = binary_size
        results["statistics"]["estimated_file_size_mb"] = binary_size / (1024 * 1024)
        
        return results


class ThreeMFExporter:
    """Exports mesh data to 3MF format with color/material support."""
    
    def __init__(self):
        """Initialize 3MF exporter."""
        pass
        
    def export(self, mesh: MeshData, filepath: str,
               colors: Optional[np.ndarray] = None,
               metadata: Optional[dict] = None) -> None:
        """
        Export mesh to 3MF format.
        
        3MF supports colors, materials, and metadata - ideal for Bambu printers.
        
        Args:
            mesh: Mesh data to export
            filepath: Output file path
            colors: Optional Nx3 array of RGB colors per vertex
            metadata: Optional metadata dictionary
        """
        filepath = Path(filepath)
        filepath.parent.mkdir(parents=True, exist_ok=True)
        
        # Create 3MF package (zip file)
        with zipfile.ZipFile(filepath, 'w', zipfile.ZIP_DEFLATED) as zf:
            # Write content types
            content_types = self._create_content_types()
            zf.writestr('[Content_Types].xml', content_types)
            
            # Write relationships
            rels = self._create_relationships()
            zf.writestr('_rels/.rels', rels)
            
            # Write 3D model
            model = self._create_model(mesh, colors, metadata)
            zf.writestr('3D/3dmodel.model', model)
            
    def _create_content_types(self) -> str:
        """Create content types XML."""
        return '''<?xml version="1.0" encoding="UTF-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
    <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
    <Default Extension="model" ContentType="application/vnd.ms-package.3dmanufacturing-3dmodel+xml"/>
</Types>'''

    def _create_relationships(self) -> str:
        """Create relationships XML."""
        return '''<?xml version="1.0" encoding="UTF-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
    <Relationship Target="/3D/3dmodel.model" Id="rel0" Type="http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel"/>
</Relationships>'''

    def _create_model(self, mesh: MeshData, colors: Optional[np.ndarray],
                      metadata: Optional[dict]) -> str:
        """Create 3D model XML."""
        ns = "http://schemas.microsoft.com/3dmanufacturing/core/2015/02"
        
        root = ET.Element('model', {
            'xmlns': ns,
            'unit': 'millimeter',
            'xml:lang': 'en-US'
        })
        
        # Add metadata
        if metadata:
            meta = ET.SubElement(root, 'metadata', {'name': 'Title'})
            meta.text = metadata.get('title', 'Terrain Map')
            
        resources = ET.SubElement(root, 'resources')
        obj = ET.SubElement(resources, 'object', {'id': '1', 'type': 'model'})
        mesh_elem = ET.SubElement(obj, 'mesh')
        
        # Vertices
        vertices_elem = ET.SubElement(mesh_elem, 'vertices')
        for v in mesh.vertices:
            ET.SubElement(vertices_elem, 'vertex', {
                'x': f'{v[0]:.6f}',
                'y': f'{v[1]:.6f}',
                'z': f'{v[2]:.6f}'
            })
            
        # Triangles
        triangles_elem = ET.SubElement(mesh_elem, 'triangles')
        for f in mesh.faces:
            ET.SubElement(triangles_elem, 'triangle', {
                'v1': str(f[0]),
                'v2': str(f[1]),
                'v3': str(f[2])
            })
            
        # Build section
        build = ET.SubElement(root, 'build')
        ET.SubElement(build, 'item', {'objectid': '1'})
        
        return ET.tostring(root, encoding='unicode', xml_declaration=True)
