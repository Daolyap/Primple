using System.Windows.Media;

namespace Primple.Core.Services;

public enum BaseShape
{
    Square,
    Circular
}

public class MapGenerationOptions
{
    public double CenterLat { get; set; }
    public double CenterLon { get; set; }
    public double RadiusMeters { get; set; }
    public bool IncludeBuildings { get; set; }
    public bool IncludeRoads { get; set; }
    public bool Is3DMode { get; set; }
    public int Resolution { get; set; } // 1-10 scale? Or maybe meters per vertex? Let's say 1-100 detail level.

    // Colors
    public Color BaseColor { get; set; }
    public Color BuildingColor { get; set; }
    public Color RoadColor { get; set; }
    public Color WaterColor { get; set; }
    public BaseShape BaseShape { get; set; } = BaseShape.Square;
}
