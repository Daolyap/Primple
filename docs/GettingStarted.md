# Getting Started with Terrain Map Generator

This guide will help you get started with the Terrain Map Generator library for creating 3D printable terrain maps.

## Installation

### NuGet Package

```bash
dotnet add package TerrainMapGenerator.Core
```

### From Source

```bash
git clone https://github.com/Primple/TerrainMapGenerator.git
cd TerrainMapGenerator
dotnet build
```

## Quick Start

### Basic Usage

```csharp
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;
using TerrainMapGenerator.Core.Services;

// Load elevation data
var elevationService = new ElevationDataService();
var elevationData = await elevationService.LoadFromFileAsync("terrain.asc");

// Configure the map
var config = new MapConfiguration
{
    OutputWidthMm = 150,
    OutputHeightMm = 150,
    MaxPrintHeightMm = 30,
    VerticalExaggeration = 2.0,
    BaseType = BaseType.Flat,
    BaseThicknessMm = 3
};

// Generate the mesh
var meshGenerator = new MeshGenerator();
var mesh = await meshGenerator.GenerateAsync(elevationData, config);

// Export to STL
var exporter = new StlExporter();
await exporter.ExportBinaryAsync(mesh, "terrain.stl");
```

### Using Dependency Injection

```csharp
using Microsoft.Extensions.DependencyInjection;
using TerrainMapGenerator.Core;

// Configure services
var services = new ServiceCollection();
services.AddTerrainMapGenerator();
services.AddLogging();

var serviceProvider = services.BuildServiceProvider();

// Use services
var elevationService = serviceProvider.GetRequiredService<IElevationDataService>();
var meshGenerator = serviceProvider.GetRequiredService<IMeshGenerator>();
var exporter = serviceProvider.GetRequiredService<IStlExporter>();
```

## Supported Input Formats

- **ASCII Grid (.asc, .grd)** - Common GIS raster format
- **GeoTIFF (.tif, .tiff)** - Geographic image format
- **HGT (.hgt)** - NASA SRTM elevation data
- **PNG Heightmap (.png)** - Grayscale elevation images
- **XYZ (.xyz)** - Point cloud format

## Export Formats

- **STL (binary/ASCII)** - Universal 3D printing format
- **OBJ** - Wavefront format with MTL materials
- **3MF** - Modern 3D printing format with color support

## Configuration Options

### Map Configuration

| Option | Default | Description |
|--------|---------|-------------|
| `OutputWidthMm` | 150 | Output width in millimeters |
| `OutputHeightMm` | 150 | Output height in millimeters |
| `MaxPrintHeightMm` | 30 | Maximum Z height in millimeters |
| `VerticalExaggeration` | 1.5 | Terrain exaggeration factor |
| `BaseType` | Flat | Base type: Flat, Tapered, Contoured, Minimal, None |
| `BaseThicknessMm` | 3 | Base thickness in millimeters |
| `ApplySmoothing` | false | Apply Gaussian smoothing |
| `MeshResolution` | 1.0 | Resolution multiplier (0-2) |

### Printer Profiles

Built-in support for Bambu Labs printers:
- X1C (X1 Carbon)
- X1E
- P1P
- P1S
- P2S
- A1
- A1 Mini

```csharp
var printerService = new PrinterProfileService();
var printer = printerService.GetProfileByName("X1C");
var constrainedConfig = printer.CreateConstrainedConfiguration(config);
```

## Desktop Application

The Terrain Map Generator also provides a cross-platform desktop application with a graphical user interface.

### Running the Desktop Application

Download the appropriate version for your platform from the releases page, or build from source:

```bash
# Build the desktop application
dotnet build src/TerrainMapGenerator.Desktop/TerrainMapGenerator.Desktop.csproj

# Run the desktop application
dotnet run --project src/TerrainMapGenerator.Desktop/TerrainMapGenerator.Desktop.csproj
```

### Desktop Features

- **File Selection**: Browse and select input elevation data files (ASC, GeoTIFF, HGT, PNG, XYZ)
- **Printer Profiles**: Select from built-in Bambu Labs printer profiles
- **Configuration Controls**: Adjust width, height, exaggeration, base type, and more
- **Progress Indicator**: Track generation progress
- **3D Preview**: Preview area for generated terrain (coming soon)

## Examples

### Generate with Contour Lines

```csharp
var contourGenerator = new ContourGenerator();
var contours = contourGenerator.GenerateContours(elevationData, new ContourOptions
{
    Interval = 100,       // 100m contour interval
    MajorInterval = 500,  // Major contours every 500m
    LineWidth = 0.5f,
    SmoothContours = true
});

mesh = contourGenerator.ApplyContoursToMesh(mesh, contours, depth: 0.3f, emboss: false);
```

### Add Peak Labels

```csharp
var labelGenerator = new LabelGenerator();
var labels = labelGenerator.GeneratePeakLabels(elevationData, new LabelOptions
{
    MaxPeakLabels = 5,
    MinProminence = 100,
    FontSize = 3,
    IncludeElevation = true
});

mesh = labelGenerator.ApplyLabelsToMesh(mesh, labels, emboss: true);
```

### Export with Colors (3MF)

```csharp
var exporter3mf = new ThreeMfExporter();
await exporter3mf.ExportAsync(mesh, "terrain.3mf", new ThreeMfExportOptions
{
    Title = "Mountain Terrain",
    EnableColors = true,
    ColorGradient = new List<ColorStop>
    {
        new(0.0f, new Color(34, 139, 34)),   // Green (low)
        new(0.5f, new Color(210, 180, 140)), // Tan (mid)
        new(1.0f, new Color(255, 255, 255))  // White (peaks)
    }
});
```

## Command Line Interface

```bash
# Basic usage
TerrainMapGenerator.Cli input.asc output.stl

# With options
TerrainMapGenerator.Cli -i terrain.tif -o model.stl -w 200 -e 2.0 -p X1C

# Apply smoothing and set base type
TerrainMapGenerator.Cli terrain.asc --smooth --base tapered -z 50

# List available printer profiles
TerrainMapGenerator.Cli --list-printers
```

## Next Steps

- Read the [API Reference](API.md) for detailed documentation
- Check [File Formats](FileFormats.md) for format specifications
- Explore the samples folder for example data
