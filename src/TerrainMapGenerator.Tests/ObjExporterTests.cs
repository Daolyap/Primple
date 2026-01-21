using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;
using TerrainMapGenerator.Core.Services;
using Xunit;

namespace TerrainMapGenerator.Tests;

public class ObjExporterTests
{
    private readonly ObjExporter _exporter = new();

    [Fact]
    public void ExportToString_WithValidMesh_ReturnsObjContent()
    {
        var mesh = CreateSimpleMesh();

        var content = _exporter.ExportToString(mesh);

        Assert.NotNull(content);
        Assert.Contains("# Terrain Map Generator", content);
        Assert.Contains("v ", content);
        Assert.Contains("f ", content);
    }

    [Fact]
    public void ExportToString_ContainsAllVertices()
    {
        var mesh = CreateSimpleMesh();

        var content = _exporter.ExportToString(mesh);

        var vertexCount = content.Split('\n').Count(line => line.StartsWith("v "));
        Assert.Equal(mesh.VertexCount, vertexCount);
    }

    [Fact]
    public void ExportToString_ContainsAllFaces()
    {
        var mesh = CreateSimpleMesh();

        var content = _exporter.ExportToString(mesh);

        var faceCount = content.Split('\n').Count(line => line.StartsWith("f "));
        Assert.Equal(mesh.TriangleCount, faceCount);
    }

    [Fact]
    public void Export_CreatesObjFile()
    {
        var mesh = CreateSimpleMesh();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.obj");
        var mtlFile = Path.ChangeExtension(tempFile, ".mtl");

        try
        {
            _exporter.Export(mesh, tempFile);

            Assert.True(File.Exists(tempFile));
            Assert.True(File.Exists(mtlFile));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(mtlFile)) File.Delete(mtlFile);
        }
    }

    [Fact]
    public void Export_WithoutMtl_DoesNotCreateMtlFile()
    {
        var mesh = CreateSimpleMesh();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.obj");
        var mtlFile = Path.ChangeExtension(tempFile, ".mtl");
        var options = new ObjExportOptions { GenerateMtl = false };

        try
        {
            _exporter.Export(mesh, tempFile, options);

            Assert.True(File.Exists(tempFile));
            Assert.False(File.Exists(mtlFile));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(mtlFile)) File.Delete(mtlFile);
        }
    }

    [Fact]
    public void Export_WithNormals_IncludesNormalData()
    {
        var mesh = CreateSimpleMesh();
        var options = new ObjExportOptions { IncludeNormals = true };

        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.obj");

        try
        {
            _exporter.Export(mesh, tempFile, options);

            var content = File.ReadAllText(tempFile);
            Assert.Contains("vn ", content);
            Assert.Contains("//", content); // Face format v//vn
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            var mtlFile = Path.ChangeExtension(tempFile, ".mtl");
            if (File.Exists(mtlFile)) File.Delete(mtlFile);
        }
    }

    [Fact]
    public void GenerateMtlContent_ReturnsValidMtl()
    {
        var options = new ObjExportOptions
        {
            MaterialName = "test_material",
            Material = new ObjMaterial
            {
                DiffuseColor = (0.5f, 0.5f, 0.5f),
                SpecularExponent = 20f
            }
        };

        var mtlContent = _exporter.GenerateMtlContent(options);

        Assert.Contains("newmtl test_material", mtlContent);
        Assert.Contains("Ka ", mtlContent);
        Assert.Contains("Kd ", mtlContent);
        Assert.Contains("Ks ", mtlContent);
        Assert.Contains("Ns ", mtlContent);
    }

    [Fact]
    public async Task ExportAsync_CreatesFile()
    {
        var mesh = CreateSimpleMesh();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.obj");

        try
        {
            await _exporter.ExportAsync(mesh, tempFile);

            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            var mtlFile = Path.ChangeExtension(tempFile, ".mtl");
            if (File.Exists(mtlFile)) File.Delete(mtlFile);
        }
    }

    [Fact]
    public void Export_WithEmptyMesh_ThrowsException()
    {
        var mesh = new TerrainMesh();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.obj");

        Assert.Throws<InvalidOperationException>(() => _exporter.Export(mesh, tempFile));
    }

    [Fact]
    public void Export_WithTextureCoords_IncludesUV()
    {
        var mesh = CreateSimpleMesh();
        var options = new ObjExportOptions { IncludeTextureCoords = true };

        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.obj");

        try
        {
            _exporter.Export(mesh, tempFile, options);

            var content = File.ReadAllText(tempFile);
            Assert.Contains("vt ", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            var mtlFile = Path.ChangeExtension(tempFile, ".mtl");
            if (File.Exists(mtlFile)) File.Delete(mtlFile);
        }
    }

    [Fact]
    public void Export_ObjIndicesAreOneBased()
    {
        var mesh = CreateSimpleMesh();

        var content = _exporter.ExportToString(mesh);

        // OBJ uses 1-based indices
        var faceLines = content.Split('\n').Where(l => l.StartsWith("f ")).ToList();
        foreach (var line in faceLines)
        {
            var indices = line.Replace("f ", "")
                .Split(' ')
                .Select(s => int.Parse(s.Split('/')[0]))
                .ToList();
            
            Assert.All(indices, i => Assert.True(i >= 1));
        }
    }

    private static TerrainMesh CreateSimpleMesh()
    {
        var mesh = new TerrainMesh();

        // Create a simple box-like mesh
        mesh.AddVertex(0, 0, 10);
        mesh.AddVertex(100, 0, 10);
        mesh.AddVertex(100, 100, 10);
        mesh.AddVertex(0, 100, 10);
        mesh.AddVertex(0, 0, 0);
        mesh.AddVertex(100, 0, 0);
        mesh.AddVertex(100, 100, 0);
        mesh.AddVertex(0, 100, 0);

        // Top
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(0, 2, 3);

        // Bottom
        mesh.AddTriangle(4, 6, 5);
        mesh.AddTriangle(4, 7, 6);

        // Sides
        mesh.AddTriangle(0, 5, 1);
        mesh.AddTriangle(0, 4, 5);

        mesh.CalculateNormals();
        mesh.UpdateBounds();

        return mesh;
    }
}
