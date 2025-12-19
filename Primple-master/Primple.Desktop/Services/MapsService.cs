using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Media3D;
using Primple.Core.Services;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows;

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

public class MapsService : IMapsService
{
    private readonly HttpClient _client = new HttpClient();
    
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
    }

    public async Task<(double lat, double lon, string name, double[]? boundingBox)> SearchLocation(string query)
    {
        try
        {
            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=1";
            var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return (0, 0, null, null);

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || !json.Trim().StartsWith("[")) return (0, 0, null, null);

            var results = JsonSerializer.Deserialize<List<NominatimResult>>(json);
            
            if (results != null && results.Count > 0)
            {
                var first = results[0];
                if (double.TryParse(first.Lat, out double lat) && double.TryParse(first.Lon, out double lon))
                {
                    double[]? bbox = null;
                    if (first.BoundingBox != null && first.BoundingBox.Count == 4)
                    {
                        bbox = first.BoundingBox.Select(s => double.TryParse(s, out double d) ? d : 0).ToArray();
                    }
                    return (lat, lon, first.DisplayName, bbox);
                }
            }
        }
        catch { }
        return (0, 0, null, null);
    }

    public async Task<Model3DGroup> GenerateMapModel(MapGenerationOptions options)
    {
        // 1. Fetch Data
        string query = BuildOverpassQuery(options.CenterLat, options.CenterLon, options.RadiusMeters);
        string url = "https://overpass-api.de/api/interpreter";
        OsmResponse? osmData = null;

        try
        {
            var response = await _client.PostAsync(url, new StringContent(query));
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                // Validate JSON before parsing
                if (!string.IsNullOrWhiteSpace(json) && json.Trim().StartsWith("{"))
                {
                     osmData = JsonSerializer.Deserialize<OsmResponse>(json);
                }
            }
        }
        catch 
        {
            // Fallback: Return just base plate if API fails
        }

        // 2. Process Data -> Mesh
        return BuildModelFromOsm(osmData, options);
    }

    private string BuildOverpassQuery(double lat, double lon, double radius)
    {
        return $"[out:json];(" +
               $"way[\"building\"](around:{radius},{lat},{lon});" +
               $"way[\"building:part\"](around:{radius},{lat},{lon});" +
               $"way[\"highway\"](around:{radius},{lat},{lon});" +
               $");(._;>;);out body;";
    }

    private Model3DGroup BuildModelFromOsm(OsmResponse? data, MapGenerationOptions options)
    {
        var group = new Model3DGroup();
        
        // Base Plane - Square or Circular based on options
        var baseMesh = new MeshGeometry3D();
        double size = options.RadiusMeters * 2;
        
        if (options.BaseShape == BaseShape.Circular)
        {
            // Create circular base using radial triangulation
            AddCircularBase(baseMesh, new Point3D(0, -0.5, 0), options.RadiusMeters, 1, 64);
        }
        else
        {
            // Square base
            AddBox(baseMesh, new Point3D(0, -0.5, 0), size, 1, size);
        }
        
        // Base Color acts as "Water"/Ground
        var baseBrush = new SolidColorBrush(options.BaseColor);
        var baseMat = new MaterialGroup();
        baseMat.Children.Add(new DiffuseMaterial(baseBrush));
        
        // Add a slight specular component so black or dark colors remain visible under light
        if (options.BaseColor.R < 30 && options.BaseColor.G < 30 && options.BaseColor.B < 30)
        {
            baseMat.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 10));
        }
        
        var baseModel = new GeometryModel3D(baseMesh, baseMat);
        baseModel.BackMaterial = baseMat;
        group.Children.Add(baseModel);

        if (data == null || data.Elements == null) return group;

        // Map Node ID -> (Lat, Lon)
        var nodes = data.Elements.Where(e => e.Type == "node").ToDictionary(n => n.Id, n => n);
        var ways = data.Elements.Where(e => e.Type == "way");

        var buildingMesh = new MeshGeometry3D();
        var roadMesh = new MeshGeometry3D();

        double boundLimit = options.RadiusMeters;

        // Pre-process buildings and parts to avoid double rendering
        var buildingData = new List<(OsmElement way, List<System.Windows.Point> points)>();
        var partData = new List<(OsmElement way, List<System.Windows.Point> points)>();

        foreach (var way in ways)
        {
            if (way.NodeIds == null || way.NodeIds.Count < 3) continue;
            if (way.Tags == null) continue;

            bool isBuilding = way.Tags.ContainsKey("building");
            bool isBuildingPart = way.Tags.ContainsKey("building:part");

            if (!isBuilding && !isBuildingPart) continue;

            var points = GetWayPoints(way, nodes, options, boundLimit);
            if (points.Count < 3) continue;

            if (isBuildingPart) partData.Add((way, points));
            else buildingData.Add((way, points));
        }

        // Identify which buildings to skip because they have parts
        var skipBuildingIds = new HashSet<long>();
        foreach (var part in partData)
        {
            var centroid = GetCentroid(part.points);
            foreach (var b in buildingData)
            {
                if (IsPointInPolygon(centroid, b.points))
                {
                    skipBuildingIds.Add(b.way.Id);
                }
            }
        }

        // Render Parts first (most detail)
        foreach (var p in partData)
        {
            double minHeight = 0;
            if (p.way.Tags!.TryGetValue("min_height", out string? mh) && double.TryParse(mh, out double mhVal))
                minHeight = mhVal;
            else if (p.way.Tags.TryGetValue("building:min_height", out string? bmh) && double.TryParse(bmh, out double bmhVal))
                minHeight = bmhVal;

            double height = ExtractBuildingHeight(p.way.Tags, options.Is3DMode);
            RenderBuildingVolume(buildingMesh, p.points, minHeight, height);
        }

        // Render Buildings that don't have parts
        foreach (var b in buildingData)
        {
            if (skipBuildingIds.Contains(b.way.Id)) continue;
            
            // Resolution check for generic buildings
            if (options.Resolution < 80)
            {
                 double areaMinX = b.points.Min(p => p.X);
                 double areaMaxX = b.points.Max(p => p.X);
                 double areaMinY = b.points.Min(p => p.Y);
                 double areaMaxY = b.points.Max(p => p.Y);
                 double area = (areaMaxX - areaMinX) * (areaMaxY - areaMinY);
                 
                 double skipThreshold = 100 - (options.Resolution * 0.5);
                 if (area < skipThreshold) continue;
            }

            double height = ExtractBuildingHeight(b.way.Tags, options.Is3DMode);
            RenderBuildingVolume(buildingMesh, b.points, 0, height);
        }

        // Render Roads (Standard path)
        foreach (var way in ways)
        {
            if (way.Tags != null && options.IncludeRoads && way.Tags.ContainsKey("highway"))
            {
                 var points = GetWayPoints(way, nodes, options, boundLimit);
                 if (points.Count < 2) continue;

                {
                    double roadHeight = 1.0; // Raised significantly above base for visibility
                    double width = 3; // Increased default width for visibility
                    
                    // Simple logic: Larger roads for higher importance?
                    string hw = way.Tags["highway"];
                    if (hw == "primary" || hw == "motorway") width = 6;
                    else if (hw == "secondary") width = 4;
                    else if (hw == "path" || hw == "footway") width = 1.5;

                    // Resolution check for paths - allow more paths at higher resolution
                    if (options.Resolution < 60 && width < 2) continue; // Skip paths only if res < 60

                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        var p1 = points[i];
                        var p2 = points[i+1];
                        // Skip zero length segments after clamping
                        if ((p1 - p2).LengthSquared < 0.01) continue;

                        AddLineSegment(roadMesh, new Point3D(p1.X, roadHeight, p1.Y), new Point3D(p2.X, roadHeight, p2.Y), width);
                    }
                }
            }
        }

        if (buildingMesh.Positions.Count > 0)
        {
             var buildingMat = new DiffuseMaterial(new SolidColorBrush(options.BuildingColor));
             var model = new GeometryModel3D(buildingMesh, buildingMat);
             model.BackMaterial = buildingMat; // Same color on both sides
             group.Children.Add(model);
        }

        if (roadMesh.Positions.Count > 0)
        {
             var roadMat = new DiffuseMaterial(new SolidColorBrush(options.RoadColor));
             var model = new GeometryModel3D(roadMesh, roadMat);
             model.BackMaterial = roadMat;
             group.Children.Add(model);
        }

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

        // Make a copy to work with
        var remaining = new List<System.Windows.Point>(polygon);
        
        // Ear clipping algorithm
        int maxIterations = remaining.Count * 2; // Prevent infinite loops
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
                
                // Check if this is an ear (convex vertex with no other points inside triangle)
                if (IsConvex(p1, p2, p3) && !ContainsAnyPoint(p1, p2, p3, remaining, i))
                {
                    triangles.Add((p1, p2, p3));
                    remaining.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }
            
            if (!earFound) break; // Degenerate polygon, exit
        }
        
        // Add final triangle
        if (remaining.Count == 3)
        {
            triangles.Add((remaining[0], remaining[1], remaining[2]));
        }
        
        return triangles;
    }

    private bool IsConvex(System.Windows.Point p1, System.Windows.Point p2, System.Windows.Point p3)
    {
        // Cross product to determine if angle is convex (CCW winding)
        double cross = (p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X);
        return cross > 0; // Assuming CCW winding for OSM data
    }

    private bool ContainsAnyPoint(System.Windows.Point p1, System.Windows.Point p2, System.Windows.Point p3, 
                                   List<System.Windows.Point> polygon, int excludeIndex)
    {
        for (int i = 0; i < polygon.Count; i++)
        {
            // Skip the vertices of the triangle itself
            int prev = (excludeIndex - 1 + polygon.Count) % polygon.Count;
            int next = (excludeIndex + 1) % polygon.Count;
            if (i == excludeIndex || i == prev || i == next) continue;
            
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
}
