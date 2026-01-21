using System.Text;
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Services;

/// <summary>
/// Service for exporting meshes to STL format.
/// </summary>
public class StlExporter : IStlExporter
{
    private const string StlHeader = "Terrain Map Generator - Binary STL";

    public async Task ExportBinaryAsync(TerrainMesh mesh, string filePath)
    {
        await Task.Run(() => ExportBinary(mesh, filePath));
    }

    public void ExportBinary(TerrainMesh mesh, string filePath)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(filePath);

        if (mesh.TriangleCount == 0)
            throw new InvalidOperationException("Cannot export empty mesh.");

        // Ensure normals are calculated
        if (mesh.Normals.Count != mesh.TriangleCount)
        {
            mesh.CalculateNormals();
        }

        var bytes = ExportToBytes(mesh);
        File.WriteAllBytes(filePath, bytes);
    }

    public byte[] ExportToBytes(TerrainMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.TriangleCount == 0)
            throw new InvalidOperationException("Cannot export empty mesh.");

        // Ensure normals are calculated
        if (mesh.Normals.Count != mesh.TriangleCount)
        {
            mesh.CalculateNormals();
        }

        // Binary STL format:
        // 80 bytes header
        // 4 bytes: number of triangles (uint32)
        // For each triangle:
        //   3x4 bytes: normal (3 floats)
        //   3x3x4 bytes: vertices (3 vertices, each 3 floats)
        //   2 bytes: attribute byte count (usually 0)

        int triangleCount = mesh.TriangleCount;
        int fileSize = 80 + 4 + (triangleCount * 50);

        using var ms = new MemoryStream(fileSize);
        using var writer = new BinaryWriter(ms);

        // Write header (80 bytes)
        var header = new byte[80];
        var headerText = Encoding.ASCII.GetBytes(StlHeader);
        Array.Copy(headerText, header, Math.Min(headerText.Length, 80));
        writer.Write(header);

        // Write triangle count
        writer.Write((uint)triangleCount);

        // Write triangles
        var vertices = mesh.Vertices;
        var triangles = mesh.Triangles;
        var normals = mesh.Normals;

        for (int i = 0; i < triangleCount; i++)
        {
            var tri = triangles[i];
            var normal = normals[i];

            // Write normal
            writer.Write(normal.X);
            writer.Write(normal.Y);
            writer.Write(normal.Z);

            // Write vertices
            var v0 = vertices[tri.V0];
            var v1 = vertices[tri.V1];
            var v2 = vertices[tri.V2];

            writer.Write(v0.X);
            writer.Write(v0.Y);
            writer.Write(v0.Z);

            writer.Write(v1.X);
            writer.Write(v1.Y);
            writer.Write(v1.Z);

            writer.Write(v2.X);
            writer.Write(v2.Y);
            writer.Write(v2.Z);

            // Attribute byte count (unused)
            writer.Write((ushort)0);
        }

        return ms.ToArray();
    }

    public async Task ExportAsciiAsync(TerrainMesh mesh, string filePath)
    {
        await Task.Run(() => ExportAscii(mesh, filePath));
    }

    public void ExportAscii(TerrainMesh mesh, string filePath)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(filePath);

        if (mesh.TriangleCount == 0)
            throw new InvalidOperationException("Cannot export empty mesh.");

        // Ensure normals are calculated
        if (mesh.Normals.Count != mesh.TriangleCount)
        {
            mesh.CalculateNormals();
        }

        using var writer = new StreamWriter(filePath, false, Encoding.ASCII);

        writer.WriteLine("solid terrain");

        var vertices = mesh.Vertices;
        var triangles = mesh.Triangles;
        var normals = mesh.Normals;

        for (int i = 0; i < mesh.TriangleCount; i++)
        {
            var tri = triangles[i];
            var normal = normals[i];

            writer.WriteLine($"  facet normal {FormatFloat(normal.X)} {FormatFloat(normal.Y)} {FormatFloat(normal.Z)}");
            writer.WriteLine("    outer loop");

            var v0 = vertices[tri.V0];
            var v1 = vertices[tri.V1];
            var v2 = vertices[tri.V2];

            writer.WriteLine($"      vertex {FormatFloat(v0.X)} {FormatFloat(v0.Y)} {FormatFloat(v0.Z)}");
            writer.WriteLine($"      vertex {FormatFloat(v1.X)} {FormatFloat(v1.Y)} {FormatFloat(v1.Z)}");
            writer.WriteLine($"      vertex {FormatFloat(v2.X)} {FormatFloat(v2.Y)} {FormatFloat(v2.Z)}");

            writer.WriteLine("    endloop");
            writer.WriteLine("  endfacet");
        }

        writer.WriteLine("endsolid terrain");
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("E6", System.Globalization.CultureInfo.InvariantCulture);
    }
}
