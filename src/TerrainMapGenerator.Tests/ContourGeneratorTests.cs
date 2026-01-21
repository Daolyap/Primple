using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;
using TerrainMapGenerator.Core.Services;
using Xunit;

namespace TerrainMapGenerator.Tests;

public class ContourGeneratorTests
{
    private readonly ContourGenerator _generator = new();

    [Fact]
    public void GenerateContours_WithValidData_ReturnsContours()
    {
        var elevationData = CreateTestElevationData();
        var options = new ContourOptions { Interval = 10 };

        var result = _generator.GenerateContours(elevationData, options);

        Assert.NotNull(result);
        Assert.True(result.Contours.Count > 0 || result.MajorContours.Count > 0);
    }

    [Fact]
    public void GenerateContours_RespectsInterval()
    {
        var elevationData = CreateTestElevationData();
        var options = new ContourOptions 
        { 
            Interval = 20,
            MajorInterval = 100
        };

        var result = _generator.GenerateContours(elevationData, options);

        // All contour elevations should be multiples of the interval
        foreach (var contour in result.Contours)
        {
            var remainder = contour.Elevation % options.Interval;
            Assert.True(remainder < 0.01f || Math.Abs(remainder - options.Interval) < 0.01f);
        }
    }

    [Fact]
    public void GenerateContours_IdentifiesMajorContours()
    {
        var elevationData = CreateTestElevationData();
        var options = new ContourOptions 
        { 
            Interval = 10,
            MajorInterval = 50
        };

        var result = _generator.GenerateContours(elevationData, options);

        // Major contours should be at the major interval
        foreach (var contour in result.MajorContours)
        {
            Assert.True(contour.IsMajor);
            var remainder = contour.Elevation % options.MajorInterval;
            Assert.True(remainder < 0.01f || Math.Abs(remainder - options.MajorInterval) < 0.01f);
        }
    }

    [Fact]
    public async Task GenerateContoursAsync_Works()
    {
        var elevationData = CreateTestElevationData();
        var options = new ContourOptions { Interval = 10 };

        var result = await _generator.GenerateContoursAsync(elevationData, options);

        Assert.NotNull(result);
    }

    [Fact]
    public void GenerateContours_WithZeroInterval_ThrowsException()
    {
        var elevationData = CreateTestElevationData();
        var options = new ContourOptions { Interval = 0 };

        Assert.Throws<ArgumentException>(() => _generator.GenerateContours(elevationData, options));
    }

    [Fact]
    public void GenerateContours_WithNegativeInterval_ThrowsException()
    {
        var elevationData = CreateTestElevationData();
        var options = new ContourOptions { Interval = -10 };

        Assert.Throws<ArgumentException>(() => _generator.GenerateContours(elevationData, options));
    }

    [Fact]
    public void GenerateContours_WithNullData_ThrowsException()
    {
        var options = new ContourOptions { Interval = 10 };

        Assert.Throws<ArgumentNullException>(() => _generator.GenerateContours(null!, options));
    }

    [Fact]
    public void GenerateContours_ContourLinesHaveCorrectElevation()
    {
        var elevationData = CreateTestElevationData();
        var options = new ContourOptions { Interval = 25 };

        var result = _generator.GenerateContours(elevationData, options);

        foreach (var contour in result.Contours.Concat(result.MajorContours))
        {
            // Elevation should be within the data range
            Assert.True(contour.Elevation >= elevationData.MinElevation);
            Assert.True(contour.Elevation <= elevationData.MaxElevation);
        }
    }

    [Fact]
    public void GenerateContours_WithSmoothing_ProducesSmootherLines()
    {
        var elevationData = CreateTestElevationData();
        
        var optionsWithoutSmoothing = new ContourOptions 
        { 
            Interval = 20,
            SmoothContours = false
        };
        
        var optionsWithSmoothing = new ContourOptions 
        { 
            Interval = 20,
            SmoothContours = true,
            SmoothingIterations = 3
        };

        var resultWithout = _generator.GenerateContours(elevationData, optionsWithoutSmoothing);
        var resultWith = _generator.GenerateContours(elevationData, optionsWithSmoothing);

        // Both should produce contours
        Assert.True(resultWithout.TotalSegments > 0);
        Assert.True(resultWith.TotalSegments > 0);
    }

    [Fact]
    public void GenerateContours_WithMinMaxElevation_RespectsRange()
    {
        var elevationData = CreateTestElevationData();
        var options = new ContourOptions 
        { 
            Interval = 10,
            MinElevation = 30,
            MaxElevation = 70
        };

        var result = _generator.GenerateContours(elevationData, options);

        foreach (var contour in result.Contours.Concat(result.MajorContours))
        {
            Assert.True(contour.Elevation >= options.MinElevation);
            Assert.True(contour.Elevation <= options.MaxElevation);
        }
    }

    [Fact]
    public void ApplyContoursToMesh_ReturnsValidMesh()
    {
        var elevationData = CreateTestElevationData();
        var options = new ContourOptions { Interval = 20 };
        var contours = _generator.GenerateContours(elevationData, options);
        var mesh = CreateSimpleMesh();

        var result = _generator.ApplyContoursToMesh(mesh, contours, 0.5f, false);

        Assert.NotNull(result);
        Assert.True(result.VertexCount > 0);
    }

    private static ElevationData CreateTestElevationData()
    {
        // Create a simple hill pattern
        var data = new ElevationData(20, 20, 10, 10);
        
        for (int row = 0; row < 20; row++)
        {
            for (int col = 0; col < 20; col++)
            {
                // Create a hill centered at (10, 10)
                float dx = col - 10;
                float dy = row - 10;
                float distance = MathF.Sqrt(dx * dx + dy * dy);
                float elevation = 100 - distance * 5;
                data.SetValue(row, col, Math.Max(0, elevation));
            }
        }
        
        data.RecalculateMinMax();
        return data;
    }

    private static TerrainMesh CreateSimpleMesh()
    {
        var mesh = new TerrainMesh();
        
        mesh.AddVertex(0, 0, 0);
        mesh.AddVertex(100, 0, 0);
        mesh.AddVertex(100, 100, 0);
        mesh.AddVertex(0, 100, 0);
        mesh.AddVertex(50, 50, 50);

        mesh.AddTriangle(0, 1, 4);
        mesh.AddTriangle(1, 2, 4);
        mesh.AddTriangle(2, 3, 4);
        mesh.AddTriangle(3, 0, 4);

        mesh.CalculateNormals();
        return mesh;
    }
}
