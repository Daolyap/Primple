using Primple.Core.Enums;
using Primple.Core.Models;
using Xunit;

namespace Primple.Core.Tests;

public class PrinterModelTests
{
    [Theory]
    [InlineData(PrinterModel.X1Carbon, "X1 Carbon")]
    [InlineData(PrinterModel.P1S, "P1S")]
    [InlineData(PrinterModel.P2S, "P2S")]
    [InlineData(PrinterModel.A1, "A1")]
    [InlineData(PrinterModel.A1Mini, "A1 Mini")]
    public void GetDisplayName_ReturnsCorrectName(PrinterModel model, string expectedName)
    {
        Assert.Equal(expectedName, model.GetDisplayName());
    }

    [Theory]
    [InlineData(PrinterModel.X1Carbon, 256, 256, 256)]
    [InlineData(PrinterModel.P1S, 256, 256, 256)]
    [InlineData(PrinterModel.P2S, 256, 256, 256)]
    [InlineData(PrinterModel.A1Mini, 180, 180, 180)]
    public void GetBuildVolume_ReturnsCorrectDimensions(PrinterModel model, double expectedX, double expectedY, double expectedZ)
    {
        var (x, y, z) = model.GetBuildVolume();
        
        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
        Assert.Equal(expectedZ, z);
    }

    [Theory]
    [InlineData(PrinterModel.X1Carbon, 4)]
    [InlineData(PrinterModel.P1S, 4)]
    [InlineData(PrinterModel.A1, 4)]
    public void GetAmsSlots_ReturnsCorrectCount(PrinterModel model, int expectedSlots)
    {
        Assert.Equal(expectedSlots, model.GetAmsSlots());
    }

    [Fact]
    public void AllPrinters_SupportMultiColor()
    {
        foreach (PrinterModel model in Enum.GetValues<PrinterModel>())
        {
            Assert.True(model.SupportsMultiColor(), $"{model} should support multi-color");
        }
    }
}

public class MaterialTypeTests
{
    [Theory]
    [InlineData(MaterialType.PLA, "PLA")]
    [InlineData(MaterialType.PETG, "PETG")]
    [InlineData(MaterialType.TPU, "TPU (Flexible)")]
    [InlineData(MaterialType.WoodPLA, "Wood PLA")]
    public void GetDisplayName_ReturnsCorrectName(MaterialType material, string expectedName)
    {
        Assert.Equal(expectedName, material.GetDisplayName());
    }

    [Theory]
    [InlineData(MaterialType.PLA, 190, 220)]
    [InlineData(MaterialType.PETG, 220, 250)]
    [InlineData(MaterialType.ASA, 240, 260)]
    public void GetPrintTemperature_ReturnsValidRange(MaterialType material, int expectedMin, int expectedMax)
    {
        var (min, max) = material.GetPrintTemperature();
        
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
        Assert.True(min < max);
    }

    [Theory]
    [InlineData(MaterialType.PLA, 45, 60)]
    [InlineData(MaterialType.PETG, 70, 85)]
    public void GetBedTemperature_ReturnsValidRange(MaterialType material, int expectedMin, int expectedMax)
    {
        var (min, max) = material.GetBedTemperature();
        
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
        Assert.True(min < max);
    }

    [Fact]
    public void AllMaterials_HaveUseCase()
    {
        foreach (MaterialType material in Enum.GetValues<MaterialType>())
        {
            var useCase = material.GetUseCase();
            Assert.False(string.IsNullOrEmpty(useCase), $"{material} should have a use case");
        }
    }
}

public class TerrainSettingsTests
{
    [Theory]
    [InlineData(MapSizePreset.Small, 100, 100)]
    [InlineData(MapSizePreset.Medium, 150, 150)]
    [InlineData(MapSizePreset.Large, 200, 200)]
    [InlineData(MapSizePreset.ExtraLarge, 250, 250)]
    [InlineData(MapSizePreset.BambuMax, 256, 256)]
    [InlineData(MapSizePreset.A1MiniMax, 180, 180)]
    public void GetActualSize_ReturnsCorrectDimensions(MapSizePreset preset, double expectedWidth, double expectedLength)
    {
        var settings = new TerrainSettings { SizePreset = preset };
        
        var (width, length) = settings.GetActualSize();
        
        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedLength, length);
    }

    [Fact]
    public void GetActualSize_Custom_ReturnsCustomDimensions()
    {
        var settings = new TerrainSettings
        {
            SizePreset = MapSizePreset.Custom,
            CustomWidth = 175,
            CustomLength = 225
        };
        
        var (width, length) = settings.GetActualSize();
        
        Assert.Equal(175, width);
        Assert.Equal(225, length);
    }
}

public class GeographicBoundsTests
{
    [Fact]
    public void Properties_CalculateCorrectly()
    {
        var bounds = new GeographicBounds(47.7, 47.6, -122.3, -122.4);
        
        Assert.Equal(0.1, bounds.Width, 6);
        Assert.Equal(0.1, bounds.Height, 6);
        Assert.Equal(47.65, bounds.CenterLat, 6);
        Assert.Equal(-122.35, bounds.CenterLon, 6);
    }

    [Fact]
    public void AreaKm2_CalculatesReasonableArea()
    {
        // Seattle area approximately 47°N, 122°W, 10km x 10km
        var bounds = new GeographicBounds(47.05, 46.95, -122.25, -122.35);
        
        var areaKm2 = bounds.AreaKm2;
        
        // Should be roughly 100 km² (10km x 10km)
        Assert.True(areaKm2 > 50 && areaKm2 < 200, $"Area {areaKm2} km² should be reasonable");
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var bounds = new GeographicBounds(47.7, 47.6, -122.3, -122.4);
        
        var result = bounds.ToString();
        
        Assert.Contains("N:47.7", result);
        Assert.Contains("S:47.6", result);
        Assert.Contains("E:-122.3", result);
        Assert.Contains("W:-122.4", result);
    }
}

public class TerrainMeshTests
{
    [Fact]
    public void GetBounds_ReturnsCorrectBounds()
    {
        var mesh = new TerrainMesh();
        mesh.Vertices.Add(new System.Numerics.Vector3(0, 0, 0));
        mesh.Vertices.Add(new System.Numerics.Vector3(100, 200, 50));
        mesh.Vertices.Add(new System.Numerics.Vector3(50, 100, 25));
        
        var (min, max) = mesh.GetBounds();
        
        Assert.Equal(0, min.X);
        Assert.Equal(0, min.Y);
        Assert.Equal(0, min.Z);
        Assert.Equal(100, max.X);
        Assert.Equal(200, max.Y);
        Assert.Equal(50, max.Z);
    }

    [Fact]
    public void GetDimensions_ReturnsCorrectDimensions()
    {
        var mesh = new TerrainMesh();
        mesh.Vertices.Add(new System.Numerics.Vector3(10, 20, 5));
        mesh.Vertices.Add(new System.Numerics.Vector3(60, 70, 35));
        
        var dims = mesh.GetDimensions();
        
        Assert.Equal(50, dims.X);
        Assert.Equal(50, dims.Y);
        Assert.Equal(30, dims.Z);
    }

    [Fact]
    public void Validate_EmptyMesh_ReturnsInvalid()
    {
        var mesh = new TerrainMesh();
        
        var result = mesh.Validate();
        
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty"));
    }

    [Fact]
    public void Validate_ValidMesh_ReturnsValid()
    {
        var mesh = CreateValidTetrahedron();
        
        var result = mesh.Validate();
        
        Assert.True(result.IsValid, $"Validation failed: {string.Join(", ", result.Errors)}");
        Assert.Equal(4, result.TriangleCount);
        Assert.Equal(4, result.VertexCount);
    }

    [Fact]
    public void CalculateNormals_SetsNormals()
    {
        var mesh = CreateValidTetrahedron();
        
        mesh.CalculateNormals();
        
        Assert.Equal(mesh.Vertices.Count, mesh.Normals.Count);
        foreach (var normal in mesh.Normals)
        {
            // Normals should be unit length
            var length = normal.Length();
            Assert.True(Math.Abs(length - 1.0) < 0.01, $"Normal length {length} should be 1");
        }
    }

    private TerrainMesh CreateValidTetrahedron()
    {
        var mesh = new TerrainMesh();
        
        mesh.Vertices.Add(new System.Numerics.Vector3(0, 0, 0));
        mesh.Vertices.Add(new System.Numerics.Vector3(100, 0, 0));
        mesh.Vertices.Add(new System.Numerics.Vector3(50, 100, 0));
        mesh.Vertices.Add(new System.Numerics.Vector3(50, 50, 80));
        
        mesh.Indices.AddRange(new[] { 0, 2, 1 });
        mesh.Indices.AddRange(new[] { 0, 1, 3 });
        mesh.Indices.AddRange(new[] { 1, 2, 3 });
        mesh.Indices.AddRange(new[] { 2, 0, 3 });
        
        return mesh;
    }
}
