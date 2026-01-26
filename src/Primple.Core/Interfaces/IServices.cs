using Primple.Core.Models;

namespace Primple.Core.Interfaces;

/// <summary>
/// Service for fetching elevation data from various sources.
/// </summary>
public interface IElevationDataService
{
    /// <summary>
    /// Fetches elevation data for the specified geographic bounds.
    /// </summary>
    Task<ElevationData> GetElevationDataAsync(GeographicBounds bounds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads elevation data from a local file.
    /// </summary>
    Task<ElevationData> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if data is available for the specified region.
    /// </summary>
    Task<bool> IsDataAvailableAsync(GeographicBounds bounds);
}

/// <summary>
/// Service for generating 3D meshes from elevation data.
/// </summary>
public interface IMeshGeneratorService
{
    /// <summary>
    /// Generates a terrain mesh from elevation data and settings.
    /// </summary>
    TerrainMesh GenerateMesh(ElevationData elevationData, TerrainSettings settings, IProgress<double>? progress = null);

    /// <summary>
    /// Applies smoothing to elevation data.
    /// </summary>
    ElevationData ApplySmoothing(ElevationData data, TerrainSettings settings);

    /// <summary>
    /// Optimizes mesh for printing (reduces polygon count while preserving features).
    /// </summary>
    TerrainMesh OptimizeMesh(TerrainMesh mesh, int targetTriangles);
}

/// <summary>
/// Service for exporting meshes to various file formats.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports mesh to STL format.
    /// </summary>
    Task ExportStlAsync(TerrainMesh mesh, string filePath, bool binary = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports mesh to 3MF format with color data.
    /// </summary>
    Task Export3MFAsync(TerrainMesh mesh, string filePath, TerrainProject project, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports mesh to OBJ format.
    /// </summary>
    Task ExportObjAsync(TerrainMesh mesh, string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for geocoding and location search.
/// </summary>
public interface IGeocodingService
{
    /// <summary>
    /// Search for locations by name or address.
    /// </summary>
    Task<IEnumerable<LocationResult>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverse geocode coordinates to get place name.
    /// </summary>
    Task<string?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing terrain projects.
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// Creates a new terrain project.
    /// </summary>
    TerrainProject CreateProject(string name);

    /// <summary>
    /// Saves project to file.
    /// </summary>
    Task SaveProjectAsync(TerrainProject project, string filePath);

    /// <summary>
    /// Loads project from file.
    /// </summary>
    Task<TerrainProject> LoadProjectAsync(string filePath);

    /// <summary>
    /// Gets list of recent projects.
    /// </summary>
    IEnumerable<RecentProject> GetRecentProjects();
}

/// <summary>
/// Location search result.
/// </summary>
public class LocationResult
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public GeographicBounds? Bounds { get; set; }
    public string Type { get; set; } = "";
}

/// <summary>
/// Recent project information.
/// </summary>
public class RecentProject
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime LastOpened { get; set; }
}
