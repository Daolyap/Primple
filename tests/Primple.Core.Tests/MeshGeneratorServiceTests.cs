using Primple.Core.Enums;
using Primple.Core.Models;
using Primple.Core.Services;
using Xunit;

namespace Primple.Core.Tests;

public class MeshGeneratorServiceTests
{
    private readonly MeshGeneratorService _service;

    public MeshGeneratorServiceTests()
    {
        _service = new MeshGeneratorService();
    }

    [Fact]
    public void GenerateMesh_WithValidData_ReturnsMesh()
    {
        // Arrange
        var elevationData = CreateTestElevationData(10, 10);
        var settings = new TerrainSettings
        {
            VerticalExaggeration = 2.0,
            SizePreset = MapSizePreset.Small,
            BaseThickness = 3.0,
            MeshResolution = 10
        };

        // Act
        var mesh = _service.GenerateMesh(elevationData, settings);

        // Assert
        Assert.NotNull(mesh);
        Assert.True(mesh.VertexCount > 0);
        Assert.True(mesh.TriangleCount > 0);
        Assert.Equal(0, mesh.Indices.Count % 3);
    }

    [Fact]
    public void GenerateMesh_HasWatertightGeometry()
    {
        // Arrange
        var elevationData = CreateTestElevationData(20, 20);
        var settings = new TerrainSettings
        {
            VerticalExaggeration = 1.5,
            SizePreset = MapSizePreset.Medium,
            MeshResolution = 20
        };

        // Act
        var mesh = _service.GenerateMesh(elevationData, settings);

        // Assert - Check mesh is valid (watertight)
        var validation = mesh.Validate();
        Assert.True(validation.IsValid, $"Mesh validation failed: {string.Join(", ", validation.Errors)}");
    }

    [Fact]
    public void GenerateMesh_RespectsMaxHeight()
    {
        // Arrange
        var elevationData = CreateTestElevationData(10, 10);
        var maxHeight = 25.0;
        var settings = new TerrainSettings
        {
            VerticalExaggeration = 10.0, // High exaggeration
            SizePreset = MapSizePreset.Medium,
            MaxHeight = maxHeight,
            BaseThickness = 3.0,
            MeshResolution = 10
        };

        // Act
        var mesh = _service.GenerateMesh(elevationData, settings);

        // Assert
        var (_, max) = mesh.GetBounds();
        Assert.True(max.Z <= maxHeight, $"Mesh height {max.Z} exceeds max height {maxHeight}");
    }

    [Fact]
    public void ApplySmoothing_Gaussian_ReducesNoise()
    {
        // Arrange
        var elevationData = CreateNoisyElevationData(10, 10);
        var settings = new TerrainSettings
        {
            SmoothingMethod = SmoothingMethod.Gaussian,
            SmoothingRadius = 2.0
        };

        // Act
        var smoothed = _service.ApplySmoothing(elevationData, settings);

        // Assert - Smoothed data should have less variance
        var originalVariance = CalculateVariance(elevationData.Data);
        var smoothedVariance = CalculateVariance(smoothed.Data);
        Assert.True(smoothedVariance <= originalVariance, "Smoothing should reduce variance");
    }

    [Fact]
    public void ApplySmoothing_Median_PreservesRange()
    {
        // Arrange
        var elevationData = CreateTestElevationData(10, 10);
        var settings = new TerrainSettings
        {
            SmoothingMethod = SmoothingMethod.Median,
            SmoothingRadius = 1.0
        };

        // Act
        var smoothed = _service.ApplySmoothing(elevationData, settings);

        // Assert - Min/max should be within original range
        Assert.True(smoothed.MinElevation >= elevationData.MinElevation - 1);
        Assert.True(smoothed.MaxElevation <= elevationData.MaxElevation + 1);
    }

    [Theory]
    [InlineData(MapSizePreset.Small, 100)]
    [InlineData(MapSizePreset.Medium, 150)]
    [InlineData(MapSizePreset.Large, 200)]
    [InlineData(MapSizePreset.BambuMax, 256)]
    public void GenerateMesh_RespectsSize(MapSizePreset preset, double expectedSize)
    {
        // Arrange
        var elevationData = CreateTestElevationData(10, 10);
        var settings = new TerrainSettings
        {
            SizePreset = preset,
            MeshResolution = 10,
            MaintainAspectRatio = false
        };

        // Act
        var mesh = _service.GenerateMesh(elevationData, settings);

        // Assert
        var dims = mesh.GetDimensions();
        Assert.True(Math.Abs(dims.X - expectedSize) < 1, $"Width {dims.X} should be close to {expectedSize}");
        Assert.True(Math.Abs(dims.Y - expectedSize) < 1, $"Length {dims.Y} should be close to {expectedSize}");
    }

    private ElevationData CreateTestElevationData(int width, int height)
    {
        var data = new ElevationData
        {
            Data = new double[height, width],
            Bounds = new GeographicBounds(47.7, 47.6, -122.3, -122.4),
            Resolution = 30
        };

        // Create a simple hill pattern
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var nx = (double)x / width - 0.5;
                var ny = (double)y / height - 0.5;
                data.Data[y, x] = 500 + 200 * Math.Exp(-(nx * nx + ny * ny) * 10);
            }
        }

        data.CalculateStatistics();
        return data;
    }

    private ElevationData CreateNoisyElevationData(int width, int height)
    {
        var random = new Random(42);
        var data = new ElevationData
        {
            Data = new double[height, width],
            Bounds = new GeographicBounds(47.7, 47.6, -122.3, -122.4),
            Resolution = 30
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                data.Data[y, x] = 500 + random.NextDouble() * 100;
            }
        }

        data.CalculateStatistics();
        return data;
    }

    private double CalculateVariance(double[,] data)
    {
        var values = new List<double>();
        for (int y = 0; y < data.GetLength(0); y++)
        {
            for (int x = 0; x < data.GetLength(1); x++)
            {
                values.Add(data[y, x]);
            }
        }
        
        var mean = values.Average();
        return values.Select(v => (v - mean) * (v - mean)).Average();
    }
}
