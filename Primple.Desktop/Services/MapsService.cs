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
}

public class MapsService : IMapsService
{
    private readonly HttpClient _client = new HttpClient();
    
    public MapsService()
    {
        _client.DefaultRequestHeaders.Add("User-Agent", "PrimpleApp/1.0");
    }

    public async Task<(double lat, double lon, string name)> SearchLocation(string query)
    {
        try
        {
            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=1";
            var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return (0, 0, null);

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || !json.Trim().StartsWith("[")) return (0, 0, null);

            var results = JsonSerializer.Deserialize<List<NominatimResult>>(json);
            
            if (results != null && results.Count > 0)
            {
                var first = results[0];
                if (double.TryParse(first.Lat, out double lat) && double.TryParse(first.Lon, out double lon))
                {
                    return (lat, lon, first.DisplayName);
                }
            }
        }
        catch { }
        return (0, 0, null);
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
        var baseMat = new DiffuseMaterial(new SolidColorBrush(options.BaseColor)); 
        group.Children.Add(new GeometryModel3D(baseMesh, baseMat));

        if (data == null || data.Elements == null) return group;

        // Map Node ID -> (Lat, Lon)
        var nodes = data.Elements.Where(e => e.Type == "node").ToDictionary(n => n.Id, n => n);
        var ways = data.Elements.Where(e => e.Type == "way");

        var buildingMesh = new MeshGeometry3D();
        var roadMesh = new MeshGeometry3D();

        double boundLimit = options.RadiusMeters;

        foreach (var way in ways)
        {
            if (way.NodeIds == null || way.NodeIds.Count < 2) continue;

            // Collect points converted to local meters
            var rawPoints = new List<System.Windows.Point>();
            foreach (var nid in way.NodeIds)
            {
                if (nodes.TryGetValue(nid, out var node))
                {
                    var (x, y) = LatLonToMeters(node.Lat, node.Lon, options.CenterLat, options.CenterLon);
                    rawPoints.Add(new System.Windows.Point(x, y));
                }
            }
            if (rawPoints.Count < 2) continue;

            // Clip points to bounds [-R, R]
            // Simple approach: Skip if entirely out of bounds? Or Clamp? 
            // Better: If centroid is way out, skip. If partially in, keep.
            // For robust 3D printing "never go outside base square", we should probably clamp or skip.
            // Clamping might distort shapes. Skipping is safer for clean edges. 
            // Or ideally, perform polygon clipping (complex). 
            // Strategy: Skip if all points out of bounds. If partial, Clamp points to border.
            
            bool anyInside = rawPoints.Any(p => Math.Abs(p.X) <= boundLimit && Math.Abs(p.Y) <= boundLimit);
            if (!anyInside) continue;

            var points = rawPoints.Select(p => new System.Windows.Point(
                Math.Clamp(p.X, -boundLimit, boundLimit),
                Math.Clamp(p.Y, -boundLimit, boundLimit)
            )).ToList();


            if (way.Tags != null)
            {
                if (options.IncludeBuildings && way.Tags.ContainsKey("building"))
                {
                    // Basic level of detail handling using resolution
                    // Resolution now ranges 1-200, higher = more detail
                    if (options.Resolution < 80)
                    {
                         // Calculate approximate area
                         double areaMinX = points.Min(p => p.X);
                         double areaMaxX = points.Max(p => p.X);
                         double areaMinY = points.Min(p => p.Y);
                         double areaMaxY = points.Max(p => p.Y);
                         double area = (areaMaxX - areaMinX) * (areaMaxY - areaMinY);
                         
                         // Skip threshold scales with resolution
                         double skipThreshold = 100 - (options.Resolution * 0.5);
                         if (area < skipThreshold) continue;
                    }

                    double height = options.Is3DMode ? 10 : 1;
                    if (way.Tags.TryGetValue("levels", out string lvl) && double.TryParse(lvl, out double l)) 
                        height = l * 3;
                    
                    // Use actual building footprint instead of bounding box
                    if (points.Count >= 3)
                    {
                        // Triangulate the polygon footprint for the base
                        var triangles = TriangulatePolygon(points);
                        
                        // Add base faces (on ground, Y=0)
                        foreach (var tri in triangles)
                        {
                            AddFace(buildingMesh, 
                                new Point3D(tri.p1.X, 0, tri.p1.Y),
                                new Point3D(tri.p2.X, 0, tri.p2.Y),
                                new Point3D(tri.p3.X, 0, tri.p3.Y),
                                new Point3D(tri.p3.X, 0, tri.p3.Y)); // Degenerate quad = triangle
                        }
                        
                        // Add roof faces (at height)
                        foreach (var tri in triangles)
                        {
                            AddFace(buildingMesh,
                                new Point3D(tri.p3.X, height, tri.p3.Y),
                                new Point3D(tri.p2.X, height, tri.p2.Y),
                                new Point3D(tri.p1.X, height, tri.p1.Y),
                                new Point3D(tri.p1.X, height, tri.p1.Y)); // Flipped winding for top
                        }
                        
                        // Add walls (vertical faces connecting base to roof)
                        for (int i = 0; i < points.Count; i++)
                        {
                            var p1 = points[i];
                            var p2 = points[(i + 1) % points.Count];
                            
                            AddFace(buildingMesh,
                                new Point3D(p1.X, 0, p1.Y),
                                new Point3D(p2.X, 0, p2.Y),
                                new Point3D(p2.X, height, p2.Y),
                                new Point3D(p1.X, height, p1.Y));
                        }
                    }
                }
                else if (options.IncludeRoads && way.Tags.ContainsKey("highway"))
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
             group.Children.Add(new GeometryModel3D(buildingMesh, buildingMat));
        }

        if (roadMesh.Positions.Count > 0)
        {
             var roadMat = new DiffuseMaterial(new SolidColorBrush(options.RoadColor));
             group.Children.Add(new GeometryModel3D(roadMesh, roadMat));
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
}
