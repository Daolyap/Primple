using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;
using TerrainMapGenerator.Core.Services;
using Xunit;

namespace TerrainMapGenerator.Tests;

public class HydrographyServiceTests
{
    private readonly HydrographyService _service = new();

    [Fact]
    public void AnalyzeFromElevation_WithValidData_ReturnsResult()
    {
        var elevationData = CreateTestElevationData();
        var options = new HydrographyOptions
        {
            DetectRivers = true,
            DetectLakes = true,
            FlowAccumulationThreshold = 5
        };

        var result = _service.AnalyzeFromElevation(elevationData, options);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task AnalyzeFromElevationAsync_Works()
    {
        var elevationData = CreateTestElevationData();
        var options = new HydrographyOptions();

        var result = await _service.AnalyzeFromElevationAsync(elevationData, options);

        Assert.NotNull(result);
    }

    [Fact]
    public void AnalyzeFromElevation_WithNullData_ThrowsException()
    {
        var options = new HydrographyOptions();

        Assert.Throws<ArgumentNullException>(() => _service.AnalyzeFromElevation(null!, options));
    }

    [Fact]
    public void AnalyzeFromElevation_WithNullOptions_ThrowsException()
    {
        var elevationData = CreateTestElevationData();

        Assert.Throws<ArgumentNullException>(() => _service.AnalyzeFromElevation(elevationData, null!));
    }

    [Fact]
    public void AnalyzeFromElevation_DetectsRiversWithLowThreshold()
    {
        var elevationData = CreateValleyElevationData();
        var options = new HydrographyOptions
        {
            DetectRivers = true,
            DetectLakes = false,
            FlowAccumulationThreshold = 3
        };

        var result = _service.AnalyzeFromElevation(elevationData, options);

        // A valley should produce flow accumulation
        Assert.NotNull(result.Rivers);
    }

    [Fact]
    public void AnalyzeFromElevation_DetectsLakes()
    {
        var elevationData = CreateLakeElevationData();
        var options = new HydrographyOptions
        {
            DetectRivers = false,
            DetectLakes = true,
            MinLakeArea = 3,
            FlatAreaTolerance = 0.5f
        };

        var result = _service.AnalyzeFromElevation(elevationData, options);

        Assert.NotNull(result.Lakes);
    }

    [Fact]
    public void CarveWaterFeatures_ModifiesElevationData()
    {
        var elevationData = CreateTestElevationData();
        var waterFeatures = new HydrographyResult
        {
            Rivers = new List<WaterFeature>
            {
                new WaterFeature
                {
                    Type = WaterFeatureType.River,
                    Points = new List<WaterPoint>
                    {
                        new WaterPoint(50, 50),
                        new WaterPoint(60, 60)
                    }
                }
            }
        };

        var originalValue = elevationData.Values[5, 5];
        var result = _service.CarveWaterFeatures(elevationData, waterFeatures, 10f);

        Assert.NotNull(result);
        // Carved value should be lower
        Assert.True(result.Values[5, 5] < originalValue || 
                    Math.Abs(result.Values[5, 5] - (originalValue - 10f)) < 0.001f);
    }

    [Fact]
    public void CarveWaterFeatures_PreservesOriginalData()
    {
        var elevationData = CreateTestElevationData();
        var originalValue = elevationData.Values[5, 5];
        var waterFeatures = new HydrographyResult
        {
            Rivers = new List<WaterFeature>
            {
                new WaterFeature
                {
                    Type = WaterFeatureType.River,
                    Points = new List<WaterPoint> { new WaterPoint(50, 50) }
                }
            }
        };

        _service.CarveWaterFeatures(elevationData, waterFeatures, 10f);

        // Original data should be unchanged
        Assert.Equal(originalValue, elevationData.Values[5, 5]);
    }

    [Fact]
    public void ApplyToMesh_ReturnsValidMesh()
    {
        var mesh = CreateSimpleMesh();
        var waterFeatures = new HydrographyResult();
        var options = new WaterMeshOptions();

        var result = _service.ApplyToMesh(mesh, waterFeatures, options);

        Assert.NotNull(result);
        Assert.True(result.VertexCount > 0);
    }

    [Fact]
    public void LoadFromGeoJson_WithNonExistentFile_ThrowsException()
    {
        var bounds = new GeographicBounds(-123, 37, -122, 38);

        Assert.Throws<FileNotFoundException>(() => 
            _service.LoadFromGeoJson("nonexistent.geojson", bounds));
    }

    [Fact]
    public void WaterFeature_TypesAreCorrect()
    {
        var river = new WaterFeature { Type = WaterFeatureType.River };
        var stream = new WaterFeature { Type = WaterFeatureType.Stream };
        var lake = new WaterFeature { Type = WaterFeatureType.Lake };
        var pond = new WaterFeature { Type = WaterFeatureType.Pond };

        Assert.Equal(WaterFeatureType.River, river.Type);
        Assert.Equal(WaterFeatureType.Stream, stream.Type);
        Assert.Equal(WaterFeatureType.Lake, lake.Type);
        Assert.Equal(WaterFeatureType.Pond, pond.Type);
    }

    [Fact]
    public void GeographicBounds_ContainsWorks()
    {
        var bounds = new GeographicBounds(-123, 37, -122, 38);

        Assert.True(bounds.Contains(-122.5, 37.5));
        Assert.False(bounds.Contains(-124, 37.5));
        Assert.False(bounds.Contains(-122.5, 36));
    }

    [Fact]
    public void HydrographyResult_HasFeaturesProperty()
    {
        var emptyResult = new HydrographyResult();
        Assert.False(emptyResult.HasFeatures);
        Assert.Equal(0, emptyResult.TotalFeatures);

        var resultWithRiver = new HydrographyResult
        {
            Rivers = new List<WaterFeature> { new WaterFeature() }
        };
        Assert.True(resultWithRiver.HasFeatures);
        Assert.Equal(1, resultWithRiver.TotalFeatures);
    }

    private static ElevationData CreateTestElevationData()
    {
        var data = new ElevationData(20, 20, 10, 10);
        
        for (int row = 0; row < 20; row++)
        {
            for (int col = 0; col < 20; col++)
            {
                data.SetValue(row, col, 100 - row * 2 - col);
            }
        }
        
        data.RecalculateMinMax();
        return data;
    }

    private static ElevationData CreateValleyElevationData()
    {
        var data = new ElevationData(20, 20, 10, 10);
        
        for (int row = 0; row < 20; row++)
        {
            for (int col = 0; col < 20; col++)
            {
                // Create a valley down the middle
                float distanceFromCenter = Math.Abs(col - 10);
                float elevation = distanceFromCenter * 5 + (19 - row) * 2;
                data.SetValue(row, col, elevation);
            }
        }
        
        data.RecalculateMinMax();
        return data;
    }

    private static ElevationData CreateLakeElevationData()
    {
        var data = new ElevationData(20, 20, 10, 10);
        
        for (int row = 0; row < 20; row++)
        {
            for (int col = 0; col < 20; col++)
            {
                // Create a depression in the center (potential lake)
                if (row >= 8 && row <= 12 && col >= 8 && col <= 12)
                {
                    data.SetValue(row, col, 50); // Flat area
                }
                else
                {
                    data.SetValue(row, col, 100);
                }
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

        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(0, 2, 3);

        mesh.CalculateNormals();
        return mesh;
    }
}
