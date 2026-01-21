using TerrainMapGenerator.Core.Models;
using Xunit;

namespace TerrainMapGenerator.Tests;

public class ElevationDataTests
{
    [Fact]
    public void Constructor_WithValidDimensions_CreatesGrid()
    {
        var data = new ElevationData(10, 20);

        Assert.Equal(10, data.Width);
        Assert.Equal(20, data.Height);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    public void Constructor_WithInvalidDimensions_ThrowsException(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ElevationData(width, height));
    }

    [Fact]
    public void SetValue_GetValue_WorksCorrectly()
    {
        var data = new ElevationData(5, 5);

        data.SetValue(2, 3, 100.5f);

        Assert.Equal(100.5f, data.GetValue(2, 3), 0.001f);
    }

    [Fact]
    public void SetValue_UpdatesMinMax()
    {
        var data = new ElevationData(3, 3);

        data.SetValue(0, 0, 10f);
        data.SetValue(1, 1, 50f);
        data.SetValue(2, 2, 30f);

        Assert.Equal(10f, data.MinElevation, 0.001f);
        Assert.Equal(50f, data.MaxElevation, 0.001f);
    }

    [Fact]
    public void ElevationRange_CalculatesCorrectly()
    {
        var data = new ElevationData(3, 3);

        data.SetValue(0, 0, 100f);
        data.SetValue(1, 1, 500f);
        data.SetValue(2, 2, 200f);

        Assert.Equal(400f, data.ElevationRange, 0.001f);
    }

    [Fact]
    public void NoDataValue_IsExcludedFromMinMax()
    {
        var data = new ElevationData(3, 1)
        {
            NoDataValue = -9999f
        };

        data.SetValue(0, 0, -9999f);  // No-data value
        data.SetValue(0, 1, 100f);
        data.SetValue(0, 2, 200f);
        data.RecalculateMinMax();

        Assert.Equal(100f, data.MinElevation, 0.001f);
        Assert.Equal(200f, data.MaxElevation, 0.001f);
    }

    [Fact]
    public void GeographicDimensions_CalculateCorrectly()
    {
        var data = new ElevationData(100, 50, cellSizeX: 30.0, cellSizeY: 30.0);

        Assert.Equal(3000.0, data.GeographicWidth, 0.001);
        Assert.Equal(1500.0, data.GeographicHeight, 0.001);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(10, 0)]
    [InlineData(0, 10)]
    public void GetValue_OutOfBounds_ThrowsException(int row, int col)
    {
        var data = new ElevationData(5, 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => data.GetValue(row, col));
    }
}
