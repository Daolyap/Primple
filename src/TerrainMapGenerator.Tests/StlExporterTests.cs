using TerrainMapGenerator.Core.Models;
using TerrainMapGenerator.Core.Services;
using Xunit;

namespace TerrainMapGenerator.Tests;

public class StlExporterTests
{
    private readonly StlExporter _exporter = new();

    [Fact]
    public void ExportToBytes_WithValidMesh_ReturnsBinaryStl()
    {
        var mesh = CreateSimpleMesh();

        var bytes = _exporter.ExportToBytes(mesh);

        // Binary STL header is 80 bytes + 4 bytes for triangle count
        Assert.True(bytes.Length >= 84);

        // Check triangle count in header
        var triangleCount = BitConverter.ToUInt32(bytes, 80);
        Assert.Equal((uint)mesh.TriangleCount, triangleCount);
    }

    [Fact]
    public void ExportToBytes_CorrectFileSize()
    {
        var mesh = CreateSimpleMesh();

        var bytes = _exporter.ExportToBytes(mesh);

        // Expected size: 80 (header) + 4 (count) + triangles * 50 bytes each
        int expectedSize = 80 + 4 + mesh.TriangleCount * 50;
        Assert.Equal(expectedSize, bytes.Length);
    }

    [Fact]
    public void ExportBinary_CreatesFile()
    {
        var mesh = CreateSimpleMesh();
        var tempFile = Path.GetTempFileName();

        try
        {
            _exporter.ExportBinary(mesh, tempFile);

            Assert.True(File.Exists(tempFile));
            var fileInfo = new FileInfo(tempFile);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ExportAscii_CreatesValidFile()
    {
        var mesh = CreateSimpleMesh();
        var tempFile = Path.GetTempFileName();

        try
        {
            _exporter.ExportAscii(mesh, tempFile);

            Assert.True(File.Exists(tempFile));

            var content = File.ReadAllText(tempFile);
            Assert.StartsWith("solid terrain", content);
            Assert.Contains("endsolid terrain", content);
            Assert.Contains("facet normal", content);
            Assert.Contains("vertex", content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ExportAscii_ContainsAllTriangles()
    {
        var mesh = CreateSimpleMesh();
        var tempFile = Path.GetTempFileName();

        try
        {
            _exporter.ExportAscii(mesh, tempFile);

            var content = File.ReadAllText(tempFile);
            var facetCount = content.Split("facet normal").Length - 1;

            Assert.Equal(mesh.TriangleCount, facetCount);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Export_WithEmptyMesh_ThrowsException()
    {
        var mesh = new TerrainMesh();

        Assert.Throws<InvalidOperationException>(() => _exporter.ExportToBytes(mesh));
    }

    [Fact]
    public async Task ExportBinaryAsync_WorksCorrectly()
    {
        var mesh = CreateSimpleMesh();
        var tempFile = Path.GetTempFileName();

        try
        {
            await _exporter.ExportBinaryAsync(mesh, tempFile);

            Assert.True(File.Exists(tempFile));
            var bytes = await File.ReadAllBytesAsync(tempFile);
            Assert.True(bytes.Length > 84);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExportAsciiAsync_WorksCorrectly()
    {
        var mesh = CreateSimpleMesh();
        var tempFile = Path.GetTempFileName();

        try
        {
            await _exporter.ExportAsciiAsync(mesh, tempFile);

            Assert.True(File.Exists(tempFile));
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.StartsWith("solid terrain", content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ExportBinary_PreservesTriangleData()
    {
        var mesh = CreateSimpleMesh();
        var bytes = _exporter.ExportToBytes(mesh);

        // Read back the first triangle
        // After header (80) and count (4), first triangle starts at 84
        // Normal: 3 floats (12 bytes)
        // Then 3 vertices, each 3 floats (36 bytes)
        // Then attribute count (2 bytes)

        var normal = new float[3];
        var v0 = new float[3];
        var v1 = new float[3];
        var v2 = new float[3];

        int offset = 84;
        for (int i = 0; i < 3; i++)
            normal[i] = BitConverter.ToSingle(bytes, offset + i * 4);

        offset += 12;
        for (int i = 0; i < 3; i++)
            v0[i] = BitConverter.ToSingle(bytes, offset + i * 4);

        offset += 12;
        for (int i = 0; i < 3; i++)
            v1[i] = BitConverter.ToSingle(bytes, offset + i * 4);

        offset += 12;
        for (int i = 0; i < 3; i++)
            v2[i] = BitConverter.ToSingle(bytes, offset + i * 4);

        // Verify vertices match original mesh
        var tri = mesh.Triangles[0];
        var origV0 = mesh.Vertices[tri.V0];
        var origV1 = mesh.Vertices[tri.V1];
        var origV2 = mesh.Vertices[tri.V2];

        Assert.Equal(origV0.X, v0[0], 0.001f);
        Assert.Equal(origV0.Y, v0[1], 0.001f);
        Assert.Equal(origV0.Z, v0[2], 0.001f);

        Assert.Equal(origV1.X, v1[0], 0.001f);
        Assert.Equal(origV1.Y, v1[1], 0.001f);
        Assert.Equal(origV1.Z, v1[2], 0.001f);

        Assert.Equal(origV2.X, v2[0], 0.001f);
        Assert.Equal(origV2.Y, v2[1], 0.001f);
        Assert.Equal(origV2.Z, v2[2], 0.001f);
    }

    private static TerrainMesh CreateSimpleMesh()
    {
        var mesh = new TerrainMesh();

        // Create a simple box-like mesh
        // Top face
        mesh.AddVertex(0, 0, 10);  // 0
        mesh.AddVertex(100, 0, 10); // 1
        mesh.AddVertex(100, 100, 10); // 2
        mesh.AddVertex(0, 100, 10); // 3

        // Bottom face
        mesh.AddVertex(0, 0, 0);  // 4
        mesh.AddVertex(100, 0, 0); // 5
        mesh.AddVertex(100, 100, 0); // 6
        mesh.AddVertex(0, 100, 0); // 7

        // Top triangles
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(0, 2, 3);

        // Bottom triangles (reversed winding)
        mesh.AddTriangle(4, 6, 5);
        mesh.AddTriangle(4, 7, 6);

        // Side triangles
        // Front
        mesh.AddTriangle(0, 5, 1);
        mesh.AddTriangle(0, 4, 5);

        // Back
        mesh.AddTriangle(2, 7, 3);
        mesh.AddTriangle(2, 6, 7);

        // Left
        mesh.AddTriangle(0, 7, 4);
        mesh.AddTriangle(0, 3, 7);

        // Right
        mesh.AddTriangle(1, 5, 6);
        mesh.AddTriangle(1, 6, 2);

        mesh.CalculateNormals();

        return mesh;
    }
}
