using TerrainMapGenerator.Core.Enums;
using TerrainMapGenerator.Core.Services;
using Xunit;

namespace TerrainMapGenerator.Tests;

public class ElevationDataServiceTests
{
    private readonly ElevationDataService _service = new();

    [Fact]
    public void SupportedExtensions_ContainsExpectedFormats()
    {
        var extensions = _service.SupportedExtensions;

        Assert.Contains(".asc", extensions);
        Assert.Contains(".grd", extensions);
        Assert.Contains(".tif", extensions);
        Assert.Contains(".tiff", extensions);
        Assert.Contains(".xyz", extensions);
    }

    [Theory]
    [InlineData("file.asc", true)]
    [InlineData("file.grd", true)]
    [InlineData("file.tif", true)]
    [InlineData("file.tiff", true)]
    [InlineData("file.xyz", true)]
    [InlineData("file.ASC", true)]
    [InlineData("file.TIF", true)]
    [InlineData("file.png", false)]
    [InlineData("file.stl", false)]
    [InlineData("file", false)]
    public void IsFormatSupported_ReturnsCorrectResult(string filename, bool expected)
    {
        var result = _service.IsFormatSupported(filename);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void LoadFromFile_FileNotFound_ThrowsException()
    {
        Assert.Throws<FileNotFoundException>(() =>
            _service.LoadFromFile("nonexistent.asc"));
    }

    [Fact]
    public void LoadFromFile_UnsupportedFormat_ThrowsException()
    {
        var tempFile = Path.GetTempFileName();
        var pngFile = Path.ChangeExtension(tempFile, ".png");

        try
        {
            File.Move(tempFile, pngFile);

            Assert.Throws<NotSupportedException>(() =>
                _service.LoadFromFile(pngFile));
        }
        finally
        {
            if (File.Exists(pngFile))
                File.Delete(pngFile);
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadFromFile_ValidAsciiGrid_LoadsData()
    {
        var tempFile = CreateTempAsciiGrid();

        try
        {
            var data = _service.LoadFromFile(tempFile);

            Assert.Equal(5, data.Width);
            Assert.Equal(5, data.Height);
            Assert.Equal(10.0, data.CellSizeX);
            Assert.Equal(10.0, data.CellSizeY);
            Assert.Equal(ElevationDataFormat.AsciiGrid, data.SourceFormat);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadFromFile_AsciiGrid_CorrectElevations()
    {
        var tempFile = CreateTempAsciiGrid();

        try
        {
            var data = _service.LoadFromFile(tempFile);

            // Check some values from the test grid
            Assert.Equal(100f, data.GetValue(0, 0), 0.001f);
            Assert.Equal(500f, data.GetValue(4, 4), 0.001f);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadFromFile_AsciiGrid_HandlesNoData()
    {
        var tempFile = CreateTempAsciiGridWithNoData();

        try
        {
            var data = _service.LoadFromFile(tempFile);

            // No-data values should be excluded from min/max
            Assert.Equal(100f, data.MinElevation, 0.001f);
            Assert.Equal(500f, data.MaxElevation, 0.001f);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadFromFile_XyzFormat_LoadsData()
    {
        var tempFile = CreateTempXyzFile();

        try
        {
            var data = _service.LoadFromFile(tempFile);

            Assert.True(data.Width > 0);
            Assert.True(data.Height > 0);
            Assert.Equal(ElevationDataFormat.Xyz, data.SourceFormat);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadFromFileAsync_WorksCorrectly()
    {
        var tempFile = CreateTempAsciiGrid();

        try
        {
            var data = await _service.LoadFromFileAsync(tempFile);

            Assert.Equal(5, data.Width);
            Assert.Equal(5, data.Height);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static string CreateTempAsciiGrid()
    {
        var tempFile = Path.GetTempFileName();
        var ascFile = Path.ChangeExtension(tempFile, ".asc");

        var content = @"ncols 5
nrows 5
xllcorner 0
yllcorner 0
cellsize 10
nodata_value -9999
100 150 200 250 300
150 200 250 300 350
200 250 300 350 400
250 300 350 400 450
300 350 400 450 500";

        File.WriteAllText(ascFile, content);
        File.Delete(tempFile);

        return ascFile;
    }

    private static string CreateTempAsciiGridWithNoData()
    {
        var tempFile = Path.GetTempFileName();
        var ascFile = Path.ChangeExtension(tempFile, ".asc");

        var content = @"ncols 5
nrows 5
xllcorner 0
yllcorner 0
cellsize 10
nodata_value -9999
100 150 -9999 250 300
150 200 250 300 350
-9999 250 300 350 -9999
250 300 350 400 450
300 350 400 450 500";

        File.WriteAllText(ascFile, content);
        File.Delete(tempFile);

        return ascFile;
    }

    private static string CreateTempXyzFile()
    {
        var tempFile = Path.GetTempFileName();
        var xyzFile = Path.ChangeExtension(tempFile, ".xyz");

        var content = @"# XYZ point cloud
0 0 100
10 0 150
20 0 200
0 10 150
10 10 200
20 10 250
0 20 200
10 20 250
20 20 300";

        File.WriteAllText(xyzFile, content);
        File.Delete(tempFile);

        return xyzFile;
    }
}
