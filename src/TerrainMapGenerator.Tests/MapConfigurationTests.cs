using TerrainMapGenerator.Core.Enums;
using TerrainMapGenerator.Core.Models;
using Xunit;

namespace TerrainMapGenerator.Tests;

public class MapConfigurationTests
{
    [Fact]
    public void DefaultConfiguration_IsValid()
    {
        var config = new MapConfiguration();

        var result = config.Validate();

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(600)]
    public void Validate_InvalidWidth_ReturnsError(double width)
    {
        var config = new MapConfiguration { OutputWidthMm = width };

        var result = config.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("width", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(600)]
    public void Validate_InvalidHeight_ReturnsError(double height)
    {
        var config = new MapConfiguration { OutputHeightMm = height };

        var result = config.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("height", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(100)]
    public void Validate_InvalidExaggeration_ReturnsError(double exaggeration)
    {
        var config = new MapConfiguration { VerticalExaggeration = exaggeration };

        var result = config.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("exaggeration", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new MapConfiguration
        {
            OutputWidthMm = 200,
            VerticalExaggeration = 3.0,
            BaseType = BaseType.Tapered
        };

        var clone = original.Clone();

        // Modify original
        original.OutputWidthMm = 100;
        original.VerticalExaggeration = 1.0;

        // Clone should be unchanged
        Assert.Equal(200, clone.OutputWidthMm);
        Assert.Equal(3.0, clone.VerticalExaggeration);
        Assert.Equal(BaseType.Tapered, clone.BaseType);
    }

    [Fact]
    public void Validate_AllBaseTypes_AreValid()
    {
        foreach (BaseType baseType in Enum.GetValues<BaseType>())
        {
            var config = new MapConfiguration { BaseType = baseType };
            var result = config.Validate();

            Assert.True(result.IsValid, $"BaseType.{baseType} should be valid");
        }
    }
}
