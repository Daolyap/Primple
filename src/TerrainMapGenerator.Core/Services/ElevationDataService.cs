using System.Globalization;
using System.Text.RegularExpressions;
using TerrainMapGenerator.Core.Enums;
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Services;

/// <summary>
/// Service for loading elevation data from various file formats.
/// </summary>
public class ElevationDataService : IElevationDataService
{
    private static readonly string[] SupportedExtensionsArray = { ".asc", ".grd", ".tif", ".tiff", ".xyz" };

    public IReadOnlyList<string> SupportedExtensions => SupportedExtensionsArray;

    public bool IsFormatSupported(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensionsArray.Contains(ext);
    }

    public async Task<ElevationData> LoadFromFileAsync(string filePath)
    {
        return await Task.Run(() => LoadFromFile(filePath));
    }

    public ElevationData LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Elevation data file not found: {filePath}", filePath);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".asc" or ".grd" => LoadAsciiGrid(filePath),
            ".tif" or ".tiff" => LoadGeoTiff(filePath),
            ".xyz" => LoadXyz(filePath),
            _ => throw new NotSupportedException($"File format '{ext}' is not supported.")
        };
    }

    /// <summary>
    /// Loads an ASCII Grid format file (.asc, .grd).
    /// </summary>
    private static ElevationData LoadAsciiGrid(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int dataStartLine = 0;
        for (int i = 0; i < Math.Min(10, lines.Length); i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Check if this is a header line (contains key-value pair)
            var match = Regex.Match(line, @"^(\w+)\s+(.+)$");
            if (match.Success && !double.TryParse(match.Groups[1].Value, out _))
            {
                header[match.Groups[1].Value] = match.Groups[2].Value;
                dataStartLine = i + 1;
            }
            else
            {
                break;
            }
        }

        // Parse header values
        int ncols = GetHeaderInt(header, "ncols", "NCOLS");
        int nrows = GetHeaderInt(header, "nrows", "NROWS");
        double xllcorner = GetHeaderDouble(header, "xllcorner", "XLLCORNER", "xllcenter", "XLLCENTER");
        double yllcorner = GetHeaderDouble(header, "yllcorner", "YLLCORNER", "yllcenter", "YLLCENTER");
        double cellsize = GetHeaderDouble(header, "cellsize", "CELLSIZE", "dx", "DX");
        float nodata = (float)GetHeaderDouble(header, -9999, "nodata_value", "NODATA_VALUE", "nodata", "NODATA");

        if (ncols <= 0 || nrows <= 0)
            throw new FormatException("Invalid ASCII Grid header: ncols and nrows must be positive.");

        var elevationData = new ElevationData(ncols, nrows, cellsize, cellsize)
        {
            OriginX = xllcorner,
            OriginY = yllcorner,
            NoDataValue = nodata,
            SourceFormat = ElevationDataFormat.AsciiGrid
        };

        // Parse elevation values
        int row = 0;
        for (int lineIdx = dataStartLine; lineIdx < lines.Length && row < nrows; lineIdx++)
        {
            var line = lines[lineIdx].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var values = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int col = 0; col < Math.Min(values.Length, ncols); col++)
            {
                if (float.TryParse(values[col], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    elevationData.SetValue(row, col, value);
                }
                else
                {
                    elevationData.SetValue(row, col, nodata);
                }
            }
            row++;
        }

        elevationData.RecalculateMinMax();
        return elevationData;
    }

    /// <summary>
    /// Loads a GeoTIFF format file (.tif, .tiff).
    /// This is a simplified implementation that reads basic TIFF grayscale data.
    /// For full GeoTIFF support, a dedicated library like GDAL would be needed.
    /// </summary>
    private static ElevationData LoadGeoTiff(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        // Read TIFF header
        var byteOrder = reader.ReadUInt16();
        bool littleEndian = byteOrder == 0x4949; // II = little endian, MM = big endian

        var magic = ReadUInt16(reader, littleEndian);
        if (magic != 42)
            throw new FormatException("Not a valid TIFF file.");

        var ifdOffset = ReadUInt32(reader, littleEndian);
        stream.Seek(ifdOffset, SeekOrigin.Begin);

        // Read IFD entries
        var numEntries = ReadUInt16(reader, littleEndian);

        int width = 0, height = 0;
        int bitsPerSample = 8;
        int sampleFormat = 1; // 1 = unsigned int, 2 = signed int, 3 = float
        uint stripOffset = 0;
        int rowsPerStrip = int.MaxValue;
        double cellSizeX = 1.0, cellSizeY = 1.0;

        for (int i = 0; i < numEntries; i++)
        {
            var tag = ReadUInt16(reader, littleEndian);
            var type = ReadUInt16(reader, littleEndian);
            var count = ReadUInt32(reader, littleEndian);
            var valueOffset = ReadUInt32(reader, littleEndian);

            switch (tag)
            {
                case 256: // ImageWidth
                    width = (int)valueOffset;
                    break;
                case 257: // ImageLength
                    height = (int)valueOffset;
                    break;
                case 258: // BitsPerSample
                    bitsPerSample = (int)valueOffset;
                    break;
                case 273: // StripOffsets
                    stripOffset = valueOffset;
                    break;
                case 278: // RowsPerStrip
                    rowsPerStrip = (int)valueOffset;
                    break;
                case 339: // SampleFormat
                    sampleFormat = (int)valueOffset;
                    break;
            }
        }

        if (width <= 0 || height <= 0)
            throw new FormatException("Invalid TIFF dimensions.");

        var elevationData = new ElevationData(width, height, cellSizeX, cellSizeY)
        {
            SourceFormat = ElevationDataFormat.GeoTiff
        };

        // Read elevation data
        stream.Seek(stripOffset, SeekOrigin.Begin);

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                float value;

                if (sampleFormat == 3 && bitsPerSample == 32) // 32-bit float
                {
                    value = ReadSingle(reader, littleEndian);
                }
                else if (bitsPerSample == 16) // 16-bit integer
                {
                    var intVal = ReadInt16(reader, littleEndian);
                    value = intVal;
                }
                else if (bitsPerSample == 32 && sampleFormat != 3) // 32-bit integer
                {
                    var intVal = ReadInt32(reader, littleEndian);
                    value = intVal;
                }
                else // 8-bit
                {
                    value = reader.ReadByte();
                }

                elevationData.SetValue(row, col, value);
            }
        }

        elevationData.RecalculateMinMax();
        return elevationData;
    }

    /// <summary>
    /// Loads an XYZ point cloud format file.
    /// </summary>
    private static ElevationData LoadXyz(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var points = new List<(double X, double Y, float Z)>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

            var parts = trimmed.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                points.Add((x, y, z));
            }
        }

        if (points.Count == 0)
            throw new FormatException("No valid XYZ points found in file.");

        // Determine grid bounds and resolution
        double minX = points.Min(p => p.X);
        double maxX = points.Max(p => p.X);
        double minY = points.Min(p => p.Y);
        double maxY = points.Max(p => p.Y);

        // Estimate cell size from point spacing
        var sortedX = points.Select(p => p.X).Distinct().OrderBy(x => x).ToList();
        double cellSize = sortedX.Count > 1 ? sortedX[1] - sortedX[0] : 1.0;

        int width = (int)Math.Ceiling((maxX - minX) / cellSize) + 1;
        int height = (int)Math.Ceiling((maxY - minY) / cellSize) + 1;

        var elevationData = new ElevationData(width, height, cellSize, cellSize)
        {
            OriginX = minX,
            OriginY = minY,
            SourceFormat = ElevationDataFormat.Xyz
        };

        // Initialize with no-data
        for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
                elevationData.SetValue(row, col, elevationData.NoDataValue);

        // Place points in grid
        foreach (var (x, y, z) in points)
        {
            int col = (int)Math.Round((x - minX) / cellSize);
            // Convert from geographic Y (increases northward) to array row (row 0 is north/top)
            int row = (int)Math.Round((maxY - y) / cellSize);
            if (row >= 0 && row < height && col >= 0 && col < width)
            {
                elevationData.SetValue(row, col, z);
            }
        }

        elevationData.RecalculateMinMax();
        return elevationData;
    }

    private static int GetHeaderInt(Dictionary<string, string> header, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (header.TryGetValue(key, out var value) && int.TryParse(value, out int result))
                return result;
        }
        return 0;
    }

    private static double GetHeaderDouble(Dictionary<string, string> header, params string[] keys)
    {
        return GetHeaderDouble(header, defaultValue: 0, keys: keys);
    }

    private static double GetHeaderDouble(Dictionary<string, string> header, double defaultValue, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (header.TryGetValue(key, out var value) && 
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                return result;
        }
        return defaultValue;
    }

    // Helper methods for reading TIFF data with endianness
    private static ushort ReadUInt16(BinaryReader reader, bool littleEndian)
    {
        var bytes = reader.ReadBytes(2);
        return littleEndian ? BitConverter.ToUInt16(bytes) : (ushort)((bytes[0] << 8) | bytes[1]);
    }

    private static uint ReadUInt32(BinaryReader reader, bool littleEndian)
    {
        var bytes = reader.ReadBytes(4);
        if (!littleEndian) Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes);
    }

    private static int ReadInt16(BinaryReader reader, bool littleEndian)
    {
        var bytes = reader.ReadBytes(2);
        return littleEndian ? BitConverter.ToInt16(bytes) : (short)((bytes[0] << 8) | bytes[1]);
    }

    private static int ReadInt32(BinaryReader reader, bool littleEndian)
    {
        var bytes = reader.ReadBytes(4);
        if (!littleEndian) Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes);
    }

    private static float ReadSingle(BinaryReader reader, bool littleEndian)
    {
        var bytes = reader.ReadBytes(4);
        if (!littleEndian) Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes);
    }
}
