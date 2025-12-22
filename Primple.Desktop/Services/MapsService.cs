using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Media3D;
using Primple.Core.Services;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Primple.Desktop.Services;

// Simple Data Models for OSM JSON
public class OsmResponse
{
    [JsonPropertyName("elements")]
    public List<OsmElement>? Elements { get; set; }
}

public class OsmElement
{
    [JsonPropertyName("type")]
    public string? Type { get; set; } // node, way
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("lat")]
    public double Lat { get; set; }
    [JsonPropertyName("lon")]
    public double Lon { get; set; }
    [JsonPropertyName("nodes")]
    public List<long>? NodeIds { get; set; }
    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}

public class NominatimResult
{
    [JsonPropertyName("lat")]
    public string? Lat { get; set; }
    [JsonPropertyName("lon")]
    public string? Lon { get; set; }
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
    [JsonPropertyName("boundingbox")]
    public List<string>? BoundingBox { get; set; }
}

public class ElevationResponse
{
    [JsonPropertyName("results")]
    public List<ElevationResult>? Results { get; set; }
}

public class ElevationResult
{
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }
    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }
    [JsonPropertyName("elevation")]
    public double Elevation { get; set; }
}

public class MapsService : IMapsService
{
    private readonly HttpClient _client = new HttpClient();
    private ILogService? _logService;
    
    // Internal class for building parts (multi-level structures)
    private class BuildingPart
    {
        public List<System.Windows.Point> Footprint { get; set; } = new List<System.Windows.Point>();
        public double MinHeight { get; set; }  // Vertical offset from ground
        public double MaxHeight { get; set; }  // Top of this part
    }
    
    public MapsService()
    {
        _client.DefaultRequestHeaders.Add("User-Agent", "PrimpleApp/1.0");
        _client.Timeout = TimeSpan.FromSeconds(60);
    }

    private void Log(string message, string level = "INFO")
    {
        if (_logService == null && App.AppHost != null)
        {
            _logService = App.AppHost.Services.GetService<ILogService>();
        }
        _logService?.Log(message, "MapsService", level);
    }

    public async Task<(double lat, double lon, string? name, double[]? boundingBox)> SearchLocation(string query)
    {
        Log($"Searching for location: {query}");
        try
        {
            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=1";
            var response = await _client.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                Log($"Location search failed with status: {response.StatusCode}", "ERROR");
                return (0, 0, null, null);
            }

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || !json.Trim().StartsWith("["))
            {
                Log("Invalid JSON response from Nominatim", "WARN");
                return (0, 0, null, null);
            }

            var results = JsonSerializer.Deserialize<List<NominatimResult>>(json);
            
            if (results != null && results.Count > 0)
            {
                var first = results[0];
                if (double.TryParse(first.Lat, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) && 
                    double.TryParse(first.Lon, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lon))
                {
                    double[]? bbox = null;
                    if (first.BoundingBox != null && first.BoundingBox.Count == 4)
                    {
                        bbox = first.BoundingBox.Select(s => double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d) ? d : 0).ToArray();
                    }
                    Log($"Found location: {first.DisplayName} at ({lat}, {lon})");
                    return (lat, lon, first.DisplayName, bbox);
                }
            }
            Log("No results found for location search", "WARN");
        }
        catch (Exception ex)
        {
            Log($"Location search error: {ex.Message}", "ERROR");
        }
        return (0, 0, null, null);
    }

    public async Task<Model3DGroup> GenerateMapModel(MapGenerationOptions options)
    {
        Log($"Generating map model at ({options.CenterLat}, {options.CenterLon}) with radius {options.RadiusMeters}m");
        
        // 1. Fetch Data
        string query = BuildOverpassQuery(options.CenterLat, options.CenterLon, options.RadiusMeters);
        string url = "https://overpass-api.de/api/interpreter";
        OsmResponse? osmData = null;
        string? errorMessage = null;

        try
        {
            Log("Fetching OSM data from Overpass API...");
            var response = await _client.PostAsync(url, new StringContent(query));
            
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(json) && json.Trim().StartsWith("{"))
                {
                    osmData = JsonSerializer.Deserialize<OsmResponse>(json);
                    Log($"OSM data received: {osmData?.Elements?.Count ?? 0} elements");
                }
                else
                {
                    errorMessage = "Invalid JSON response from Overpass API";
                    Log(errorMessage, "ERROR");
                }
            }
            else
            {
                errorMessage = $"Overpass API returned status: {response.StatusCode}";
                Log(errorMessage, "ERROR");
            }
        }
        catch (TaskCanceledException)
        {
            errorMessage = "Request timed out - try a smaller area";
            Log(errorMessage, "ERROR");
        }
        catch (HttpRequestException ex)
        {
            errorMessage = $"Network error: {ex.Message}";
            Log(errorMessage, "ERROR");
        }
        catch (Exception ex)
        {
            errorMessage = $"Unexpected error: {ex.Message}";
            Log(errorMessage, "ERROR");
        }

        // Fetch Elevation if requested
        double[,]? elevationGrid = null;
        if (options.IncludeElevation)
        {
            Log("Fetching elevation data...");
            elevationGrid = await FetchElevationData(options);
            if (elevationGrid != null)
            {
                Log("Elevation data received successfully");
            }
            else
            {
                Log("Failed to fetch elevation data - using flat terrain", "WARN");
            }
        }

        // 2. Process Data -> Mesh
        // If we had an error and no data, still generate a base model (prevents all-blue screen)
        if (osmData == null || osmData.Elements == null || osmData.Elements.Count == 0)
        {
            Log("No OSM data available - generating base terrain only", "WARN");
        }

        return BuildModelFromOsm(osmData, options, elevationGrid);
    }

    private async Task<double[,]?> FetchElevationData(MapGenerationOptions options)
    {
        int gridDivs = 10; // 100 points is enough for detail and keeps URL short
        double[,] grid = new double[gridDivs, gridDivs];
        
        try
        {
            double latDelta = options.RadiusMeters / 111320.0;
            double lonDelta = options.RadiusMeters / (111320.0 * Math.Cos(options.CenterLat * Math.PI / 180));
            
            var lats = new List<string>();
            var lons = new List<string>();
            for (int i = 0; i < gridDivs; i++) // latitude (North to South)
            {
                for (int j = 0; j < gridDivs; j++) // longitude (West to East)
                {
                    double lat = options.CenterLat + latDelta - (2 * latDelta * i / (gridDivs - 1));
                    double lon = options.CenterLon - lonDelta + (2 * lonDelta * j / (gridDivs - 1));
                    lats.Add(lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
                    lons.Add(lon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            string elevUrl = $"https://api.open-meteo.com/v1/elevation?latitude={string.Join(",", lats)}&longitude={string.Join(",", lons)}";
            
            var response = await _client.GetAsync(elevUrl);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("elevation", out var elevArray))
                {
                    int index = 0;
                    for (int i = 0; i < gridDivs; i++) 
                        for (int j = 0; j < gridDivs; j++)
                            grid[i, j] = elevArray[index++].GetDouble();
                    
                    return grid;
                }
            }
            else
            {
                Log($"Elevation API returned status: {response.StatusCode}", "WARN");
            }
        }
        catch (Exception ex)
        {
            Log($"Elevation fetch error: {ex.Message}", "ERROR");
        }
        return null;
    }

    private double GetElevation(double xMeters, double yMeters, double radius, double[,]? grid, double groundLevel)
    {
        if (grid == null) return 0;
        
        int gridDivs = grid.GetLength(0);
        double u = (xMeters + radius) / (2 * radius); // West to East
        double v = (yMeters + radius) / (2 * radius); // North to South
        
        u = Math.Clamp(u * (gridDivs - 1), 0, gridDivs - 1.0001);
        v = Math.Clamp(v * (gridDivs - 1), 0, gridDivs - 1.0001);
        
        int jLon = (int)u;
        int iLat = (int)v;
        double du = u - jLon;
        double dv = v - iLat;
        
        // Ensure we don't go out of bounds
        if (iLat >= gridDivs - 1) iLat = gridDivs - 2;
        if (jLon >= gridDivs - 1) jLon = gridDivs - 2;
        
        // Bilinear interpolation: grid[Lat, Lon]
        double e00 = grid[iLat, jLon];
        double e10 = grid[iLat + 1, jLon];
        double e01 = grid[iLat, jLon + 1];
        double e11 = grid[iLat + 1, jLon + 1];
        
        double elev = e00 * (1 - dv) * (1 - du) +
                      e10 * dv * (1 - du) +
                      e01 * (1 - dv) * du +
                      e11 * dv * du;
                      
        return Math.Max(0, elev - groundLevel);
    }

    private string BuildOverpassQuery(double lat, double lon, double radius)
    {
        return $"[out:json][timeout:45];(" +
               $"way[\"building\"](around:{radius},{lat},{lon});" +
               $"way[\"building:part\"](around:{radius},{lat},{lon});" +
               $"way[\"highway\"](around:{radius},{lat},{lon});" +
               $"way[\"natural\"=\"water\"](around:{radius},{lat},{lon});" +
               $"way[\"waterway\"](around:{radius},{lat},{lon});" +
               $");(._;>;);out body;";
    }

    private Model3DGroup BuildModelFromOsm(OsmResponse? data, MapGenerationOptions options, double[,]? elevationGrid = null)
    {
        var group = new Model3DGroup();
        double boundLimit = options.RadiusMeters;
        double size = boundLimit * 2;
        double usedGroundLevel = options.UseGroundLevel ? options.GroundLevel : 0;

        // Water depth for 3D printing (water should be a depression)
        double waterDepth = 2.0; // Default 2mm depth for water
        if (App.AppHost != null)
        {
            var settings = App.AppHost.Services.GetService<IAppSettings>();
            if (settings != null)
            {
                waterDepth = settings.WaterDepth;
            }
        }

        // 1. BASE BLOCK (The Foundation)
        var baseMesh = new MeshGeometry3D();
        double baseLevel = -2.0; // Foundation always starts 2m below terrain lowest possible (normalized)
        double thickness = options.BaseThickness;
        if (options.BaseShape == BaseShape.Circular)
            AddCircularBase(baseMesh, new Point3D(0, baseLevel - (thickness/2), 0), options.RadiusMeters, thickness, 64);
        else
            AddBox(baseMesh, new Point3D(0, baseLevel - (thickness/2), 0), size, thickness, size);

        var baseBrush = new SolidColorBrush(Color.FromRgb(20, 30, 60)); // Deep Navy Blue
        var baseMat = new MaterialGroup();
        baseMat.Children.Add(new DiffuseMaterial(baseBrush));
        var baseModel = new GeometryModel3D(baseMesh, baseMat);
        baseModel.BackMaterial = baseMat;
        group.Children.Add(baseModel);

        // 2. COLLECT DATA (if available)
        var buildingData = new List<(OsmElement way, List<System.Windows.Point> points)>();
        var partData = new List<(OsmElement way, List<System.Windows.Point> points)>();
        var waterData = new List<(OsmElement way, List<System.Windows.Point> points)>();
        var waterLines = new List<(OsmElement way, List<System.Windows.Point> points)>();
        var roadData = new List<(OsmElement way, List<System.Windows.Point> points)>();

        if (data != null && data.Elements != null)
        {
            // Map Node ID -> (Lat, Lon)
            var nodes = data.Elements.Where(e => e.Type == "node").ToDictionary(n => n.Id, n => n);
            var ways = data.Elements.Where(e => e.Type == "way").ToList();

            foreach (var way in ways)
            {
                if (way.NodeIds == null || way.NodeIds.Count < 2) continue;
                var points = GetWayPoints(way, nodes, options, boundLimit);
                if (points.Count < 2) continue;

                if (way.Tags != null)
                {
                    if (way.Tags.ContainsKey("building")) buildingData.Add((way, points));
                    else if (way.Tags.ContainsKey("building:part")) partData.Add((way, points));
                    else if (way.Tags.TryGetValue("natural", out string? nat) && nat == "water") waterData.Add((way, points));
                    else if (way.Tags.ContainsKey("waterway")) waterLines.Add((way, points));
                    else if (way.Tags.ContainsKey("highway")) roadData.Add((way, points));
                }
            }
        }

        // 3. GROUND PLATE & PERIMETER SKIRT
        var groundMesh = new MeshGeometry3D();
        int gridDivs = (int)Math.Clamp(options.Resolution / 2, 40, 150);
        double step = size / (gridDivs - 1);
        double verticalScale = 5.0; 

        // Scale water depth proportionally to make it visible
        double scaledWaterDepth = waterDepth * verticalScale;

        // Create Grid of Vertices
        for (int i = 0; i < gridDivs; i++)
        {
            for (int j = 0; j < gridDivs; j++)
            {
                double x = -boundLimit + (i * step);
                double z = -boundLimit + (j * step);
                
                if (options.BaseShape == BaseShape.Circular && (Math.Sqrt(x * x + z * z) > boundLimit))
                {
                    groundMesh.Positions.Add(new Point3D(x, baseLevel, z)); 
                    continue;
                }

                double rawElev = GetElevation(x, z, boundLimit, elevationGrid, usedGroundLevel);
                double elev = Math.Max(-0.2, rawElev) * verticalScale;

                // Vertex Dipping: Carve riverbeds into the mesh for proper 3D printing
                var pt = new System.Windows.Point(x, z);
                bool inWater = waterData.Any(w => w.points.Count >= 3 && IsPointInPolygon(pt, w.points)) ||
                             waterLines.Any(wl => IsPointNearLine(pt, wl.points, 8.0)); // 8m radius for rivers (wider)
                
                // Water is a depression - cut down significantly for 3D printing visibility
                if (inWater) elev -= scaledWaterDepth;

                groundMesh.Positions.Add(new Point3D(x, elev, z));
            }
        }

        // Create Triangles & Skirt
        for (int i = 0; i < gridDivs - 1; i++)
        {
            for (int j = 0; j < gridDivs - 1; j++)
            {
                int i0 = i * gridDivs + j;
                int i1 = (i + 1) * gridDivs + j;
                int i2 = i * gridDivs + (j + 1);
                int i3 = (i + 1) * gridDivs + (j + 1);

                // Always add triangles (no more blocky cutouts)
                groundMesh.TriangleIndices.Add(i0); groundMesh.TriangleIndices.Add(i1); groundMesh.TriangleIndices.Add(i2);
                groundMesh.TriangleIndices.Add(i1); groundMesh.TriangleIndices.Add(i3); groundMesh.TriangleIndices.Add(i2);

                // Add Skirt on Perimeter
                bool edgeTop = (j == 0);
                bool edgeBottom = (j == gridDivs - 2);
                bool edgeLeft = (i == 0);
                bool edgeRight = (i == gridDivs - 2);

                if (edgeTop || edgeBottom || edgeLeft || edgeRight)
                {
                    void AddSkirtQuad(int idxA, int idxB) {
                         var pA = groundMesh.Positions[idxA]; var pB = groundMesh.Positions[idxB];
                         AddFace(groundMesh, pA, pB, new Point3D(pB.X, baseLevel, pB.Z), new Point3D(pA.X, baseLevel, pA.Z));
                    }
                    if (edgeTop) AddSkirtQuad(i * gridDivs, (i+1) * gridDivs);
                    if (edgeBottom) AddSkirtQuad((i+1) * gridDivs + (gridDivs-1), i * gridDivs + (gridDivs-1));
                    if (edgeLeft) AddSkirtQuad(j + 1, j);
                    if (edgeRight) AddSkirtQuad((gridDivs-1) * gridDivs + j, (gridDivs-1) * gridDivs + j + 1);
                }
            }
        }

        var groundMat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(100, 160, 100)));
        group.Children.Add(new GeometryModel3D(groundMesh, groundMat) { BackMaterial = groundMat });

        // 4. WATER SURFACE (visual representation - below ground level for 3D printing)
        // Note: Water is carved into terrain, this visual layer is for preview only
        var waterSurfaceMesh = new MeshGeometry3D();
        var waterLinesMesh = new MeshGeometry3D();

        foreach (var water in waterData)
        {
            if (water.points.Count >= 3)
            {
                var tris = TriangulatePolygon(water.points);
                foreach (var tri in tris)
                {
                    double e1 = GetElevation(tri.p1.X, tri.p1.Y, boundLimit, elevationGrid, usedGroundLevel) * verticalScale;
                    double e2 = GetElevation(tri.p2.X, tri.p2.Y, boundLimit, elevationGrid, usedGroundLevel) * verticalScale;
                    double e3 = GetElevation(tri.p3.X, tri.p3.Y, boundLimit, elevationGrid, usedGroundLevel) * verticalScale;
                    // Water surface sits at the carved depression level (slightly above bottom for visibility)
                    double surf = Math.Min(e1, Math.Min(e2, e3)) - scaledWaterDepth + 0.5;

                    AddTriangle(waterSurfaceMesh, new Point3D(tri.p1.X, surf, tri.p1.Y), new Point3D(tri.p2.X, surf, tri.p2.Y), new Point3D(tri.p3.X, surf, tri.p3.Y));
                }
            }
        }

        foreach (var wl in waterLines)
        {
            for (int i = 0; i < wl.points.Count - 1; i++)
            {
                var p1 = wl.points[i]; var p2 = wl.points[i+1];
                double e1 = GetElevation(p1.X, p1.Y, boundLimit, elevationGrid, usedGroundLevel) * verticalScale - scaledWaterDepth + 0.5;
                double e2 = GetElevation(p2.X, p2.Y, boundLimit, elevationGrid, usedGroundLevel) * verticalScale - scaledWaterDepth + 0.5;
                AddLineSegment(waterLinesMesh, new Point3D(p1.X, e1, p1.Y), new Point3D(p2.X, e2, p2.Y), 10.0); // Wider rivers
            }
        }

        var waterBrush = new SolidColorBrush(Color.FromRgb(60, 140, 240));
        var waterMat = new DiffuseMaterial(waterBrush);
        if (waterSurfaceMesh.Positions.Count > 0) group.Children.Add(new GeometryModel3D(waterSurfaceMesh, waterMat) { BackMaterial = waterMat });
        if (waterLinesMesh.Positions.Count > 0) group.Children.Add(new GeometryModel3D(waterLinesMesh, waterMat) { BackMaterial = waterMat });

        // 5. BUILDINGS & ROADS
        var buildingMesh = new MeshGeometry3D();
        var roadMesh = new MeshGeometry3D();

        var skipBuildingIds = new HashSet<long>();
        foreach (var part in partData)
        {
            var centroid = GetCentroid(part.points);
            foreach (var b in buildingData) { if (IsPointInPolygon(centroid, b.points)) skipBuildingIds.Add(b.way.Id); }
        }

        // Building elevation offset from settings
        double buildingOffset = 0.0;
        if (App.AppHost != null)
        {
            var settings = App.AppHost.Services.GetService<IAppSettings>();
            if (settings != null)
            {
                buildingOffset = settings.BuildingElevationOffset;
            }
        }

        foreach (var p in partData)
        {
            double buildingHeight = options.Is3DMode ? ExtractBuildingHeight(p.way.Tags, true) : 0.4;
            // FIX: Render building with terrain-aware vertices for inclines
            RenderBuildingVolumeOnTerrain(buildingMesh, p.points, buildingHeight, boundLimit, elevationGrid, usedGroundLevel, verticalScale, buildingOffset);
        }

        foreach (var b in buildingData)
        {
            if (skipBuildingIds.Contains(b.way.Id)) continue;
            double buildingHeight = options.Is3DMode ? ExtractBuildingHeight(b.way.Tags, true) : 0.4;
            // FIX: Render building with terrain-aware vertices for inclines
            RenderBuildingVolumeOnTerrain(buildingMesh, b.points, buildingHeight, boundLimit, elevationGrid, usedGroundLevel, verticalScale, buildingOffset);
        }

        foreach (var road in roadData)
        {
            double width = 3; string hw = road.way.Tags!["highway"];
            if (hw == "primary" || hw == "motorway") width = 6;
            else if (hw == "secondary") width = 4;
            else if (hw == "path" || hw == "footway") width = 1.5;

            for (int i = 0; i < road.points.Count - 1; i++)
            {
                var p1 = road.points[i]; var p2 = road.points[i+1];
                if ((p1 - p2).LengthSquared < 0.01) continue;
                double e1 = GetElevation(p1.X, p1.Y, boundLimit, elevationGrid, usedGroundLevel) * verticalScale + 0.12;
                double e2 = GetElevation(p2.X, p2.Y, boundLimit, elevationGrid, usedGroundLevel) * verticalScale + 0.12;
                AddLineSegment(roadMesh, new Point3D(p1.X, e1, p1.Y), new Point3D(p2.X, e2, p2.Y), width);
            }
        }

        if (buildingMesh.Positions.Count > 0)
        {
            var buildingMat = new DiffuseMaterial(new SolidColorBrush(options.BuildingColor));
            group.Children.Add(new GeometryModel3D(buildingMesh, buildingMat) { BackMaterial = buildingMat });
        }
        if (roadMesh.Positions.Count > 0)
        {
            var roadMat = new DiffuseMaterial(new SolidColorBrush(options.RoadColor));
            group.Children.Add(new GeometryModel3D(roadMesh, roadMat) { BackMaterial = roadMat });
        }

        Log($"Model generated with {group.Children.Count} elements");
        return group;
    }

    private void AddCircularBase(MeshGeometry3D mesh, Point3D center, double radius, double height, int segments)
    {
        int baseIndex = mesh.Positions.Count;
        
        // Add center point at bottom
        mesh.Positions.Add(new Point3D(center.X, center.Y - height/2, center.Z));
        
        // Add circle perimeter points at bottom
        for (int i = 0; i <= segments; i++)
        {
            double angle = (i / (double)segments) * 2 * Math.PI;
            double x = center.X + radius * Math.Cos(angle);
            double z = center.Z + radius * Math.Sin(angle);
            mesh.Positions.Add(new Point3D(x, center.Y - height/2, z));
        }
        
        // Add center point at top
        mesh.Positions.Add(new Point3D(center.X, center.Y + height/2, center.Z));
        
        // Add circle perimeter points at top
        for (int i = 0; i <= segments; i++)
        {
            double angle = (i / (double)segments) * 2 * Math.PI;
            double x = center.X + radius * Math.Cos(angle);
            double z = center.Z + radius * Math.Sin(angle);
            mesh.Positions.Add(new Point3D(x, center.Y + height/2, z));
        }
        
        // Bottom triangles (center to perimeter)
        for (int i = 0; i < segments; i++)
        {
            mesh.TriangleIndices.Add(baseIndex); // center
            mesh.TriangleIndices.Add(baseIndex + i + 1);
            mesh.TriangleIndices.Add(baseIndex + i + 2);
        }
        
        // Top triangles
        int topCenter = baseIndex + segments + 2;
        int topStart = topCenter + 1;
        for (int i = 0; i < segments; i++)
        {
            mesh.TriangleIndices.Add(topCenter);
            mesh.TriangleIndices.Add(topStart + i + 1);
            mesh.TriangleIndices.Add(topStart + i);
        }
        
        // Side quads
        for (int i = 0; i < segments; i++)
        {
            int b1 = baseIndex + i + 1;
            int b2 = baseIndex + i + 2;
            int t1 = topStart + i;
            int t2 = topStart + i + 1;
            
            // Two triangles per quad
            mesh.TriangleIndices.Add(b1);
            mesh.TriangleIndices.Add(t1);
            mesh.TriangleIndices.Add(b2);
            
            mesh.TriangleIndices.Add(b2);
            mesh.TriangleIndices.Add(t1);
            mesh.TriangleIndices.Add(t2);
        }
    }

    private void AddBox(MeshGeometry3D mesh, Point3D center, double width, double height, double depth)
    {
        double hx = width / 2;
        double hy = height / 2;
        double hz = depth / 2;

        Point3D[] p = {
            new Point3D(center.X - hx, center.Y - hy, center.Z - hz),
            new Point3D(center.X + hx, center.Y - hy, center.Z - hz),
            new Point3D(center.X + hx, center.Y - hy, center.Z + hz),
            new Point3D(center.X - hx, center.Y - hy, center.Z + hz),
            new Point3D(center.X - hx, center.Y + hy, center.Z - hz),
            new Point3D(center.X + hx, center.Y + hy, center.Z - hz),
            new Point3D(center.X + hx, center.Y + hy, center.Z + hz),
            new Point3D(center.X - hx, center.Y + hy, center.Z + hz)
        };

        AddFace(mesh, p[3], p[2], p[1], p[0]); // Bottom
        AddFace(mesh, p[4], p[5], p[6], p[7]); // Top
        AddFace(mesh, p[0], p[1], p[5], p[4]); // Front
        AddFace(mesh, p[1], p[2], p[6], p[5]); // Right
        AddFace(mesh, p[2], p[3], p[7], p[6]); // Back
        AddFace(mesh, p[3], p[0], p[4], p[7]); // Left
    }

    private void AddLineSegment(MeshGeometry3D mesh, Point3D start, Point3D end, double width)
    {
        var dir = end - start;
        var perp = new Vector3D(-dir.Z, 0, dir.X); 
        perp.Normalize();
        var offset = perp * (width / 2);
        
        Point3D p0 = start - offset;
        Point3D p1 = start + offset;
        Point3D p2 = end + offset;
        Point3D p3 = end - offset;
        
        AddFace(mesh, p0, p3, p2, p1); 
    }

    private void AddFace(MeshGeometry3D mesh, Point3D p0, Point3D p1, Point3D p2, Point3D p3)
    {
        int index = mesh.Positions.Count;
        mesh.Positions.Add(p0); mesh.Positions.Add(p1); mesh.Positions.Add(p2); mesh.Positions.Add(p3);
        mesh.TriangleIndices.Add(index); mesh.TriangleIndices.Add(index + 1); mesh.TriangleIndices.Add(index + 2);
        mesh.TriangleIndices.Add(index); mesh.TriangleIndices.Add(index + 2); mesh.TriangleIndices.Add(index + 3);
    }

    private void AddTriangle(MeshGeometry3D mesh, Point3D p0, Point3D p1, Point3D p2)
    {
        int index = mesh.Positions.Count;
        mesh.Positions.Add(p0); 
        mesh.Positions.Add(p1); 
        mesh.Positions.Add(p2);
        mesh.TriangleIndices.Add(index); 
        mesh.TriangleIndices.Add(index + 1); 
        mesh.TriangleIndices.Add(index + 2);
    }

    private (double x, double y) LatLonToMeters(double lat, double lon, double centerLat, double centerLon)
    {
        double r = 6378137; 
        // Simple Equirectangular approximation
        double x = (lon - centerLon) * (Math.PI / 180) * r * Math.Cos(centerLat * Math.PI / 180);
        double y = (lat - centerLat) * (Math.PI / 180) * r;
        return (x, -y); 
    }

    // Ear Clipping Triangulation for simple polygons
    private List<(System.Windows.Point p1, System.Windows.Point p2, System.Windows.Point p3)> TriangulatePolygon(List<System.Windows.Point> polygon)
    {
        var triangles = new List<(System.Windows.Point, System.Windows.Point, System.Windows.Point)>();
        if (polygon.Count < 3) return triangles;

        // Make a copy and ensure it's Clockwise for consistent ear clipping
        var remaining = new List<System.Windows.Point>(polygon);
        
        // Remove duplicate last point if it exists
        if (remaining.Count > 1 && remaining[0] == remaining[remaining.Count-1])
            remaining.RemoveAt(remaining.Count-1);

        if (remaining.Count < 3) return triangles;

        // Calculate signed area to check winding
        double area = 0;
        for (int i = 0; i < remaining.Count; i++)
        {
            var p1 = remaining[i];
            var p2 = remaining[(i + 1) % remaining.Count];
            area += (p2.X - p1.X) * (p2.Y + p1.Y);
        }
        
        // Normalize to CW (area > 0 for standard Screen Coordinates, but Helix might differ)
        // Let's ensure consistency: reverse if area < 0
        if (area < 0) remaining.Reverse();

        int maxIterations = remaining.Count * 10; 
        int iterations = 0;
        
        while (remaining.Count > 3 && iterations < maxIterations)
        {
            iterations++;
            bool earFound = false;
            
            for (int i = 0; i < remaining.Count; i++)
            {
                int prev = (i - 1 + remaining.Count) % remaining.Count;
                int next = (i + 1) % remaining.Count;
                
                var p1 = remaining[prev];
                var p2 = remaining[i];
                var p3 = remaining[next];
                
                if (IsConvex(p1, p2, p3) && !ContainsAnyPoint(p1, p2, p3, remaining, i))
                {
                    triangles.Add((p1, p2, p3));
                    remaining.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }
            if (!earFound) break; 
        }
        
        if (remaining.Count == 3)
            triangles.Add((remaining[0], remaining[1], remaining[2]));
        
        return triangles;
    }

    private bool IsConvex(System.Windows.Point p1, System.Windows.Point p2, System.Windows.Point p3)
    {
        // For CW winding, cross product (p2-p1) x (p3-p2) should be negative
        double cross = (p2.X - p1.X) * (p3.Y - p2.Y) - (p2.Y - p1.Y) * (p3.X - p2.X);
        return cross <= 0; 
    }

    private bool ContainsAnyPoint(System.Windows.Point p1, System.Windows.Point p2, System.Windows.Point p3, 
                                   List<System.Windows.Point> polygon, int excludeIndex)
    {
        int prev = (excludeIndex - 1 + polygon.Count) % polygon.Count;
        int next = (excludeIndex + 1) % polygon.Count;

        for (int i = 0; i < polygon.Count; i++)
        {
            if (i == excludeIndex || i == prev || i == next)
                continue;
            
            if (PointInTriangle(polygon[i], p1, p2, p3))
                return true;
        }
        return false;
    }

    private bool PointInTriangle(System.Windows.Point pt, System.Windows.Point p1, System.Windows.Point p2, System.Windows.Point p3)
    {
        // Barycentric coordinate test
        double d1 = Sign(pt, p1, p2);
        double d2 = Sign(pt, p2, p3);
        double d3 = Sign(pt, p3, p1);
        
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        
        return !(hasNeg && hasPos);
    }


    private double Sign(System.Windows.Point p1, System.Windows.Point p2, System.Windows.Point p3)
    {
        return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
    }

    /// <summary>
    /// Renders a building volume between minHeight and maxHeight
    /// </summary>
    private void RenderBuildingVolume(MeshGeometry3D mesh, List<System.Windows.Point> points, double minHeight, double maxHeight)
    {
        if (points.Count < 3) return;
        
        // Final height check
        if (maxHeight <= minHeight) maxHeight = minHeight + 1;

        var triangles = TriangulatePolygon(points);
        
        // Add base faces (at minHeight)
        foreach (var tri in triangles)
        {
            AddTriangle(mesh, 
                new Point3D(tri.p1.X, minHeight, tri.p1.Y),
                new Point3D(tri.p2.X, minHeight, tri.p2.Y),
                new Point3D(tri.p3.X, minHeight, tri.p3.Y));
        }
        
        // Add roof faces (at maxHeight) - flipped winding for correct normal
        foreach (var tri in triangles)
        {
            AddTriangle(mesh,
                new Point3D(tri.p3.X, maxHeight, tri.p3.Y),
                new Point3D(tri.p2.X, maxHeight, tri.p2.Y),
                new Point3D(tri.p1.X, maxHeight, tri.p1.Y));
        }
        
        // Add walls (vertical faces connecting minHeight to maxHeight)
        for (int i = 0; i < points.Count; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % points.Count];
            
            AddFace(mesh,
                new Point3D(p1.X, minHeight, p1.Y),
                new Point3D(p2.X, minHeight, p2.Y),
                new Point3D(p2.X, maxHeight, p2.Y),
                new Point3D(p1.X, maxHeight, p1.Y));
        }
    }

    /// <summary>
    /// Renders a building volume that follows terrain elevation - fixes floating buildings on hills
    /// Each corner of the building sits on the terrain at its elevation, walls follow the terrain.
    /// </summary>
    private void RenderBuildingVolumeOnTerrain(MeshGeometry3D mesh, List<System.Windows.Point> points, double buildingHeight,
        double boundLimit, double[,]? elevationGrid, double groundLevel, double verticalScale, double buildingOffset = 0)
    {
        if (points.Count < 3) return;
        
        // Calculate terrain elevation at each vertex
        var baseElevations = new List<double>();
        double minElevation = double.MaxValue;
        double maxElevation = double.MinValue;
        
        foreach (var pt in points)
        {
            double elev = GetElevation(pt.X, pt.Y, boundLimit, elevationGrid, groundLevel) * verticalScale + 0.1 + buildingOffset;
            baseElevations.Add(elev);
            minElevation = Math.Min(minElevation, elev);
            maxElevation = Math.Max(maxElevation, elev);
        }

        // Building roof height is constant above the minimum terrain elevation
        // This ensures buildings don't float - their base follows terrain
        double roofHeight = minElevation + buildingHeight;
        
        // Ensure roof is always above all base vertices
        if (roofHeight <= maxElevation)
        {
            roofHeight = maxElevation + buildingHeight * 0.5; // Minimum half height above highest corner
        }

        var triangles = TriangulatePolygon(points);
        
        // Add base faces (following terrain)
        foreach (var tri in triangles)
        {
            int idx1 = points.FindIndex(p => p == tri.p1);
            int idx2 = points.FindIndex(p => p == tri.p2);
            int idx3 = points.FindIndex(p => p == tri.p3);
            
            // Fall back to finding closest point if exact match fails
            if (idx1 < 0) idx1 = FindClosestPointIndex(points, tri.p1);
            if (idx2 < 0) idx2 = FindClosestPointIndex(points, tri.p2);
            if (idx3 < 0) idx3 = FindClosestPointIndex(points, tri.p3);
            
            double e1 = idx1 >= 0 && idx1 < baseElevations.Count ? baseElevations[idx1] : minElevation;
            double e2 = idx2 >= 0 && idx2 < baseElevations.Count ? baseElevations[idx2] : minElevation;
            double e3 = idx3 >= 0 && idx3 < baseElevations.Count ? baseElevations[idx3] : minElevation;
            
            AddTriangle(mesh, 
                new Point3D(tri.p1.X, e1, tri.p1.Y),
                new Point3D(tri.p2.X, e2, tri.p2.Y),
                new Point3D(tri.p3.X, e3, tri.p3.Y));
        }
        
        // Add roof faces (flat at roofHeight) - flipped winding for correct normal
        foreach (var tri in triangles)
        {
            AddTriangle(mesh,
                new Point3D(tri.p3.X, roofHeight, tri.p3.Y),
                new Point3D(tri.p2.X, roofHeight, tri.p2.Y),
                new Point3D(tri.p1.X, roofHeight, tri.p1.Y));
        }
        
        // Add walls (connecting terrain-following base to flat roof)
        for (int i = 0; i < points.Count; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % points.Count];
            double e1 = baseElevations[i];
            double e2 = baseElevations[(i + 1) % points.Count];
            
            // Wall is a quad from base elevation to roof
            AddFace(mesh,
                new Point3D(p1.X, e1, p1.Y),
                new Point3D(p2.X, e2, p2.Y),
                new Point3D(p2.X, roofHeight, p2.Y),
                new Point3D(p1.X, roofHeight, p1.Y));
        }
    }

    private int FindClosestPointIndex(List<System.Windows.Point> points, System.Windows.Point target)
    {
        int closestIdx = 0;
        double closestDist = double.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            double dist = (points[i].X - target.X) * (points[i].X - target.X) + 
                         (points[i].Y - target.Y) * (points[i].Y - target.Y);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestIdx = i;
            }
        }
        return closestIdx;
    }


    /// <summary>
    /// Extracts building height from OSM tags with comprehensive fallback logic
    /// </summary>
    private double ExtractBuildingHeight(Dictionary<string, string>? tags, bool is3DMode)
    {
        if (tags == null) return is3DMode ? 15 : 2;

        // Priority 1: Explicit height in meters
        if (TryParseHeight(tags, "height", out double height)) return height;
        if (TryParseHeight(tags, "building:height", out height)) return height;
        if (TryParseHeight(tags, "height:building", out height)) return height;

        // Priority 2: Tower-specific heights (for landmarks like Eiffel Tower)
        if (tags.ContainsKey("man_made") && tags["man_made"] == "tower")
        {
            if (TryParseHeight(tags, "tower:height", out height)) return height;
            if (TryParseHeight(tags, "height", out height)) return height;
            return 50; // Reasonable default for unmarked towers
        }

        // Priority 3: Convert levels to height (3.5m per level - more realistic)
        if (TryParseDouble(tags, "building:levels", out double levels))
            return Math.Max(levels * 3.5, 3.5);
        if (TryParseDouble(tags, "levels", out levels))
            return Math.Max(levels * 3.5, 3.5);

        // Priority 4: Min height (for elevated structures)
        if (TryParseHeight(tags, "min_height", out double minHeight))
            return Math.Max(minHeight + 10, 15);

        // Fallback: Intelligent defaults based on building type
        if (tags.TryGetValue("building", out string? buildingType))
        {
            return buildingType.ToLower() switch
            {
                "cathedral" or "church" => 25,
                "tower" => 50,
                "stadium" => 30,
                "commercial" or "office" => 20,
                "retail" => 12,
                "industrial" or "warehouse" => 8,
                "residential" or "apartments" or "house" => 12,
                "garage" or "garages" => 4,
                "shed" or "roof" => 3,
                _ => is3DMode ? 15 : 2  // Better defaults than 10m/1m
            };
        }

        return is3DMode ? 15 : 2;
    }

    /// <summary>
    /// Tries to parse a height value from OSM tags, handling various formats
    /// </summary>
    private bool TryParseHeight(Dictionary<string, string> tags, string key, out double value)
    {
        if (tags.TryGetValue(key, out string? str) && !string.IsNullOrWhiteSpace(str))
        {
            // Handle formats like "330", "330 m", "330m", "330.5"
            str = str.Trim().ToLower()
                .Replace(" m", "")
                .Replace("m", "")
                .Replace(" ", "");

            if (double.TryParse(str, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                // Clamp to reasonable values (1m to 1000m)
                value = Math.Clamp(value, 1, 1000);
                return true;
            }
        }
        value = 0;
        return false;
    }

    /// <summary>
    /// Tries to parse a generic double value from OSM tags
    /// </summary>
    private bool TryParseDouble(Dictionary<string, string> tags, string key, out double value)
    {
        if (tags.TryGetValue(key, out string? str) && !string.IsNullOrWhiteSpace(str))
        {
            str = str.Trim();
            if (double.TryParse(str, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                return value > 0;
            }
        }
        value = 0;
        return false;
    }

    private List<System.Windows.Point> GetWayPoints(OsmElement way, Dictionary<long, OsmElement> nodes, MapGenerationOptions options, double boundLimit)
    {
        var rawPoints = new List<System.Windows.Point>();
        if (way.NodeIds == null) return rawPoints;

        foreach (var nid in way.NodeIds)
        {
            if (nodes.TryGetValue(nid, out var node))
            {
                var (x, y) = LatLonToMeters(node.Lat, node.Lon, options.CenterLat, options.CenterLon);
                rawPoints.Add(new System.Windows.Point(x, y));
            }
        }

        if (rawPoints.Count < 2) return rawPoints;

        bool anyInside = rawPoints.Any(p => Math.Abs(p.X) <= boundLimit && Math.Abs(p.Y) <= boundLimit);
        if (!anyInside) return new List<System.Windows.Point>();

        return rawPoints.Select(p => new System.Windows.Point(
            Math.Clamp(p.X, -boundLimit, boundLimit),
            Math.Clamp(p.Y, -boundLimit, boundLimit)
        )).ToList();
    }

    private System.Windows.Point GetCentroid(List<System.Windows.Point> points)
    {
        if (points.Count == 0) return new System.Windows.Point(0, 0);
        return new System.Windows.Point(points.Average(p => p.X), points.Average(p => p.Y));
    }

    private bool IsPointInPolygon(System.Windows.Point p, List<System.Windows.Point> polygon)
    {
        if (polygon.Count < 3) return false;
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            if (((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y)) &&
                (p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private bool IsPointNearLine(System.Windows.Point p, List<System.Windows.Point> line, double distance)
    {
        for (int i = 0; i < line.Count - 1; i++)
        {
            var p1 = line[i];
            var p2 = line[i + 1];
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            if (dx == 0 && dy == 0) continue;

            double t = ((p.X - p1.X) * dx + (p.Y - p1.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Clamp(t, 0, 1);
            double closestX = p1.X + t * dx;
            double closestY = p1.Y + t * dy;
            double d2 = (p.X - closestX) * (p.X - closestX) + (p.Y - closestY) * (p.Y - closestY);
            if (d2 < distance * distance) return true;
        }
        return false;
    }
}
