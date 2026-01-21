using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Interfaces;

/// <summary>
/// Service for loading elevation data from various sources.
/// </summary>
public interface IElevationDataService
{
    /// <summary>
    /// Loads elevation data from a file.
    /// </summary>
    /// <param name="filePath">Path to the elevation data file.</param>
    /// <returns>The loaded elevation data.</returns>
    Task<ElevationData> LoadFromFileAsync(string filePath);

    /// <summary>
    /// Loads elevation data from a file synchronously.
    /// </summary>
    /// <param name="filePath">Path to the elevation data file.</param>
    /// <returns>The loaded elevation data.</returns>
    ElevationData LoadFromFile(string filePath);

    /// <summary>
    /// Checks if the file format is supported.
    /// </summary>
    /// <param name="filePath">Path to check.</param>
    /// <returns>True if the format is supported.</returns>
    bool IsFormatSupported(string filePath);

    /// <summary>
    /// Gets the supported file extensions.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }
}
