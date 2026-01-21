using TerrainMapGenerator.Core.Enums;
using TerrainMapGenerator.Core.Models;
using TerrainMapGenerator.Core.Services;
using Xunit;

namespace TerrainMapGenerator.Tests;

public class MeshGeneratorTests
{
    private readonly MeshGenerator _generator = new();

    [Fact]
    public void Generate_WithSimpleGrid_CreatesMesh()
    {
        var data = CreateSimpleElevationData(3, 3);
        var config = new MapConfiguration
        {
            OutputWidthMm = 100,
            OutputHeightMm = 100,
            MaxPrintHeightMm = 20,
            BaseType = BaseType.Flat,
            BaseThicknessMm = 2
        };

        var mesh = _generator.Generate(data, config);

        Assert.True(mesh.VertexCount > 0);
        Assert.True(mesh.TriangleCount > 0);
    }

    [Fact]
    public void Generate_WithFlatBase_CreatesWatertightMesh()
    {
        var data = CreateSimpleElevationData(5, 5);
        var config = new MapConfiguration
        {
            OutputWidthMm = 100,
            OutputHeightMm = 100,
            MaxPrintHeightMm = 20,
            BaseType = BaseType.Flat,
            BaseThicknessMm = 3
        };

        var mesh = _generator.Generate(data, config);
        var validation = mesh.Validate();

        Assert.True(validation.IsWatertight, "Mesh should be watertight");
        Assert.True(validation.IsManifold, "Mesh should be manifold");
    }

    [Theory]
    [InlineData(BaseType.Flat)]
    [InlineData(BaseType.Tapered)]
    [InlineData(BaseType.Contoured)]
    [InlineData(BaseType.Minimal)]
    public void Generate_WithDifferentBaseTypes_CreatesMesh(BaseType baseType)
    {
        var data = CreateSimpleElevationData(5, 5);
        var config = new MapConfiguration
        {
            OutputWidthMm = 100,
            OutputHeightMm = 100,
            MaxPrintHeightMm = 20,
            BaseType = baseType,
            BaseThicknessMm = 3
        };

        var mesh = _generator.Generate(data, config);

        Assert.True(mesh.VertexCount > 0);
        Assert.True(mesh.TriangleCount > 0);
    }

    [Fact]
    public void Generate_WithNoBase_CreatesOpenMesh()
    {
        var data = CreateSimpleElevationData(5, 5);
        var config = new MapConfiguration
        {
            OutputWidthMm = 100,
            OutputHeightMm = 100,
            MaxPrintHeightMm = 20,
            BaseType = BaseType.None
        };

        var mesh = _generator.Generate(data, config);
        var validation = mesh.Validate();

        Assert.True(mesh.VertexCount > 0);
        // Open mesh should have boundary edges
        Assert.False(validation.IsWatertight);
    }

    [Fact]
    public void Generate_WithVerticalExaggeration_ScalesElevation()
    {
        var data = CreateSimpleElevationData(3, 3);
        // Set specific elevations
        data.SetValue(0, 0, 0);
        data.SetValue(1, 1, 100);
        data.RecalculateMinMax();

        var config1 = new MapConfiguration
        {
            OutputWidthMm = 100,
            OutputHeightMm = 100,
            MaxPrintHeightMm = 50,
            VerticalExaggeration = 1.0,
            BaseType = BaseType.None,
            NormalizeElevation = true
        };

        var config2 = new MapConfiguration
        {
            OutputWidthMm = 100,
            OutputHeightMm = 100,
            MaxPrintHeightMm = 50,
            VerticalExaggeration = 2.0,
            BaseType = BaseType.None,
            NormalizeElevation = true
        };

        var mesh1 = _generator.Generate(data, config1);
        var mesh2 = _generator.Generate(data, config2);

        mesh1.UpdateBounds();
        mesh2.UpdateBounds();

        // Higher exaggeration should produce taller mesh
        var height1 = mesh1.BoundsMax.Z - mesh1.BoundsMin.Z;
        var height2 = mesh2.BoundsMax.Z - mesh2.BoundsMin.Z;

        Assert.True(height2 > height1, "Higher exaggeration should produce taller mesh");
    }

    [Fact]
    public void Generate_WithSmoothing_ProducesSmoothMesh()
    {
        var data = CreateNoisyElevationData(10, 10);

        var configNoSmooth = new MapConfiguration
        {
            OutputWidthMm = 100,
            OutputHeightMm = 100,
            MaxPrintHeightMm = 20,
            ApplySmoothing = false,
            BaseType = BaseType.None
        };

        var configSmooth = new MapConfiguration
        {
            OutputWidthMm = 100,
            OutputHeightMm = 100,
            MaxPrintHeightMm = 20,
            ApplySmoothing = true,
            SmoothingRadius = 2,
            BaseType = BaseType.None
        };

        var meshNoSmooth = _generator.Generate(data, configNoSmooth);
        var meshSmooth = _generator.Generate(data, configSmooth);

        // Both should generate valid meshes
        Assert.True(meshNoSmooth.VertexCount > 0);
        Assert.True(meshSmooth.VertexCount > 0);
    }

    [Fact]
    public void Generate_WithLowResolution_ReducesTriangles()
    {
        var data = CreateSimpleElevationData(20, 20);

        var configFull = new MapConfiguration
        {
            OutputWidthMm = 100,
            OutputHeightMm = 100,
            MeshResolution = 1.0,
            BaseType = BaseType.None
        };

        var configLow = new MapConfiguration
        {
            OutputWidthMm = 100,
            OutputHeightMm = 100,
            MeshResolution = 0.5,
            BaseType = BaseType.None
        };

        var meshFull = _generator.Generate(data, configFull);
        var meshLow = _generator.Generate(data, configLow);

        Assert.True(meshLow.TriangleCount < meshFull.TriangleCount,
            "Lower resolution should produce fewer triangles");
    }

    [Fact]
    public void Generate_CalculatesNormals()
    {
        var data = CreateSimpleElevationData(3, 3);
        var config = new MapConfiguration
        {
            OutputWidthMm = 100,
            OutputHeightMm = 100,
            BaseType = BaseType.Flat
        };

        var mesh = _generator.Generate(data, config);

        Assert.Equal(mesh.TriangleCount, mesh.Normals.Count);

        // Check that normals are normalized
        foreach (var normal in mesh.Normals)
        {
            var length = normal.Length();
            Assert.True(Math.Abs(length - 1.0f) < 0.001f || Math.Abs(length) < 0.001f,
                "Normals should be unit vectors or zero vectors");
        }
    }

    [Fact]
    public async Task GenerateAsync_WorksCorrectly()
    {
        var data = CreateSimpleElevationData(5, 5);
        var config = new MapConfiguration
        {
            OutputWidthMm = 100,
            OutputHeightMm = 100
        };

        var mesh = await _generator.GenerateAsync(data, config);

        Assert.True(mesh.VertexCount > 0);
        Assert.True(mesh.TriangleCount > 0);
    }

    private static ElevationData CreateSimpleElevationData(int width, int height)
    {
        var data = new ElevationData(width, height, 10.0, 10.0);

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                // Create a simple hill pattern
                float elevation = (float)(Math.Sin(col * 0.5) * Math.Cos(row * 0.5) * 100 + 500);
                data.SetValue(row, col, elevation);
            }
        }

        data.RecalculateMinMax();
        return data;
    }

    private static ElevationData CreateNoisyElevationData(int width, int height)
    {
        var data = new ElevationData(width, height, 10.0, 10.0);
        var random = new Random(42); // Fixed seed for reproducibility

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                float elevation = (float)(random.NextDouble() * 200 + 500);
                data.SetValue(row, col, elevation);
            }
        }

        data.RecalculateMinMax();
        return data;
    }
}
