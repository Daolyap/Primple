using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Interfaces;

/// <summary>
/// Service for exporting meshes to STL format.
/// </summary>
public interface IStlExporter
{
    /// <summary>
    /// Exports a mesh to a binary STL file.
    /// </summary>
    /// <param name="mesh">The mesh to export.</param>
    /// <param name="filePath">The output file path.</param>
    void ExportBinary(TerrainMesh mesh, string filePath);

    /// <summary>
    /// Exports a mesh to a binary STL file asynchronously.
    /// </summary>
    Task ExportBinaryAsync(TerrainMesh mesh, string filePath);

    /// <summary>
    /// Exports a mesh to an ASCII STL file.
    /// </summary>
    /// <param name="mesh">The mesh to export.</param>
    /// <param name="filePath">The output file path.</param>
    void ExportAscii(TerrainMesh mesh, string filePath);

    /// <summary>
    /// Exports a mesh to an ASCII STL file asynchronously.
    /// </summary>
    Task ExportAsciiAsync(TerrainMesh mesh, string filePath);

    /// <summary>
    /// Exports a mesh to a byte array in binary STL format.
    /// </summary>
    byte[] ExportToBytes(TerrainMesh mesh);
}
