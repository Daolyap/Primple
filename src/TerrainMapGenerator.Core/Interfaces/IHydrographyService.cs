using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Interfaces;

/// <summary>
/// Service for identifying and processing water features (rivers, lakes) from elevation data
/// or external data sources like OpenStreetMap.
/// </summary>
public interface IHydrographyService
{
    /// <summary>
    /// Identifies water bodies from elevation data using flow accumulation analysis.
    /// </summary>
    /// <param name="elevationData">Source elevation data.</param>
    /// <param name="options">Hydrography analysis options.</param>
    /// <returns>Identified water features.</returns>
    HydrographyResult AnalyzeFromElevation(ElevationData elevationData, HydrographyOptions options);

    /// <summary>
    /// Identifies water bodies from elevation data asynchronously.
    /// </summary>
    Task<HydrographyResult> AnalyzeFromElevationAsync(ElevationData elevationData, HydrographyOptions options);

    /// <summary>
    /// Loads water features from GeoJSON data (e.g., from OpenStreetMap export).
    /// </summary>
    /// <param name="geoJsonPath">Path to GeoJSON file containing water features.</param>
    /// <param name="bounds">Geographic bounds to filter features.</param>
    /// <returns>Loaded water features.</returns>
    HydrographyResult LoadFromGeoJson(string geoJsonPath, GeographicBounds bounds);

    /// <summary>
    /// Loads water features from GeoJSON asynchronously.
    /// </summary>
    Task<HydrographyResult> LoadFromGeoJsonAsync(string geoJsonPath, GeographicBounds bounds);

    /// <summary>
    /// Carves river channels and water bodies into elevation data.
    /// </summary>
    /// <param name="elevationData">Elevation data to modify.</param>
    /// <param name="waterFeatures">Water features to carve.</param>
    /// <param name="carveDepth">Depth to carve in elevation units.</param>
    /// <returns>Modified elevation data with carved water features.</returns>
    ElevationData CarveWaterFeatures(ElevationData elevationData, HydrographyResult waterFeatures, float carveDepth);

    /// <summary>
    /// Applies water features to a terrain mesh.
    /// </summary>
    /// <param name="mesh">Terrain mesh to modify.</param>
    /// <param name="waterFeatures">Water features to apply.</param>
    /// <param name="options">Application options.</param>
    /// <returns>Modified mesh with water features.</returns>
    TerrainMesh ApplyToMesh(TerrainMesh mesh, HydrographyResult waterFeatures, WaterMeshOptions options);
}

/// <summary>
/// Options for hydrography analysis.
/// </summary>
public class HydrographyOptions
{
    /// <summary>
    /// Minimum flow accumulation threshold for river detection.
    /// Higher values = fewer, larger rivers.
    /// </summary>
    public int FlowAccumulationThreshold { get; set; } = 100;

    /// <summary>
    /// Minimum area for lake/pond detection in grid cells.
    /// </summary>
    public int MinLakeArea { get; set; } = 50;

    /// <summary>
    /// Elevation tolerance for flat area (lake) detection.
    /// </summary>
    public float FlatAreaTolerance { get; set; } = 0.5f;

    /// <summary>
    /// Whether to detect rivers using flow accumulation.
    /// </summary>
    public bool DetectRivers { get; set; } = true;

    /// <summary>
    /// Whether to detect lakes/ponds using flat area analysis.
    /// </summary>
    public bool DetectLakes { get; set; } = true;

    /// <summary>
    /// River width scaling factor.
    /// </summary>
    public float RiverWidthScale { get; set; } = 1.0f;
}

/// <summary>
/// Options for applying water features to a mesh.
/// </summary>
public class WaterMeshOptions
{
    /// <summary>
    /// Depth to carve water features in millimeters.
    /// </summary>
    public float CarveDepth { get; set; } = 1.0f;

    /// <summary>
    /// Width of rivers in millimeters.
    /// </summary>
    public float RiverWidth { get; set; } = 1.5f;

    /// <summary>
    /// Whether to create flat bottoms for water features.
    /// </summary>
    public bool FlattenWaterSurface { get; set; } = true;

    /// <summary>
    /// Whether to smooth river edges.
    /// </summary>
    public bool SmoothEdges { get; set; } = true;
}

/// <summary>
/// Result of hydrography analysis.
/// </summary>
public class HydrographyResult
{
    /// <summary>
    /// Identified rivers/streams.
    /// </summary>
    public List<WaterFeature> Rivers { get; set; } = new();

    /// <summary>
    /// Identified lakes and ponds.
    /// </summary>
    public List<WaterFeature> Lakes { get; set; } = new();

    /// <summary>
    /// Total count of water features.
    /// </summary>
    public int TotalFeatures => Rivers.Count + Lakes.Count;

    /// <summary>
    /// Whether any water features were found.
    /// </summary>
    public bool HasFeatures => TotalFeatures > 0;
}

/// <summary>
/// Represents a water feature (river, lake, etc.).
/// </summary>
public class WaterFeature
{
    /// <summary>
    /// Type of water feature.
    /// </summary>
    public WaterFeatureType Type { get; set; }

    /// <summary>
    /// Name of the feature (if available from data source).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Points defining the feature geometry.
    /// For rivers: centerline points.
    /// For lakes: polygon boundary points.
    /// </summary>
    public List<WaterPoint> Points { get; set; } = new();

    /// <summary>
    /// Flow accumulation value (for rivers).
    /// Higher values indicate larger rivers.
    /// </summary>
    public int FlowAccumulation { get; set; }

    /// <summary>
    /// Estimated width in grid cells (for rivers).
    /// </summary>
    public float EstimatedWidth { get; set; } = 1.0f;
}

/// <summary>
/// Types of water features.
/// </summary>
public enum WaterFeatureType
{
    River,
    Stream,
    Lake,
    Pond,
    Reservoir,
    Wetland
}

/// <summary>
/// A point in a water feature.
/// </summary>
public readonly struct WaterPoint
{
    public float X { get; }
    public float Y { get; }
    public float? Elevation { get; }

    public WaterPoint(float x, float y, float? elevation = null)
    {
        X = x;
        Y = y;
        Elevation = elevation;
    }
}

/// <summary>
/// Geographic bounds for filtering data.
/// </summary>
public class GeographicBounds
{
    public double MinLongitude { get; set; }
    public double MaxLongitude { get; set; }
    public double MinLatitude { get; set; }
    public double MaxLatitude { get; set; }

    public GeographicBounds() { }

    public GeographicBounds(double minLon, double minLat, double maxLon, double maxLat)
    {
        MinLongitude = minLon;
        MinLatitude = minLat;
        MaxLongitude = maxLon;
        MaxLatitude = maxLat;
    }

    public bool Contains(double longitude, double latitude)
    {
        return longitude >= MinLongitude && longitude <= MaxLongitude &&
               latitude >= MinLatitude && latitude <= MaxLatitude;
    }
}
