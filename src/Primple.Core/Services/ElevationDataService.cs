using Flurl.Http;
using Polly;
using Polly.Retry;
using Primple.Core.Enums;
using Primple.Core.Interfaces;
using Primple.Core.Models;

namespace Primple.Core.Services;

/// <summary>
/// Service for fetching elevation data from various sources.
/// </summary>
public class ElevationDataService : IElevationDataService
{
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly string _cacheDirectory;

    public ElevationDataService()
    {
        _cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Primple", "Cache");
        Directory.CreateDirectory(_cacheDirectory);

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<FlurlHttpException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    /// <summary>
    /// Fetches elevation data for the specified geographic bounds.
    /// </summary>
    public async Task<ElevationData> GetElevationDataAsync(GeographicBounds bounds, CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = GetCacheKey(bounds);
        var cachedData = await LoadFromCacheAsync(cacheKey);
        if (cachedData != null)
            return cachedData;

        // Try to fetch from OpenTopography or similar service
        // For now, generate sample data for demonstration
        var elevationData = GenerateSampleElevationData(bounds);

        // Cache the data
        await SaveToCacheAsync(cacheKey, elevationData);

        return elevationData;
    }

    /// <summary>
    /// Generates sample elevation data for demonstration.
    /// Creates realistic-looking terrain using Perlin-like noise.
    /// </summary>
    private ElevationData GenerateSampleElevationData(GeographicBounds bounds)
    {
        var resolution = 256; // pixels
        var data = new ElevationData
        {
            Data = new double[resolution, resolution],
            Bounds = bounds,
            Source = ElevationDataSource.SRTM30m,
            Resolution = 30,
            NoDataValue = -9999
        };

        // Generate terrain using multiple octaves of noise
        var random = new Random(bounds.GetHashCode());
        var baseElevation = 500 + random.NextDouble() * 1000; // Random base between 500-1500m
        var maxVariation = 500 + random.NextDouble() * 1500; // Random variation

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                var nx = (double)x / resolution;
                var ny = (double)y / resolution;

                // Multi-octave noise for realistic terrain
                var elevation = baseElevation;
                elevation += Noise(nx * 2, ny * 2, random) * maxVariation * 0.5;
                elevation += Noise(nx * 4, ny * 4, random) * maxVariation * 0.25;
                elevation += Noise(nx * 8, ny * 8, random) * maxVariation * 0.125;
                elevation += Noise(nx * 16, ny * 16, random) * maxVariation * 0.0625;

                // Add some mountain peaks
                var distFromCenter = Math.Sqrt(Math.Pow(nx - 0.5, 2) + Math.Pow(ny - 0.5, 2));
                if (distFromCenter < 0.3)
                {
                    elevation += (0.3 - distFromCenter) * maxVariation * 2;
                }

                // Add a valley
                var valleyDist = Math.Abs(ny - (0.3 + 0.4 * Math.Sin(nx * Math.PI * 2)));
                if (valleyDist < 0.1)
                {
                    elevation -= (0.1 - valleyDist) * maxVariation * 0.5;
                }

                data.Data[y, x] = Math.Max(0, elevation);
            }
        }

        data.CalculateStatistics();
        return data;
    }

    /// <summary>
    /// Simple noise function for terrain generation.
    /// </summary>
    private double Noise(double x, double y, Random random)
    {
        var xi = (int)Math.Floor(x);
        var yi = (int)Math.Floor(y);
        var xf = x - xi;
        var yf = y - yi;

        // Hash function for deterministic randomness
        int Hash(int px, int py) => (px * 374761393 + py * 668265263) ^ random.Next();

        var n00 = (Hash(xi, yi) % 1000) / 500.0 - 1;
        var n01 = (Hash(xi, yi + 1) % 1000) / 500.0 - 1;
        var n10 = (Hash(xi + 1, yi) % 1000) / 500.0 - 1;
        var n11 = (Hash(xi + 1, yi + 1) % 1000) / 500.0 - 1;

        // Smooth interpolation
        var u = xf * xf * (3 - 2 * xf);
        var v = yf * yf * (3 - 2 * yf);

        var nx0 = n00 * (1 - u) + n10 * u;
        var nx1 = n01 * (1 - u) + n11 * u;

        return nx0 * (1 - v) + nx1 * v;
    }

    /// <summary>
    /// Loads elevation data from a local file.
    /// </summary>
    public async Task<ElevationData> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".tif" or ".tiff" => await LoadGeoTiffAsync(filePath, cancellationToken),
            ".asc" or ".grd" => await LoadAsciiGridAsync(filePath, cancellationToken),
            ".hgt" => await LoadHgtAsync(filePath, cancellationToken),
            ".xyz" => await LoadXyzAsync(filePath, cancellationToken),
            _ => throw new NotSupportedException($"File format '{extension}' is not supported")
        };
    }

    private async Task<ElevationData> LoadGeoTiffAsync(string filePath, CancellationToken cancellationToken)
    {
        // GeoTIFF loading requires GDAL or similar library
        // For now, throw not implemented
        throw new NotImplementedException("GeoTIFF support requires additional libraries");
    }

    private async Task<ElevationData> LoadAsciiGridAsync(string filePath, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var data = new ElevationData();

        // Parse header
        var headerLines = 6;
        var ncols = 0;
        var nrows = 0;
        var xllcorner = 0.0;
        var yllcorner = 0.0;
        var cellsize = 0.0;
        var noDataValue = -9999.0;

        for (int i = 0; i < headerLines && i < lines.Length; i++)
        {
            var parts = lines[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var key = parts[0].ToLowerInvariant();
                var value = parts[1];

                switch (key)
                {
                    case "ncols": ncols = int.Parse(value); break;
                    case "nrows": nrows = int.Parse(value); break;
                    case "xllcorner": xllcorner = double.Parse(value); break;
                    case "yllcorner": yllcorner = double.Parse(value); break;
                    case "cellsize": cellsize = double.Parse(value); break;
                    case "nodata_value": noDataValue = double.Parse(value); break;
                }
            }
        }

        data.Data = new double[nrows, ncols];
        data.NoDataValue = noDataValue;
        data.Resolution = cellsize * 111000; // Approximate meters per degree
        data.Bounds = new GeographicBounds(
            yllcorner + nrows * cellsize,
            yllcorner,
            xllcorner + ncols * cellsize,
            xllcorner);

        // Parse data
        for (int row = 0; row < nrows && row + headerLines < lines.Length; row++)
        {
            var values = lines[row + headerLines].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int col = 0; col < ncols && col < values.Length; col++)
            {
                data.Data[row, col] = double.Parse(values[col]);
            }
        }

        data.CalculateStatistics();
        return data;
    }

    private async Task<ElevationData> LoadHgtAsync(string filePath, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(filePath);
        var sampleCount = fileInfo.Length == 25934402 ? 3601 : 1201;

        var data = new ElevationData
        {
            Data = new double[sampleCount, sampleCount],
            Resolution = fileInfo.Length == 25934402 ? 30 : 90,
            Source = fileInfo.Length == 25934402 ? ElevationDataSource.SRTM30m : ElevationDataSource.SRTM90m
        };

        // Parse coordinates from filename (e.g., N47W122.hgt)
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (fileName.Length >= 7)
        {
            var latDir = fileName[0];
            var lat = int.Parse(fileName.Substring(1, 2));
            var lonDir = fileName[3];
            var lon = int.Parse(fileName.Substring(4, 3));

            if (latDir == 'S') lat = -lat;
            if (lonDir == 'W') lon = -lon;

            data.Bounds = new GeographicBounds(lat + 1, lat, lon + 1, lon);
        }

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var offset = 0;

        for (int row = 0; row < sampleCount; row++)
        {
            for (int col = 0; col < sampleCount; col++)
            {
                // Big-endian 16-bit signed integer
                var value = (short)((bytes[offset] << 8) | bytes[offset + 1]);
                data.Data[row, col] = value == -32768 ? data.NoDataValue : value;
                offset += 2;
            }
        }

        data.CalculateStatistics();
        return data;
    }

    private async Task<ElevationData> LoadXyzAsync(string filePath, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var points = new List<(double X, double Y, double Z)>();

        foreach (var line in lines)
        {
            var parts = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 &&
                double.TryParse(parts[0], out var x) &&
                double.TryParse(parts[1], out var y) &&
                double.TryParse(parts[2], out var z))
            {
                points.Add((x, y, z));
            }
        }

        if (points.Count == 0)
            throw new InvalidDataException("No valid points found in XYZ file");

        // Determine bounds and create grid
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);

        // Assume points are on a regular grid
        var distinctX = points.Select(p => p.X).Distinct().OrderBy(x => x).ToList();
        var distinctY = points.Select(p => p.Y).Distinct().OrderBy(y => y).ToList();

        var ncols = distinctX.Count;
        var nrows = distinctY.Count;

        var data = new ElevationData
        {
            Data = new double[nrows, ncols],
            Bounds = new GeographicBounds(maxY, minY, maxX, minX),
            Resolution = ncols > 1 ? Math.Abs(distinctX[1] - distinctX[0]) * 111000 : 30
        };

        // Initialize with no-data
        for (int row = 0; row < nrows; row++)
        {
            for (int col = 0; col < ncols; col++)
            {
                data.Data[row, col] = data.NoDataValue;
            }
        }

        // Fill in values
        foreach (var point in points)
        {
            var col = distinctX.IndexOf(point.X);
            var row = nrows - 1 - distinctY.IndexOf(point.Y); // Flip Y
            if (row >= 0 && row < nrows && col >= 0 && col < ncols)
            {
                data.Data[row, col] = point.Z;
            }
        }

        data.CalculateStatistics();
        return data;
    }

    /// <summary>
    /// Check if data is available for the specified region.
    /// </summary>
    public async Task<bool> IsDataAvailableAsync(GeographicBounds bounds)
    {
        // For now, always return true since we can generate sample data
        return await Task.FromResult(true);
    }

    private string GetCacheKey(GeographicBounds bounds)
    {
        return $"elev_{bounds.North:F4}_{bounds.South:F4}_{bounds.East:F4}_{bounds.West:F4}";
    }

    private async Task<ElevationData?> LoadFromCacheAsync(string key)
    {
        var cachePath = Path.Combine(_cacheDirectory, $"{key}.json");
        if (!File.Exists(cachePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(cachePath);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<ElevationData>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveToCacheAsync(string key, ElevationData data)
    {
        var cachePath = Path.Combine(_cacheDirectory, $"{key}.json");
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        await File.WriteAllTextAsync(cachePath, json);
    }
}
