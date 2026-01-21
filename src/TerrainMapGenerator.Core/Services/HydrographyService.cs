using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Services;

/// <summary>
/// Service for identifying and processing water features from elevation data
/// or external data sources.
/// </summary>
public class HydrographyService : IHydrographyService
{
    private readonly ILogger<HydrographyService> _logger;

    public HydrographyService() : this(NullLogger<HydrographyService>.Instance) { }

    public HydrographyService(ILogger<HydrographyService> logger)
    {
        _logger = logger;
    }

    public async Task<HydrographyResult> AnalyzeFromElevationAsync(ElevationData elevationData, HydrographyOptions options)
    {
        return await Task.Run(() => AnalyzeFromElevation(elevationData, options));
    }

    public HydrographyResult AnalyzeFromElevation(ElevationData elevationData, HydrographyOptions options)
    {
        ArgumentNullException.ThrowIfNull(elevationData);
        ArgumentNullException.ThrowIfNull(options);

        _logger.LogInformation("Analyzing hydrography from elevation data ({Width}x{Height})",
            elevationData.Width, elevationData.Height);

        var result = new HydrographyResult();

        // Calculate flow direction and accumulation
        var flowDirection = CalculateFlowDirection(elevationData);
        var flowAccumulation = CalculateFlowAccumulation(flowDirection, elevationData.Width, elevationData.Height);

        // Detect rivers from flow accumulation
        if (options.DetectRivers)
        {
            var rivers = ExtractRivers(flowAccumulation, flowDirection, elevationData, options);
            result.Rivers.AddRange(rivers);
            _logger.LogInformation("Detected {Count} rivers/streams", rivers.Count);
        }

        // Detect lakes from flat areas
        if (options.DetectLakes)
        {
            var lakes = DetectLakes(elevationData, options);
            result.Lakes.AddRange(lakes);
            _logger.LogInformation("Detected {Count} lakes/ponds", lakes.Count);
        }

        return result;
    }

    public async Task<HydrographyResult> LoadFromGeoJsonAsync(string geoJsonPath, GeographicBounds bounds)
    {
        return await Task.Run(() => LoadFromGeoJson(geoJsonPath, bounds));
    }

    public HydrographyResult LoadFromGeoJson(string geoJsonPath, GeographicBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(geoJsonPath);
        ArgumentNullException.ThrowIfNull(bounds);

        if (!File.Exists(geoJsonPath))
            throw new FileNotFoundException("GeoJSON file not found.", geoJsonPath);

        _logger.LogInformation("Loading water features from GeoJSON: {Path}", geoJsonPath);

        var result = new HydrographyResult();
        var json = File.ReadAllText(geoJsonPath);
        
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("features", out var features))
        {
            _logger.LogWarning("No features found in GeoJSON file");
            return result;
        }

        foreach (var feature in features.EnumerateArray())
        {
            var waterFeature = ParseGeoJsonFeature(feature, bounds);
            if (waterFeature != null)
            {
                if (waterFeature.Type == WaterFeatureType.River || waterFeature.Type == WaterFeatureType.Stream)
                {
                    result.Rivers.Add(waterFeature);
                }
                else
                {
                    result.Lakes.Add(waterFeature);
                }
            }
        }

        _logger.LogInformation("Loaded {Rivers} rivers and {Lakes} lakes from GeoJSON",
            result.Rivers.Count, result.Lakes.Count);

        return result;
    }

    public ElevationData CarveWaterFeatures(ElevationData elevationData, HydrographyResult waterFeatures, float carveDepth)
    {
        ArgumentNullException.ThrowIfNull(elevationData);
        ArgumentNullException.ThrowIfNull(waterFeatures);

        _logger.LogInformation("Carving {Count} water features with depth {Depth}",
            waterFeatures.TotalFeatures, carveDepth);

        // Create a copy of elevation data to modify
        var result = new ElevationData(elevationData.Width, elevationData.Height,
            elevationData.CellSizeX, elevationData.CellSizeY)
        {
            OriginX = elevationData.OriginX,
            OriginY = elevationData.OriginY,
            NoDataValue = elevationData.NoDataValue,
            SourceFormat = elevationData.SourceFormat
        };

        // Copy values
        for (int row = 0; row < elevationData.Height; row++)
        {
            for (int col = 0; col < elevationData.Width; col++)
            {
                result.SetValue(row, col, elevationData.Values[row, col]);
            }
        }

        // Carve rivers
        foreach (var river in waterFeatures.Rivers)
        {
            CarveFeature(result, river, carveDepth);
        }

        // Carve lakes
        foreach (var lake in waterFeatures.Lakes)
        {
            CarveFeature(result, lake, carveDepth);
        }

        result.RecalculateMinMax();
        return result;
    }

    public TerrainMesh ApplyToMesh(TerrainMesh mesh, HydrographyResult waterFeatures, WaterMeshOptions options)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(waterFeatures);
        ArgumentNullException.ThrowIfNull(options);

        _logger.LogInformation("Applying {Count} water features to mesh", waterFeatures.TotalFeatures);

        // For now, return the mesh unchanged - full implementation would modify vertices
        // This is a placeholder for the full implementation
        return mesh;
    }

    private int[,] CalculateFlowDirection(ElevationData data)
    {
        var flowDir = new int[data.Height, data.Width];
        
        // D8 flow direction: 1=E, 2=SE, 4=S, 8=SW, 16=W, 32=NW, 64=N, 128=NE
        int[] dr = { 0, 1, 1, 1, 0, -1, -1, -1 };
        int[] dc = { 1, 1, 0, -1, -1, -1, 0, 1 };
        int[] dirValues = { 1, 2, 4, 8, 16, 32, 64, 128 };

        for (int row = 0; row < data.Height; row++)
        {
            for (int col = 0; col < data.Width; col++)
            {
                float centerElev = data.Values[row, col];
                if (Math.Abs(centerElev - data.NoDataValue) < 0.001f)
                {
                    flowDir[row, col] = 0;
                    continue;
                }

                float maxDrop = 0;
                int flowDirection = 0;

                for (int i = 0; i < 8; i++)
                {
                    int newRow = row + dr[i];
                    int newCol = col + dc[i];

                    if (newRow < 0 || newRow >= data.Height || newCol < 0 || newCol >= data.Width)
                        continue;

                    float neighborElev = data.Values[newRow, newCol];
                    if (Math.Abs(neighborElev - data.NoDataValue) < 0.001f)
                        continue;

                    float drop = centerElev - neighborElev;
                    // Adjust for diagonal distance
                    if (i % 2 == 1) drop /= 1.414f;

                    if (drop > maxDrop)
                    {
                        maxDrop = drop;
                        flowDirection = dirValues[i];
                    }
                }

                flowDir[row, col] = flowDirection;
            }
        }

        return flowDir;
    }

    private int[,] CalculateFlowAccumulation(int[,] flowDirection, int width, int height)
    {
        var accumulation = new int[height, width];
        var visited = new bool[height, width];

        // Initialize all cells with 1 (self)
        for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
                accumulation[row, col] = 1;

        // Calculate accumulation using recursive approach
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                if (!visited[row, col])
                {
                    CalculateAccumulationRecursive(row, col, flowDirection, accumulation, visited, width, height);
                }
            }
        }

        return accumulation;
    }

    private void CalculateAccumulationRecursive(int row, int col, int[,] flowDir, 
        int[,] accumulation, bool[,] visited, int width, int height)
    {
        if (visited[row, col]) return;

        // D8 directions mapping
        int[] dr = { 0, 1, 1, 1, 0, -1, -1, -1 };
        int[] dc = { 1, 1, 0, -1, -1, -1, 0, 1 };
        int[] dirValues = { 1, 2, 4, 8, 16, 32, 64, 128 };

        // First, calculate accumulation for all cells that flow into this one
        for (int i = 0; i < 8; i++)
        {
            int neighborRow = row + dr[i];
            int neighborCol = col + dc[i];

            if (neighborRow < 0 || neighborRow >= height || neighborCol < 0 || neighborCol >= width)
                continue;

            // Check if neighbor flows into current cell (opposite direction)
            int oppositeDir = dirValues[(i + 4) % 8];
            if (flowDir[neighborRow, neighborCol] == oppositeDir)
            {
                if (!visited[neighborRow, neighborCol])
                {
                    CalculateAccumulationRecursive(neighborRow, neighborCol, flowDir, accumulation, visited, width, height);
                }
                accumulation[row, col] += accumulation[neighborRow, neighborCol];
            }
        }

        visited[row, col] = true;
    }

    private List<WaterFeature> ExtractRivers(int[,] flowAccumulation, int[,] flowDirection, 
        ElevationData data, HydrographyOptions options)
    {
        var rivers = new List<WaterFeature>();
        var visited = new bool[data.Height, data.Width];

        // Find starting points (high accumulation cells)
        var startPoints = new List<(int Row, int Col, int Accumulation)>();
        
        for (int row = 0; row < data.Height; row++)
        {
            for (int col = 0; col < data.Width; col++)
            {
                if (flowAccumulation[row, col] >= options.FlowAccumulationThreshold)
                {
                    startPoints.Add((row, col, flowAccumulation[row, col]));
                }
            }
        }

        // Sort by accumulation (descending) to process larger rivers first
        startPoints.Sort((a, b) => b.Accumulation.CompareTo(a.Accumulation));

        // D8 directions
        int[] dr = { 0, 1, 1, 1, 0, -1, -1, -1 };
        int[] dc = { 1, 1, 0, -1, -1, -1, 0, 1 };
        int[] dirValues = { 1, 2, 4, 8, 16, 32, 64, 128 };

        foreach (var start in startPoints)
        {
            if (visited[start.Row, start.Col]) continue;

            var river = new WaterFeature
            {
                Type = start.Accumulation > options.FlowAccumulationThreshold * 10 
                    ? WaterFeatureType.River 
                    : WaterFeatureType.Stream,
                FlowAccumulation = start.Accumulation
            };

            // Trace the river downstream
            int currentRow = start.Row;
            int currentCol = start.Col;

            while (currentRow >= 0 && currentRow < data.Height && 
                   currentCol >= 0 && currentCol < data.Width &&
                   !visited[currentRow, currentCol])
            {
                visited[currentRow, currentCol] = true;

                float x = (float)(currentCol * data.CellSizeX);
                float y = (float)(currentRow * data.CellSizeY);
                river.Points.Add(new WaterPoint(x, y, data.Values[currentRow, currentCol]));

                // Follow flow direction
                int dir = flowDirection[currentRow, currentCol];
                if (dir == 0) break;

                bool found = false;
                for (int i = 0; i < 8; i++)
                {
                    if (dirValues[i] == dir)
                    {
                        currentRow += dr[i];
                        currentCol += dc[i];
                        found = true;
                        break;
                    }
                }

                if (!found) break;
            }

            if (river.Points.Count >= 3)
            {
                river.EstimatedWidth = MathF.Log(start.Accumulation) * options.RiverWidthScale;
                rivers.Add(river);
            }
        }

        return rivers;
    }

    private List<WaterFeature> DetectLakes(ElevationData data, HydrographyOptions options)
    {
        var lakes = new List<WaterFeature>();
        var visited = new bool[data.Height, data.Width];

        for (int row = 0; row < data.Height; row++)
        {
            for (int col = 0; col < data.Width; col++)
            {
                if (visited[row, col]) continue;

                float elevation = data.Values[row, col];
                if (Math.Abs(elevation - data.NoDataValue) < 0.001f)
                {
                    visited[row, col] = true;
                    continue;
                }

                // Flood fill to find flat areas
                var flatArea = new List<(int Row, int Col)>();
                var queue = new Queue<(int Row, int Col)>();
                queue.Enqueue((row, col));

                while (queue.Count > 0)
                {
                    var (r, c) = queue.Dequeue();
                    if (r < 0 || r >= data.Height || c < 0 || c >= data.Width) continue;
                    if (visited[r, c]) continue;

                    float cellElev = data.Values[r, c];
                    if (Math.Abs(cellElev - elevation) > options.FlatAreaTolerance) continue;

                    visited[r, c] = true;
                    flatArea.Add((r, c));

                    // Check 4-connected neighbors
                    queue.Enqueue((r - 1, c));
                    queue.Enqueue((r + 1, c));
                    queue.Enqueue((r, c - 1));
                    queue.Enqueue((r, c + 1));
                }

                if (flatArea.Count >= options.MinLakeArea)
                {
                    var lake = new WaterFeature
                    {
                        Type = flatArea.Count > options.MinLakeArea * 10 
                            ? WaterFeatureType.Lake 
                            : WaterFeatureType.Pond
                    };

                    // Create boundary points
                    foreach (var (r, c) in flatArea)
                    {
                        float x = (float)(c * data.CellSizeX);
                        float y = (float)(r * data.CellSizeY);
                        lake.Points.Add(new WaterPoint(x, y, elevation));
                    }

                    lakes.Add(lake);
                }
            }
        }

        return lakes;
    }

    private void CarveFeature(ElevationData data, WaterFeature feature, float carveDepth)
    {
        foreach (var point in feature.Points)
        {
            int col = (int)(point.X / data.CellSizeX);
            int row = (int)(point.Y / data.CellSizeY);

            if (row >= 0 && row < data.Height && col >= 0 && col < data.Width)
            {
                float currentElev = data.Values[row, col];
                if (Math.Abs(currentElev - data.NoDataValue) > 0.001f)
                {
                    data.SetValue(row, col, currentElev - carveDepth);
                }
            }
        }
    }

    private WaterFeature? ParseGeoJsonFeature(JsonElement feature, GeographicBounds bounds)
    {
        try
        {
            if (!feature.TryGetProperty("geometry", out var geometry)) return null;
            if (!geometry.TryGetProperty("type", out var geoType)) return null;
            if (!geometry.TryGetProperty("coordinates", out var coordinates)) return null;

            var type = geoType.GetString();
            string? name = null;
            var featureType = WaterFeatureType.River;

            if (feature.TryGetProperty("properties", out var properties))
            {
                if (properties.TryGetProperty("name", out var nameElem))
                    name = nameElem.GetString();

                if (properties.TryGetProperty("natural", out var naturalElem))
                {
                    var natural = naturalElem.GetString();
                    featureType = natural switch
                    {
                        "water" => WaterFeatureType.Lake,
                        "wetland" => WaterFeatureType.Wetland,
                        _ => WaterFeatureType.River
                    };
                }

                if (properties.TryGetProperty("waterway", out var waterwayElem))
                {
                    var waterway = waterwayElem.GetString();
                    featureType = waterway switch
                    {
                        "river" => WaterFeatureType.River,
                        "stream" => WaterFeatureType.Stream,
                        "canal" => WaterFeatureType.River,
                        _ => WaterFeatureType.Stream
                    };
                }
            }

            var waterFeature = new WaterFeature
            {
                Name = name,
                Type = featureType
            };

            if (type == "LineString")
            {
                foreach (var coord in coordinates.EnumerateArray())
                {
                    var lon = coord[0].GetDouble();
                    var lat = coord[1].GetDouble();

                    if (bounds.Contains(lon, lat))
                    {
                        waterFeature.Points.Add(new WaterPoint((float)lon, (float)lat));
                    }
                }
            }
            else if (type == "Polygon")
            {
                // Take the outer ring
                var ring = coordinates[0];
                foreach (var coord in ring.EnumerateArray())
                {
                    var lon = coord[0].GetDouble();
                    var lat = coord[1].GetDouble();

                    if (bounds.Contains(lon, lat))
                    {
                        waterFeature.Points.Add(new WaterPoint((float)lon, (float)lat));
                    }
                }
            }

            return waterFeature.Points.Count >= 2 ? waterFeature : null;
        }
        catch
        {
            return null;
        }
    }
}
