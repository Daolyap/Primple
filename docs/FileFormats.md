# File Formats

This document describes the file formats supported by Terrain Map Generator.

## Input Formats

### ASCII Grid (.asc, .grd)

The ASCII Grid format is a common GIS raster format supported by most GIS software.

#### Structure

```
ncols         100
nrows         100
xllcorner     -122.5
yllcorner     37.5
cellsize      0.001
NODATA_value  -9999
100.5 101.2 102.3 ...
...
```

#### Header Fields

| Field | Description |
|-------|-------------|
| `ncols` | Number of columns |
| `nrows` | Number of rows |
| `xllcorner` | X coordinate of lower-left corner |
| `yllcorner` | Y coordinate of lower-left corner |
| `cellsize` | Cell size in coordinate units |
| `NODATA_value` | Value representing no data (optional) |

#### Notes

- Values are space or tab-delimited
- Data is stored row by row, from top to bottom
- Coordinates are typically in decimal degrees or meters

---

### GeoTIFF (.tif, .tiff)

GeoTIFF is a standard image format with embedded geographic metadata.

#### Supported Features

- Grayscale elevation data
- 8-bit, 16-bit, and 32-bit integer values
- 32-bit floating-point values
- Little-endian and big-endian byte order

#### Limitations

- Complex tiling/striping not fully supported
- Some GeoTIFF tags are ignored
- For full GeoTIFF support, consider using GDAL

---

### HGT (.hgt) - NASA SRTM Format

The HGT format is used by NASA's Shuttle Radar Topography Mission (SRTM) data.

#### Structure

- Binary file with 16-bit signed big-endian integers
- No header - dimensions inferred from file size
- Void/no-data value: -32768

#### File Naming Convention

Files are named by the coordinates of the southwest corner:
- `N37W122.hgt` - 37°N, 122°W
- `S12E045.hgt` - 12°S, 45°E

#### Resolutions

| Type | Size | Resolution | File Size |
|------|------|------------|-----------|
| SRTM1 | 3601×3601 | 1 arc-second (~30m) | ~25 MB |
| SRTM3 | 1201×1201 | 3 arc-seconds (~90m) | ~2.8 MB |

#### Example Sources

- [USGS EarthExplorer](https://earthexplorer.usgs.gov/)
- [OpenTopography](https://opentopography.org/)
- [NASA Earthdata](https://earthdata.nasa.gov/)

---

### PNG Heightmap (.png)

PNG images can be used as heightmaps where pixel brightness represents elevation.

#### Supported Color Types

| Type | Channels | Description |
|------|----------|-------------|
| Grayscale | 1 | 8-bit or 16-bit grayscale |
| RGB | 3 | Converted to grayscale |
| RGBA | 4 | Alpha ignored, converted to grayscale |

#### Elevation Mapping

- 0 (black) = minimum elevation
- 255 (white) = maximum elevation
- For 16-bit: 0 = min, 65535 = max

#### Grayscale Conversion

For color images: `gray = 0.299R + 0.587G + 0.114B`

#### Notes

- No geographic metadata - assumes 1:1 cell size
- Elevation range defaults to 0-1000m
- Higher bit depth provides smoother gradients

---

### XYZ Point Cloud (.xyz)

Simple text format with X, Y, Z coordinates per point.

#### Structure

```
x1 y1 z1
x2 y2 z2
...
```

#### Variations

- Space, tab, or comma delimited
- Optional header lines (starting with #)
- Irregular point spacing supported

#### Notes

- Points are automatically gridded
- Grid resolution estimated from point spacing
- Gaps filled with no-data values

---

## Output Formats

### STL (Binary)

Standard 3D printing format in binary encoding.

#### Structure

```
[80-byte header]
[4-byte triangle count]
[50-byte per triangle] × count
```

#### Per Triangle (50 bytes)

| Field | Type | Size |
|-------|------|------|
| Normal | 3 × float32 | 12 bytes |
| Vertex 1 | 3 × float32 | 12 bytes |
| Vertex 2 | 3 × float32 | 12 bytes |
| Vertex 3 | 3 × float32 | 12 bytes |
| Attribute | uint16 | 2 bytes |

### STL (ASCII)

Human-readable STL format.

```
solid terrain
  facet normal 0.0 0.0 1.0
    outer loop
      vertex 0.0 0.0 10.0
      vertex 100.0 0.0 10.0
      vertex 100.0 100.0 10.0
    endloop
  endfacet
  ...
endsolid terrain
```

---

### OBJ (Wavefront)

Common 3D format with material support.

#### OBJ File Structure

```obj
# Comment
mtllib terrain.mtl

o terrain
v x y z     # vertex
vn x y z    # normal (optional)
vt u v      # texture coord (optional)
usemtl terrain_material
f v1 v2 v3  # face
f v1/vt1 v2/vt2 v3/vt3  # face with texture
f v1//vn1 v2//vn2 v3//vn3  # face with normal
```

#### MTL File Structure

```mtl
newmtl terrain_material
Ka 0.2 0.2 0.2    # ambient color
Kd 0.8 0.8 0.8    # diffuse color
Ks 0.1 0.1 0.1    # specular color
Ns 10.0           # shininess
d 1.0             # dissolve (opacity)
illum 2           # illumination model
map_Kd texture.png  # texture map (optional)
```

---

### 3MF (3D Manufacturing Format)

Modern 3D printing format supporting multi-material and color.

#### Structure

3MF is a ZIP archive containing:

```
├── [Content_Types].xml
├── _rels/
│   └── .rels
└── 3D/
    └── 3dmodel.model
```

#### Features

- XML-based mesh data
- Support for vertex colors
- Multiple materials/objects
- Metadata and thumbnails
- Compact compression

#### Color Support

Triangles can reference base materials with colors:

```xml
<basematerials id="1">
  <base name="Green" displaycolor="#228B22"/>
  <base name="White" displaycolor="#FFFFFF"/>
</basematerials>
<triangle v1="0" v2="1" v3="2" pid="1" p1="0"/>
```

---

## Data Sources

### Free Elevation Data

| Source | Resolution | Coverage |
|--------|------------|----------|
| SRTM | 30m / 90m | ±60° latitude |
| ASTER GDEM | 30m | Global |
| ALOS World 3D | 30m | Global |
| NED (USA) | 10m / 30m | United States |
| EU-DEM | 25m | Europe |
| LINZ (NZ) | 8m | New Zealand |

### Download Links

- **USGS EarthExplorer**: https://earthexplorer.usgs.gov/
- **OpenTopography**: https://opentopography.org/
- **Copernicus**: https://land.copernicus.eu/imagery-in-situ/eu-dem
- **JAXA ALOS**: https://www.eorc.jaxa.jp/ALOS/en/aw3d30/

### Creating Heightmaps

For custom heightmaps:

1. Create grayscale image in any graphics editor
2. White = high elevation, Black = low elevation
3. Use 16-bit PNG for smoother gradients
4. Power-of-two dimensions recommended (512×512, 1024×1024)
