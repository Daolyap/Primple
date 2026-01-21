using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TerrainMapGenerator.Core.Enums;
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Services;

/// <summary>
/// Service for loading elevation data from various file formats.
/// </summary>
public class ElevationDataService : IElevationDataService
{
    private static readonly string[] SupportedExtensionsArray = { ".asc", ".grd", ".tif", ".tiff", ".xyz", ".hgt", ".png" };
    private readonly ILogger<ElevationDataService> _logger;

    public ElevationDataService() : this(NullLogger<ElevationDataService>.Instance) { }

    public ElevationDataService(ILogger<ElevationDataService> logger)
    {
        _logger = logger;
    }

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

        _logger.LogInformation("Loading elevation data from {FilePath} (format: {Format})", filePath, ext);

        var result = ext switch
        {
            ".asc" or ".grd" => LoadAsciiGrid(filePath),
            ".tif" or ".tiff" => LoadGeoTiff(filePath),
            ".xyz" => LoadXyz(filePath),
            ".hgt" => LoadHgt(filePath),
            ".png" => LoadPngHeightmap(filePath),
            _ => throw new NotSupportedException($"File format '{ext}' is not supported.")
        };

        _logger.LogInformation("Loaded elevation data: {Width}x{Height}, range: {Min}m to {Max}m",
            result.Width, result.Height, result.MinElevation, result.MaxElevation);

        return result;
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

    /// <summary>
    /// Loads NASA SRTM HGT format file (.hgt).
    /// HGT files contain 16-bit signed integers in big-endian format.
    /// File naming convention: N37W122.hgt (latitude/longitude of SW corner)
    /// </summary>
    private static ElevationData LoadHgt(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;

        // Determine resolution from file size
        // SRTM1: 3601x3601 samples (1 arc-second), ~25MB
        // SRTM3: 1201x1201 samples (3 arc-seconds), ~2.8MB
        int size;
        double cellSize;

        if (fileSize == 3601 * 3601 * 2)
        {
            size = 3601;
            cellSize = 1.0 / 3600.0; // 1 arc-second in degrees
        }
        else if (fileSize == 1201 * 1201 * 2)
        {
            size = 1201;
            cellSize = 3.0 / 3600.0; // 3 arc-seconds in degrees
        }
        else
        {
            throw new FormatException($"Unknown HGT file size: {fileSize}. Expected SRTM1 or SRTM3 format.");
        }

        // Parse coordinates from filename (e.g., N37W122.hgt)
        var fileName = Path.GetFileNameWithoutExtension(filePath).ToUpperInvariant();
        var match = Regex.Match(fileName, @"^([NS])(\d{2})([EW])(\d{3})$");
        
        double originLat = 0, originLon = 0;
        if (match.Success)
        {
            originLat = int.Parse(match.Groups[2].Value);
            if (match.Groups[1].Value == "S") originLat = -originLat;

            originLon = int.Parse(match.Groups[4].Value);
            if (match.Groups[3].Value == "W") originLon = -originLon;
        }

        var elevationData = new ElevationData(size, size, cellSize, cellSize)
        {
            OriginX = originLon,
            OriginY = originLat,
            NoDataValue = -32768, // SRTM void value
            SourceFormat = ElevationDataFormat.Hgt
        };

        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        // HGT files are stored from north to south, west to east
        // Data is big-endian 16-bit signed integers
        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                var bytes = reader.ReadBytes(2);
                // Big-endian to little-endian
                short value = (short)((bytes[0] << 8) | bytes[1]);
                elevationData.SetValue(row, col, value);
            }
        }

        elevationData.RecalculateMinMax();
        return elevationData;
    }

    /// <summary>
    /// Loads a PNG heightmap file.
    /// Interprets grayscale values as elevation data.
    /// </summary>
    private static ElevationData LoadPngHeightmap(string filePath)
    {
        return LoadPngHeightmap(filePath, minElevation: 0, maxElevation: 1000);
    }

    /// <summary>
    /// Loads a PNG heightmap file with specified elevation range.
    /// </summary>
    private static ElevationData LoadPngHeightmap(string filePath, float minElevation, float maxElevation)
    {
        using var stream = File.OpenRead(filePath);
        
        // Read PNG header
        var signature = new byte[8];
        stream.Read(signature, 0, 8);
        
        // Verify PNG signature
        byte[] expectedSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        for (int i = 0; i < 8; i++)
        {
            if (signature[i] != expectedSignature[i])
                throw new FormatException("Not a valid PNG file.");
        }

        // Parse chunks to find IHDR and IDAT
        int width = 0, height = 0;
        int bitDepth = 8;
        int colorType = 0;
        var imageData = new List<byte>();
        var palette = new byte[0];

        using var reader = new BinaryReader(stream);

        while (stream.Position < stream.Length)
        {
            // Read chunk length (big-endian)
            var lengthBytes = reader.ReadBytes(4);
            Array.Reverse(lengthBytes);
            int chunkLength = BitConverter.ToInt32(lengthBytes, 0);

            // Read chunk type
            var typeBytes = reader.ReadBytes(4);
            var chunkType = System.Text.Encoding.ASCII.GetString(typeBytes);

            // Read chunk data
            var chunkData = reader.ReadBytes(chunkLength);

            // Skip CRC
            reader.ReadBytes(4);

            switch (chunkType)
            {
                case "IHDR":
                    width = (chunkData[0] << 24) | (chunkData[1] << 16) | (chunkData[2] << 8) | chunkData[3];
                    height = (chunkData[4] << 24) | (chunkData[5] << 16) | (chunkData[6] << 8) | chunkData[7];
                    bitDepth = chunkData[8];
                    colorType = chunkData[9];
                    break;

                case "PLTE":
                    palette = chunkData;
                    break;

                case "IDAT":
                    imageData.AddRange(chunkData);
                    break;

                case "IEND":
                    break;
            }
        }

        if (width == 0 || height == 0)
            throw new FormatException("Invalid PNG: Could not read image dimensions.");

        // Decompress IDAT data (zlib compressed)
        var decompressed = DecompressZlib(imageData.ToArray());

        var elevationData = new ElevationData(width, height, 1.0, 1.0)
        {
            SourceFormat = ElevationDataFormat.Raw
        };

        float elevationRange = maxElevation - minElevation;
        int bytesPerPixel = GetBytesPerPixel(colorType, bitDepth);
        int scanlineLength = 1 + width * bytesPerPixel; // +1 for filter byte

        for (int row = 0; row < height; row++)
        {
            int rowStart = row * scanlineLength + 1; // Skip filter byte

            for (int col = 0; col < width; col++)
            {
                int pixelStart = rowStart + col * bytesPerPixel;
                
                float grayscale;
                if (colorType == 0) // Grayscale
                {
                    if (bitDepth == 16 && pixelStart + 1 < decompressed.Length)
                    {
                        int value = (decompressed[pixelStart] << 8) | decompressed[pixelStart + 1];
                        grayscale = value / 65535f;
                    }
                    else if (pixelStart < decompressed.Length)
                    {
                        grayscale = decompressed[pixelStart] / 255f;
                    }
                    else
                    {
                        grayscale = 0;
                    }
                }
                else if (colorType == 2 && pixelStart + 2 < decompressed.Length) // RGB - convert to grayscale
                {
                    float r = decompressed[pixelStart] / 255f;
                    float g = decompressed[pixelStart + 1] / 255f;
                    float b = decompressed[pixelStart + 2] / 255f;
                    grayscale = 0.299f * r + 0.587f * g + 0.114f * b;
                }
                else if (colorType == 6 && pixelStart + 3 < decompressed.Length) // RGBA - convert to grayscale
                {
                    float r = decompressed[pixelStart] / 255f;
                    float g = decompressed[pixelStart + 1] / 255f;
                    float b = decompressed[pixelStart + 2] / 255f;
                    grayscale = 0.299f * r + 0.587f * g + 0.114f * b;
                }
                else
                {
                    grayscale = 0;
                }

                float elevation = minElevation + grayscale * elevationRange;
                elevationData.SetValue(row, col, elevation);
            }
        }

        elevationData.RecalculateMinMax();
        return elevationData;
    }

    private static int GetBytesPerPixel(int colorType, int bitDepth)
    {
        int samplesPerPixel = colorType switch
        {
            0 => 1, // Grayscale
            2 => 3, // RGB
            3 => 1, // Indexed
            4 => 2, // Grayscale + Alpha
            6 => 4, // RGBA
            _ => 1
        };

        return samplesPerPixel * (bitDepth / 8);
    }

    private static byte[] DecompressZlib(byte[] data)
    {
        // Skip zlib header (2 bytes)
        if (data.Length < 2)
            return Array.Empty<byte>();

        try
        {
            using var input = new MemoryStream(data, 2, data.Length - 2);
            using var output = new MemoryStream();
            using var deflate = new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress);

            deflate.CopyTo(output);
            return output.ToArray();
        }
        catch (System.IO.InvalidDataException ex)
        {
            throw new FormatException("Failed to decompress PNG image data. The file may be corrupted.", ex);
        }
    }
}
