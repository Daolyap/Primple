using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Interfaces;

/// <summary>
/// Service for generating terrain meshes from elevation data.
/// </summary>
public interface IMeshGenerator
{
    /// <summary>
    /// Generates a terrain mesh from elevation data.
    /// </summary>
    /// <param name="elevationData">The source elevation data.</param>
    /// <param name="configuration">The map configuration settings.</param>
    /// <returns>The generated terrain mesh.</returns>
    TerrainMesh Generate(ElevationData elevationData, MapConfiguration configuration);

    /// <summary>
    /// Generates a terrain mesh asynchronously.
    /// </summary>
    Task<TerrainMesh> GenerateAsync(ElevationData elevationData, MapConfiguration configuration);
}
