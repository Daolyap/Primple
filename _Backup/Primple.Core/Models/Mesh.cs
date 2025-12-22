using System.Numerics;

namespace Primple.Core.Models;

public class Mesh
{
    public List<Vector3> Vertices { get; set; } = new();
    public List<int> Indices { get; set; } = new();
    public List<Vector3> Normals { get; set; } = new();
    
    public string Name { get; set; } = "Untitled";
    
    public int TriangleCount => Indices.Count / 3;
    public int VertexCount => Vertices.Count;
}
