using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;
using TerrainMapGenerator.Core.Services;
using Xunit;

namespace TerrainMapGenerator.Tests;

public class LabelGeneratorTests
{
    private readonly LabelGenerator _generator = new();

    [Fact]
    public void GeneratePeakLabels_WithValidData_ReturnsLabels()
    {
        var elevationData = CreateTestElevationData();
        var options = new LabelOptions
        {
            MaxPeakLabels = 5,
            MinProminence = 1
        };

        var result = _generator.GeneratePeakLabels(elevationData, options);

        Assert.NotNull(result);
    }

    [Fact]
    public void GeneratePeakLabels_RespectsMaxLabels()
    {
        var elevationData = CreateTestElevationData();
        var options = new LabelOptions
        {
            MaxPeakLabels = 3,
            MinProminence = 0.1f
        };

        var result = _generator.GeneratePeakLabels(elevationData, options);

        Assert.True(result.Labels.Count <= options.MaxPeakLabels);
    }

    [Fact]
    public async Task GeneratePeakLabelsAsync_Works()
    {
        var elevationData = CreateTestElevationData();
        var options = new LabelOptions();

        var result = await _generator.GeneratePeakLabelsAsync(elevationData, options);

        Assert.NotNull(result);
    }

    [Fact]
    public void GeneratePeakLabels_WithNullData_ThrowsException()
    {
        var options = new LabelOptions();

        Assert.Throws<ArgumentNullException>(() => _generator.GeneratePeakLabels(null!, options));
    }

    [Fact]
    public void GeneratePeakLabels_WithNullOptions_ThrowsException()
    {
        var elevationData = CreateTestElevationData();

        Assert.Throws<ArgumentNullException>(() => _generator.GeneratePeakLabels(elevationData, null!));
    }

    [Fact]
    public void CreateLabel_ReturnsValidLabel()
    {
        var options = new LabelOptions
        {
            FontSize = 5,
            TextDepth = 0.5f
        };

        var label = _generator.CreateLabel("Test", (50, 50), options);

        Assert.Equal("Test", label.Text);
        Assert.Equal((50f, 50f), label.Position);
        Assert.Equal(5, label.FontSize);
        Assert.Equal(0.5f, label.Depth);
        Assert.Equal(LabelType.Custom, label.Type);
    }

    [Fact]
    public void CreateLabel_WithNullText_ThrowsException()
    {
        var options = new LabelOptions();

        Assert.Throws<ArgumentNullException>(() => _generator.CreateLabel(null!, (50, 50), options));
    }

    [Fact]
    public void GeneratePeakLabels_WithElevationFormat_FormatsCorrectly()
    {
        var elevationData = CreateTestElevationData();
        var options = new LabelOptions
        {
            MaxPeakLabels = 5,
            MinProminence = 0.1f,
            IncludeElevation = true,
            ElevationFormat = "{0:F0}m"
        };

        var result = _generator.GeneratePeakLabels(elevationData, options);

        foreach (var label in result.Labels)
        {
            Assert.Contains("m", label.Text);
        }
    }

    [Fact]
    public void GeneratePeakLabels_WithoutElevation_UsesSymbol()
    {
        var elevationData = CreateTestElevationData();
        var options = new LabelOptions
        {
            MaxPeakLabels = 5,
            MinProminence = 0.1f,
            IncludeElevation = false
        };

        var result = _generator.GeneratePeakLabels(elevationData, options);

        foreach (var label in result.Labels)
        {
            Assert.Equal("▲", label.Text);
        }
    }

    [Fact]
    public void GeneratePeakLabels_LabelsHavePeakType()
    {
        var elevationData = CreateTestElevationData();
        var options = new LabelOptions
        {
            MaxPeakLabels = 5,
            MinProminence = 0.1f
        };

        var result = _generator.GeneratePeakLabels(elevationData, options);

        foreach (var label in result.Labels)
        {
            Assert.Equal(LabelType.Peak, label.Type);
        }
    }

    [Fact]
    public void ApplyLabelsToMesh_ReturnsValidMesh()
    {
        var mesh = CreateSimpleMesh();
        var labels = new LabelResult
        {
            Labels = new List<TerrainLabel>
            {
                new TerrainLabel
                {
                    Text = "Peak",
                    Position = (50, 50),
                    FontSize = 3,
                    Depth = 0.5f
                }
            }
        };

        var result = _generator.ApplyLabelsToMesh(mesh, labels, true);

        Assert.NotNull(result);
        Assert.True(result.VertexCount > 0);
    }

    [Fact]
    public void ApplyLabelToMesh_ReturnsValidMesh()
    {
        var mesh = CreateSimpleMesh();
        var label = new TerrainLabel
        {
            Text = "Test",
            Position = (50, 50),
            FontSize = 3,
            Depth = 0.5f
        };

        var result = _generator.ApplyLabelToMesh(mesh, label, true);

        Assert.NotNull(result);
    }

    [Fact]
    public void LabelOptions_DefaultValues()
    {
        var options = new LabelOptions();

        Assert.Equal(3.0f, options.FontSize);
        Assert.Equal(0.5f, options.TextDepth);
        Assert.Equal(TextAlignment.Center, options.Alignment);
        Assert.Equal(10, options.MaxPeakLabels);
        Assert.Equal(50.0f, options.MinProminence);
        Assert.True(options.IncludeElevation);
    }

    [Fact]
    public void TerrainLabel_Properties()
    {
        var label = new TerrainLabel
        {
            Text = "Summit",
            Position = (100, 200),
            Elevation = 4000,
            FontSize = 4,
            Depth = 0.3f,
            Rotation = 45,
            Type = LabelType.Peak,
            Alignment = TextAlignment.Left
        };

        Assert.Equal("Summit", label.Text);
        Assert.Equal((100f, 200f), label.Position);
        Assert.Equal(4000, label.Elevation);
        Assert.Equal(4, label.FontSize);
        Assert.Equal(0.3f, label.Depth);
        Assert.Equal(45, label.Rotation);
        Assert.Equal(LabelType.Peak, label.Type);
        Assert.Equal(TextAlignment.Left, label.Alignment);
    }

    [Fact]
    public void LabelResult_Count()
    {
        var result = new LabelResult();
        Assert.Equal(0, result.Count);

        result.Labels.Add(new TerrainLabel { Text = "A" });
        result.Labels.Add(new TerrainLabel { Text = "B" });
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GeneratePeakLabels_RespectsMinLabelSpacing()
    {
        var elevationData = CreateMultiPeakElevationData();
        var options = new LabelOptions
        {
            MaxPeakLabels = 10,
            MinProminence = 0.1f,
            MinLabelSpacing = 30
        };

        var result = _generator.GeneratePeakLabels(elevationData, options);

        // Check that labels are spaced apart
        for (int i = 0; i < result.Labels.Count; i++)
        {
            for (int j = i + 1; j < result.Labels.Count; j++)
            {
                var dx = result.Labels[i].Position.X - result.Labels[j].Position.X;
                var dy = result.Labels[i].Position.Y - result.Labels[j].Position.Y;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                Assert.True(distance >= options.MinLabelSpacing);
            }
        }
    }

    private static ElevationData CreateTestElevationData()
    {
        var data = new ElevationData(20, 20, 10, 10);
        
        for (int row = 0; row < 20; row++)
        {
            for (int col = 0; col < 20; col++)
            {
                // Create a simple peak in the center
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

    private static ElevationData CreateMultiPeakElevationData()
    {
        var data = new ElevationData(30, 30, 10, 10);
        
        // Create multiple peaks
        var peaks = new[] { (7, 7), (7, 22), (22, 7), (22, 22), (15, 15) };
        
        for (int row = 0; row < 30; row++)
        {
            for (int col = 0; col < 30; col++)
            {
                float maxElevation = 0;
                foreach (var (pr, pc) in peaks)
                {
                    float dx = col - pc;
                    float dy = row - pr;
                    float distance = MathF.Sqrt(dx * dx + dy * dy);
                    float elevation = 100 - distance * 5;
                    maxElevation = Math.Max(maxElevation, elevation);
                }
                data.SetValue(row, col, Math.Max(0, maxElevation));
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
