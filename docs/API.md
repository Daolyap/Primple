# API Reference

This document provides detailed API documentation for the Terrain Map Generator library.

## Core Interfaces

### IElevationDataService

Service for loading elevation data from various file formats.

```csharp
public interface IElevationDataService
{
    Task<ElevationData> LoadFromFileAsync(string filePath);
    ElevationData LoadFromFile(string filePath);
    bool IsFormatSupported(string filePath);
    IReadOnlyList<string> SupportedExtensions { get; }
}
```

**Implementation:** `ElevationDataService`

#### Methods

| Method | Description |
|--------|-------------|
| `LoadFromFileAsync` | Asynchronously loads elevation data from a file |
| `LoadFromFile` | Synchronously loads elevation data from a file |
| `IsFormatSupported` | Checks if a file format is supported |

#### Example

```csharp
var service = new ElevationDataService();
var data = await service.LoadFromFileAsync("terrain.asc");
Console.WriteLine($"Loaded {data.Width}x{data.Height} grid");
Console.WriteLine($"Elevation range: {data.MinElevation}m to {data.MaxElevation}m");
```

---

### IMeshGenerator

Service for generating terrain meshes from elevation data.

```csharp
public interface IMeshGenerator
{
    TerrainMesh Generate(ElevationData elevationData, MapConfiguration configuration);
    Task<TerrainMesh> GenerateAsync(ElevationData elevationData, MapConfiguration configuration);
}
```

**Implementation:** `MeshGenerator`

#### Methods

| Method | Description |
|--------|-------------|
| `Generate` | Generates a terrain mesh synchronously |
| `GenerateAsync` | Generates a terrain mesh asynchronously |

---

### IStlExporter

Service for exporting meshes to STL format.

```csharp
public interface IStlExporter
{
    void ExportBinary(TerrainMesh mesh, string filePath);
    Task ExportBinaryAsync(TerrainMesh mesh, string filePath);
    void ExportAscii(TerrainMesh mesh, string filePath);
    Task ExportAsciiAsync(TerrainMesh mesh, string filePath);
    byte[] ExportToBytes(TerrainMesh mesh);
}
```

**Implementation:** `StlExporter`

---

### I3MfExporter

Service for exporting meshes to 3MF format with multi-material and color support.

```csharp
public interface I3MfExporter
{
    void Export(TerrainMesh mesh, string filePath);
    Task ExportAsync(TerrainMesh mesh, string filePath);
    void Export(TerrainMesh mesh, string filePath, ThreeMfExportOptions options);
    Task ExportAsync(TerrainMesh mesh, string filePath, ThreeMfExportOptions options);
    byte[] ExportToBytes(TerrainMesh mesh);
    byte[] ExportToBytes(TerrainMesh mesh, ThreeMfExportOptions options);
}
```

**Implementation:** `ThreeMfExporter`

#### ThreeMfExportOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Title` | string | "Terrain Map" | Model title |
| `EnableColors` | bool | false | Enable vertex colors |
| `ColorGradient` | List<ColorStop> | Green-to-white | Elevation color gradient |
| `Unit` | string | "millimeter" | Measurement unit |

---

### IObjExporter

Service for exporting meshes to OBJ format with MTL material support.

```csharp
public interface IObjExporter
{
    void Export(TerrainMesh mesh, string filePath);
    Task ExportAsync(TerrainMesh mesh, string filePath);
    void Export(TerrainMesh mesh, string filePath, ObjExportOptions options);
    Task ExportAsync(TerrainMesh mesh, string filePath, ObjExportOptions options);
    string ExportToString(TerrainMesh mesh);
    string GenerateMtlContent(ObjExportOptions options);
}
```

**Implementation:** `ObjExporter`

---

### IContourGenerator

Service for generating contour lines from elevation data.

```csharp
public interface IContourGenerator
{
    ContourResult GenerateContours(ElevationData elevationData, ContourOptions options);
    Task<ContourResult> GenerateContoursAsync(ElevationData elevationData, ContourOptions options);
    TerrainMesh ApplyContoursToMesh(TerrainMesh mesh, ContourResult contours, float depth, bool emboss = false);
}
```

**Implementation:** `ContourGenerator`

#### ContourOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Interval` | float | 100 | Contour interval in meters |
| `MajorInterval` | float | 500 | Major contour interval |
| `LineWidth` | float | 0.5 | Line width in mm |
| `MinSegmentLength` | float | 1.0 | Minimum segment length |
| `SmoothContours` | bool | true | Apply smoothing |

---

### IHydrographyService

Service for water feature detection and processing.

```csharp
public interface IHydrographyService
{
    HydrographyResult AnalyzeFromElevation(ElevationData elevationData, HydrographyOptions options);
    Task<HydrographyResult> AnalyzeFromElevationAsync(ElevationData elevationData, HydrographyOptions options);
    HydrographyResult LoadFromGeoJson(string geoJsonPath, GeographicBounds bounds);
    Task<HydrographyResult> LoadFromGeoJsonAsync(string geoJsonPath, GeographicBounds bounds);
    ElevationData CarveWaterFeatures(ElevationData elevationData, HydrographyResult waterFeatures, float carveDepth);
    TerrainMesh ApplyToMesh(TerrainMesh mesh, HydrographyResult waterFeatures, WaterMeshOptions options);
}
```

**Implementation:** `HydrographyService`

---

### ILabelGenerator

Service for generating text labels on terrain.

```csharp
public interface ILabelGenerator
{
    LabelResult GeneratePeakLabels(ElevationData elevationData, LabelOptions options);
    Task<LabelResult> GeneratePeakLabelsAsync(ElevationData elevationData, LabelOptions options);
    TerrainLabel CreateLabel(string text, (float X, float Y) position, LabelOptions options);
    TerrainMesh ApplyLabelsToMesh(TerrainMesh mesh, LabelResult labels, bool emboss = true);
    TerrainMesh ApplyLabelToMesh(TerrainMesh mesh, TerrainLabel label, bool emboss = true);
}
```

**Implementation:** `LabelGenerator`

---

## Models

### ElevationData

Represents a grid of elevation values.

| Property | Type | Description |
|----------|------|-------------|
| `Width` | int | Number of columns |
| `Height` | int | Number of rows |
| `CellSizeX` | double | X cell size in meters |
| `CellSizeY` | double | Y cell size in meters |
| `MinElevation` | float | Minimum elevation value |
| `MaxElevation` | float | Maximum elevation value |
| `Values` | float[,] | Elevation grid |
| `NoDataValue` | float | No-data sentinel value |
| `OriginX` | double | Origin longitude/easting |
| `OriginY` | double | Origin latitude/northing |

### TerrainMesh

Represents a 3D mesh with vertices and triangles.

| Property | Type | Description |
|----------|------|-------------|
| `Vertices` | IReadOnlyList<Vector3> | Mesh vertices |
| `Triangles` | IReadOnlyList<Triangle> | Triangle indices |
| `Normals` | IReadOnlyList<Vector3> | Per-triangle normals |
| `VertexCount` | int | Number of vertices |
| `TriangleCount` | int | Number of triangles |
| `BoundsMin` | Vector3 | Bounding box minimum |
| `BoundsMax` | Vector3 | Bounding box maximum |

### MapConfiguration

Configuration for terrain map generation.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `OutputWidthMm` | double | 150 | Output width in mm |
| `OutputHeightMm` | double | 150 | Output height in mm |
| `MaxPrintHeightMm` | double | 30 | Maximum Z height in mm |
| `VerticalExaggeration` | double | 1.5 | Vertical scale factor |
| `BaseType` | BaseType | Flat | Base style |
| `BaseThicknessMm` | double | 3 | Base thickness in mm |
| `EdgeType` | EdgeType | Vertical | Edge style |
| `MeshResolution` | double | 1.0 | Resolution multiplier |
| `ApplySmoothing` | bool | false | Enable smoothing |
| `SmoothingRadius` | int | 1 | Smoothing kernel radius |

---

## Dependency Injection

### ServiceCollectionExtensions

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTerrainMapGenerator(this IServiceCollection services);
    public static IServiceCollection AddTerrainMapGenerator(this IServiceCollection services, 
        Action<TerrainMapGeneratorOptions> configure);
}
```

#### TerrainMapGeneratorOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableCaching` | bool | false | Enable elevation data caching |
| `MaxCacheSizeMb` | int | 100 | Maximum cache size |
| `DefaultExportFormat` | string | "stl" | Default export format |
| `ValidateMeshes` | bool | true | Validate meshes after generation |
| `DefaultVerticalExaggeration` | double | 1.5 | Default exaggeration |

---

## Enums

### BaseType

```csharp
public enum BaseType
{
    None,      // No base
    Flat,      // Flat base
    Tapered,   // Tapered edges
    Contoured, // Follows terrain contour
    Minimal    // Minimal flat base
}
```

### EdgeType

```csharp
public enum EdgeType
{
    Vertical,  // Vertical walls
    Beveled,   // Angled bevel
    Curved     // Curved transition
}
```

### ElevationDataFormat

```csharp
public enum ElevationDataFormat
{
    GeoTiff,   // .tif, .tiff
    AsciiGrid, // .asc, .grd
    Hgt,       // .hgt (SRTM)
    Xyz,       // .xyz
    Png,       // .png heightmap
    Raw        // Raw binary
}
```
