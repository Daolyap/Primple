using Primple.Core.Enums;

namespace Primple.Core.Models;

/// <summary>
/// Represents a geographic bounding box for terrain selection.
/// </summary>
public class GeographicBounds
{
    public double North { get; set; }
    public double South { get; set; }
    public double East { get; set; }
    public double West { get; set; }

    public double Width => Math.Abs(East - West);
    public double Height => Math.Abs(North - South);
    public double CenterLat => (North + South) / 2;
    public double CenterLon => (East + West) / 2;

    public GeographicBounds() { }

    public GeographicBounds(double north, double south, double east, double west)
    {
        North = north;
        South = south;
        East = east;
        West = west;
    }

    /// <summary>
    /// Calculate approximate area in square kilometers.
    /// </summary>
    public double AreaKm2
    {
        get
        {
            const double earthRadiusKm = 6371;
            var latRad = CenterLat * Math.PI / 180;
            var widthKm = Width * Math.Cos(latRad) * (Math.PI / 180) * earthRadiusKm;
            var heightKm = Height * (Math.PI / 180) * earthRadiusKm;
            return widthKm * heightKm;
        }
    }

    public override string ToString() =>
        $"N:{North:F4}° S:{South:F4}° E:{East:F4}° W:{West:F4}°";
}

/// <summary>
/// Represents elevation data for a terrain region.
/// </summary>
public class ElevationData
{
    public double[,] Data { get; set; } = new double[0, 0];
    public int Width => Data.GetLength(1);
    public int Height => Data.GetLength(0);
    public GeographicBounds Bounds { get; set; } = new();
    public ElevationDataSource Source { get; set; }
    public double Resolution { get; set; } // meters per pixel
    public double MinElevation { get; set; }
    public double MaxElevation { get; set; }
    public double NoDataValue { get; set; } = -9999;

    public void CalculateStatistics()
    {
        MinElevation = double.MaxValue;
        MaxElevation = double.MinValue;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var value = Data[y, x];
                if (Math.Abs(value - NoDataValue) > 0.01)
                {
                    MinElevation = Math.Min(MinElevation, value);
                    MaxElevation = Math.Max(MaxElevation, value);
                }
            }
        }
    }
}

/// <summary>
/// Settings for terrain generation and processing.
/// </summary>
public class TerrainSettings
{
    // Elevation Settings
    public double VerticalExaggeration { get; set; } = 2.0;
    public double MinElevationClip { get; set; } = 0;
    public double MaxElevationClip { get; set; } = double.MaxValue;
    public bool NormalizeElevation { get; set; } = true;
    public bool RemoveBaseElevation { get; set; } = true;

    // Smoothing Settings
    public SmoothingMethod SmoothingMethod { get; set; } = SmoothingMethod.Gaussian;
    public double SmoothingRadius { get; set; } = 1.0;

    // Base Settings
    public BaseType BaseType { get; set; } = BaseType.Flat;
    public double BaseThickness { get; set; } = 3.0; // mm
    public EdgeTreatment EdgeTreatment { get; set; } = EdgeTreatment.Beveled30;

    // Size Settings
    public MapSizePreset SizePreset { get; set; } = MapSizePreset.Medium;
    public double CustomWidth { get; set; } = 150; // mm
    public double CustomLength { get; set; } = 150; // mm
    public double MaxHeight { get; set; } = 30; // mm
    public bool MaintainAspectRatio { get; set; } = true;

    // Mesh Settings
    public int MeshResolution { get; set; } = 500; // Grid points per side
    public double MinWallThickness { get; set; } = 0.8; // mm

    public (double Width, double Length) GetActualSize() => SizePreset switch
    {
        MapSizePreset.Small => (100, 100),
        MapSizePreset.Medium => (150, 150),
        MapSizePreset.Large => (200, 200),
        MapSizePreset.ExtraLarge => (250, 250),
        MapSizePreset.BambuMax => (256, 256),
        MapSizePreset.A1MiniMax => (180, 180),
        MapSizePreset.Custom => (CustomWidth, CustomLength),
        _ => (150, 150)
    };
}

/// <summary>
/// Printer profile with material and print settings.
/// </summary>
public class PrinterProfile
{
    public PrinterModel Model { get; set; } = PrinterModel.P2S;
    public MaterialType Material { get; set; } = MaterialType.PLA;
    public double LayerHeight { get; set; } = 0.2; // mm
    public double InfillPercentage { get; set; } = 15;
    public double PrintSpeed { get; set; } = 50; // mm/s
    public bool EnableSupports { get; set; } = false;
    public bool EnableBrim { get; set; } = true;
    public double BrimWidth { get; set; } = 5; // mm

    // Multi-color/AMS settings
    public bool UseAms { get; set; } = false;
    public List<MaterialColor> MaterialColors { get; set; } = new();
    public ColorMappingStrategy ColorStrategy { get; set; } = ColorMappingStrategy.TopographicStandard;
}

/// <summary>
/// Represents a material color for multi-material printing.
/// </summary>
public class MaterialColor
{
    public string Name { get; set; } = "";
    public string HexColor { get; set; } = "#FFFFFF";
    public MaterialType Material { get; set; } = MaterialType.PLA;
    public int AmsSlot { get; set; } = 0;
    public double ElevationStart { get; set; } = 0;
    public double ElevationEnd { get; set; } = 100;
}

/// <summary>
/// Complete terrain project with all settings.
/// </summary>
public class TerrainProject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Project";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    public GeographicBounds Bounds { get; set; } = new();
    public ElevationData? ElevationData { get; set; }
    public TerrainSettings Settings { get; set; } = new();
    public PrinterProfile PrinterProfile { get; set; } = new();

    // Annotations and markers
    public List<MapMarker> Markers { get; set; } = new();
    public List<TextAnnotation> Annotations { get; set; } = new();

    // Export metadata
    public string? LastExportPath { get; set; }
    public ExportFormat LastExportFormat { get; set; } = ExportFormat.STLBinary;
}

/// <summary>
/// Represents a marker on the map.
/// </summary>
public class MapMarker
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public MarkerType Type { get; set; } = MarkerType.Pin;
    public string IconPath { get; set; } = "";
    public double Size { get; set; } = 2; // mm
}

/// <summary>
/// Types of map markers.
/// </summary>
public enum MarkerType
{
    Pin,
    Flag,
    Pyramid,
    Sphere,
    Star,
    Peak,
    Campsite,
    Custom
}

/// <summary>
/// Text annotation on the map.
/// </summary>
public class TextAnnotation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string FontFamily { get; set; } = "Arial";
    public double FontSize { get; set; } = 3; // mm height
    public bool IsEmbossed { get; set; } = true;
    public double Depth { get; set; } = 0.5; // mm
    public double Rotation { get; set; } = 0; // degrees
}
