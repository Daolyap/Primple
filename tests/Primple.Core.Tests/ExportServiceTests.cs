using Primple.Core.Models;
using Primple.Core.Services;
using Xunit;

namespace Primple.Core.Tests;

public class ExportServiceTests
{
    private readonly ExportService _service;
    private readonly string _testOutputDir;

    public ExportServiceTests()
    {
        _service = new ExportService();
        _testOutputDir = Path.Combine(Path.GetTempPath(), "PrimpleTests");
        Directory.CreateDirectory(_testOutputDir);
    }

    [Fact]
    public async Task ExportStl_Binary_CreatesValidFile()
    {
        // Arrange
        var mesh = CreateTestMesh();
        var filePath = Path.Combine(_testOutputDir, $"test_{Guid.NewGuid()}.stl");

        try
        {
            // Act
            await _service.ExportStlAsync(mesh, filePath, binary: true);

            // Assert
            Assert.True(File.Exists(filePath));
            var fileInfo = new FileInfo(filePath);
            Assert.True(fileInfo.Length > 0);
            
            // Check STL header
            var bytes = await File.ReadAllBytesAsync(filePath);
            Assert.True(bytes.Length >= 84, "Binary STL should have at least 84 bytes header");
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ExportStl_Ascii_CreatesValidFile()
    {
        // Arrange
        var mesh = CreateTestMesh();
        var filePath = Path.Combine(_testOutputDir, $"test_{Guid.NewGuid()}.stl");

        try
        {
            // Act
            await _service.ExportStlAsync(mesh, filePath, binary: false);

            // Assert
            Assert.True(File.Exists(filePath));
            var content = await File.ReadAllTextAsync(filePath);
            Assert.Contains("solid primple_terrain", content);
            Assert.Contains("endsolid primple_terrain", content);
            Assert.Contains("facet normal", content);
            Assert.Contains("vertex", content);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ExportObj_CreatesValidFile()
    {
        // Arrange
        var mesh = CreateTestMesh();
        var filePath = Path.Combine(_testOutputDir, $"test_{Guid.NewGuid()}.obj");

        try
        {
            // Act
            await _service.ExportObjAsync(mesh, filePath);

            // Assert
            Assert.True(File.Exists(filePath));
            var content = await File.ReadAllTextAsync(filePath);
            Assert.Contains("# Primple Terrain Generator", content);
            Assert.Contains("v ", content); // Vertices
            Assert.Contains("f ", content); // Faces
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Export3MF_CreatesValidZipArchive()
    {
        // Arrange
        var mesh = CreateTestMesh();
        var project = new TerrainProject
        {
            Name = "Test Project",
            Description = "Test Description"
        };
        var filePath = Path.Combine(_testOutputDir, $"test_{Guid.NewGuid()}.3mf");

        try
        {
            // Act
            await _service.Export3MFAsync(mesh, filePath, project);

            // Assert
            Assert.True(File.Exists(filePath));
            
            // Verify it's a valid ZIP archive
            using var archive = System.IO.Compression.ZipFile.OpenRead(filePath);
            var modelEntry = archive.GetEntry("3D/3dmodel.model");
            Assert.NotNull(modelEntry);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void TerrainMesh_EstimateStlFileSize_ReturnsReasonableValue()
    {
        // Arrange
        var mesh = CreateTestMesh();

        // Act
        var binarySize = mesh.EstimateStlFileSize(binary: true);
        var asciiSize = mesh.EstimateStlFileSize(binary: false);

        // Assert
        Assert.True(binarySize > 0);
        Assert.True(asciiSize > binarySize, "ASCII STL should be larger than binary");
        
        // Binary: 84 + triangles * 50
        var expectedBinarySize = 84 + mesh.TriangleCount * 50;
        Assert.Equal(expectedBinarySize, binarySize);
    }

    private TerrainMesh CreateTestMesh()
    {
        var mesh = new TerrainMesh();
        
        // Create a simple triangle
        mesh.Vertices.Add(new System.Numerics.Vector3(0, 0, 0));
        mesh.Vertices.Add(new System.Numerics.Vector3(100, 0, 0));
        mesh.Vertices.Add(new System.Numerics.Vector3(50, 100, 0));
        mesh.Vertices.Add(new System.Numerics.Vector3(50, 50, 50));
        
        // Create a tetrahedron (4 triangles)
        mesh.Indices.AddRange(new[] { 0, 1, 2 }); // Base
        mesh.Indices.AddRange(new[] { 0, 1, 3 }); // Side 1
        mesh.Indices.AddRange(new[] { 1, 2, 3 }); // Side 2
        mesh.Indices.AddRange(new[] { 2, 0, 3 }); // Side 3

        mesh.CalculateNormals();
        
        return mesh;
    }
}
