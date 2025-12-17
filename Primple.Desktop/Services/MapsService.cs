using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Media3D;
using Primple.Core.Services;
using HelixToolkit.Wpf;
using System.Windows.Media;

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
            var json = await _client.GetStringAsync(url);
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

    public async Task<Model3DGroup> GenerateMapModel(double centerLat, double centerLon, double radiusMeters, bool includeBuildings, bool includeRoads, bool is3DMode)
    {
        // 1. Fetch Data via Overpass API
        string query = BuildOverpassQuery(centerLat, centerLon, radiusMeters);
        string url = "https://overpass-api.de/api/interpreter";
        
        var response = await _client.PostAsync(url, new StringContent(query));
        string json = await response.Content.ReadAsStringAsync();
        var osmData = JsonSerializer.Deserialize<OsmResponse>(json);

        // 2. Process Data -> Mesh
        return BuildModelFromOsm(osmData, centerLat, centerLon, includeBuildings, includeRoads, is3DMode);
    }

    private string BuildOverpassQuery(double lat, double lon, double radius)
    {
        // Simple radius search for ways with building or highway tags
        return $"[out:json];(" +
               $"way[\"building\"](around:{radius},{lat},{lon});" +
               $"way[\"highway\"](around:{radius},{lat},{lon});" +
               $");(._;>;);out body;";
    }

    private Model3DGroup BuildModelFromOsm(OsmResponse data, double centerLat, double centerLon, bool includeBuildings, bool includeRoads, bool is3DMode)
    {
        var group = new Model3DGroup();
        if (data == null || data.Elements == null) return group;

        // Map Node ID -> (Lat, Lon)
        var nodes = data.Elements.Where(e => e.Type == "node").ToDictionary(n => n.Id, n => n);
        var ways = data.Elements.Where(e => e.Type == "way");

        // Base Plane
        double size = 1000;
        var baseMesh = new MeshGeometry3D();
        AddBox(baseMesh, new Point3D(0, -1, 0), size, 2, size);
        
        var baseMat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(200, 200, 200)));
        group.Children.Add(new GeometryModel3D(baseMesh, baseMat));

        var buildingMesh = new MeshGeometry3D();
        var roadMesh = new MeshGeometry3D();

        foreach (var way in ways)
        {
            if (way.NodeIds == null || way.NodeIds.Count < 2) continue;

            // Collect points
            var points = new List<System.Windows.Point>();
            foreach (var nid in way.NodeIds)
            {
                if (nodes.TryGetValue(nid, out var node))
                {
                    var (x, y) = LatLonToMeters(node.Lat, node.Lon, centerLat, centerLon);
                    points.Add(new System.Windows.Point(x, y));
                }
            }
            if (points.Count < 2) continue;

            if (way.Tags != null)
            {
                if (includeBuildings && way.Tags.ContainsKey("building"))
                {
                    double height = is3DMode ? 10 : 0.5;
                    if (way.Tags.TryGetValue("levels", out string lvl) && double.TryParse(lvl, out double l)) height = l * 3;
                    
                    // Simple Box at Centroid
                    double cx = points.Average(p => p.X);
                    double cy = points.Average(p => p.Y);
                    
                    // Width estimation? 
                    // Let's use bounding box size or fixed size?
                    // Fixed size for now to ensure visibility. 
                    // Better: Create convex polygon? No, sticking to Box for reliability "Project Proceeding".
                    // Actually, let's use min/max for the box.
                    double minX = points.Min(p => p.X);
                    double maxX = points.Max(p => p.X);
                    double minY = points.Min(p => p.Y);
                    double maxY = points.Max(p => p.Y);
                    double w = Math.Max(2, maxX - minX);
                    double d = Math.Max(2, maxY - minY);
                    
                    AddBox(buildingMesh, new Point3D(cx, height/2, cy), w, height, d);
                }
                else if (includeRoads && way.Tags.ContainsKey("highway"))
                {
                    double roadHeight = 0.2;
                    double width = 2; // Road width
                    
                    // Add Line Logic
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        var p1 = points[i];
                        var p2 = points[i+1];
                        AddLineSegment(roadMesh, new Point3D(p1.X, roadHeight, p1.Y), new Point3D(p2.X, roadHeight, p2.Y), width);
                    }
                }
            }
        }

        if (includeBuildings && buildingMesh.Positions.Count > 0)
        {
             var buildingMat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 100, 100)));
             group.Children.Add(new GeometryModel3D(buildingMesh, buildingMat));
        }

        if (includeRoads && roadMesh.Positions.Count > 0)
        {
             var roadMat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(50, 50, 50)));
             group.Children.Add(new GeometryModel3D(roadMesh, roadMat));
        }

        return group;
    }

    private void AddBox(MeshGeometry3D mesh, Point3D center, double xSize, double ySize, double zSize)
    {
        double hx = xSize / 2;
        double hy = ySize / 2;
        double hz = zSize / 2;

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
        // Vector along line
        var dir = end - start;
        // Horizontal perpendicular (XZ plane)
        var perp = new Vector3D(-dir.Z, 0, dir.X); // Rotate 90 deg in XZ
        perp.Normalize();
        var offset = perp * (width / 2);
        
        Point3D p0 = start - offset;
        Point3D p1 = start + offset;
        Point3D p2 = end + offset;
        Point3D p3 = end - offset;
        
        AddFace(mesh, p0, p3, p2, p1); // Top face
        // Add walls if needed for 3D printing thickness? For now just flat ribbon.
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
        // Haversine / Projection to Local Cartesian
        double r = 6378137; // Earth Radius
        double dLat = (lat - centerLat) * Math.PI / 180;
        double dLon = (lon - centerLon) * Math.PI / 180;
        double a = Math.Cos(centerLat * Math.PI / 180) * Math.Cos(lat * Math.PI / 180);
        
        // Simple Equirectangular approximation for small areas
        double x = (lon - centerLon) * (Math.PI / 180) * r * Math.Cos(centerLat * Math.PI / 180);
        double y = (lat - centerLat) * (Math.PI / 180) * r;
        
        // Convert Y (Latitude) to Z in 3D Space (Top-Down view where Y is UP? No, usually X=East, Z=North/South, Y=Elevation)
        // Here X = East (Lon), Y = North (Lat). Return as 2D X,Y. Caller maps to 3D X,Z.
        // Wait, in my `BuildModelFromOsm`: `new Point3D(cx, height/2, cy)` -> X=x, Y=Height, Z=y. 
        // So I should return x, z actually.
        
        return (x, -y); // Flip Y to match standard Z+ is South? Or -Y is North? 
        // Lat increases North. Y increases Up (in 2D).
    }
}
