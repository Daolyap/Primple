using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;
using TerrainMapGenerator.Core.Services;
using Xunit;

namespace TerrainMapGenerator.Tests;

public class ThreeMfExporterTests
{
    private readonly ThreeMfExporter _exporter = new();

    [Fact]
    public void ExportToBytes_WithValidMesh_Returns3MfData()
    {
        var mesh = CreateSimpleMesh();

        var bytes = _exporter.ExportToBytes(mesh);

        // 3MF is a ZIP file, should start with PK signature
        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public void ExportToBytes_ContainsRequiredEntries()
    {
        var mesh = CreateSimpleMesh();

        var bytes = _exporter.ExportToBytes(mesh);

        using var stream = new MemoryStream(bytes);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);

        var entryNames = archive.Entries.Select(e => e.FullName).ToList();
        
        Assert.Contains("[Content_Types].xml", entryNames);
        Assert.Contains("_rels/.rels", entryNames);
        Assert.Contains("3D/3dmodel.model", entryNames);
    }

    [Fact]
    public void Export_CreatesFile()
    {
        var mesh = CreateSimpleMesh();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.3mf");

        try
        {
            _exporter.Export(mesh, tempFile);

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
    public async Task ExportAsync_CreatesFile()
    {
        var mesh = CreateSimpleMesh();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.3mf");

        try
        {
            await _exporter.ExportAsync(mesh, tempFile);

            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ExportToBytes_WithColors_IncludesBaseMaterials()
    {
        var mesh = CreateSimpleMesh();
        var options = new ThreeMfExportOptions
        {
            Title = "Test Terrain",
            EnableColors = true
        };

        var bytes = _exporter.ExportToBytes(mesh, options);

        using var stream = new MemoryStream(bytes);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);

        var modelEntry = archive.GetEntry("3D/3dmodel.model");
        Assert.NotNull(modelEntry);

        using var modelStream = modelEntry!.Open();
        using var reader = new StreamReader(modelStream);
        var content = reader.ReadToEnd();

        Assert.Contains("basematerials", content);
        Assert.Contains("displaycolor", content);
    }

    [Fact]
    public void Export_WithEmptyMesh_ThrowsException()
    {
        var mesh = new TerrainMesh();

        Assert.Throws<InvalidOperationException>(() => _exporter.ExportToBytes(mesh));
    }

    [Fact]
    public void Export_WithNullMesh_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => _exporter.Export(null!, "test.3mf"));
    }

    [Fact]
    public void ExportToBytes_ContainsVertexData()
    {
        var mesh = CreateSimpleMesh();

        var bytes = _exporter.ExportToBytes(mesh);

        using var stream = new MemoryStream(bytes);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);

        var modelEntry = archive.GetEntry("3D/3dmodel.model");
        using var modelStream = modelEntry!.Open();
        using var reader = new StreamReader(modelStream);
        var content = reader.ReadToEnd();

        // Should contain vertex elements
        Assert.Contains("<vertex", content);
        Assert.Contains("<triangle", content);
    }

    [Fact]
    public void ExportToBytes_WithCustomTitle_IncludesMetadata()
    {
        var mesh = CreateSimpleMesh();
        var options = new ThreeMfExportOptions
        {
            Title = "Custom Mountain Terrain"
        };

        var bytes = _exporter.ExportToBytes(mesh, options);

        using var stream = new MemoryStream(bytes);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);

        var modelEntry = archive.GetEntry("3D/3dmodel.model");
        using var modelStream = modelEntry!.Open();
        using var reader = new StreamReader(modelStream);
        var content = reader.ReadToEnd();

        Assert.Contains("Custom Mountain Terrain", content);
    }

    private static TerrainMesh CreateSimpleMesh()
    {
        var mesh = new TerrainMesh();

        // Create a simple pyramid
        mesh.AddVertex(0, 0, 0);
        mesh.AddVertex(10, 0, 0);
        mesh.AddVertex(10, 10, 0);
        mesh.AddVertex(0, 10, 0);
        mesh.AddVertex(5, 5, 10);

        // Base
        mesh.AddTriangle(0, 2, 1);
        mesh.AddTriangle(0, 3, 2);

        // Sides
        mesh.AddTriangle(0, 1, 4);
        mesh.AddTriangle(1, 2, 4);
        mesh.AddTriangle(2, 3, 4);
        mesh.AddTriangle(3, 0, 4);

        mesh.CalculateNormals();
        mesh.UpdateBounds();

        return mesh;
    }
}
