namespace TerrainMapGenerator.Core.Models;

/// <summary>
/// Represents a 3D mesh with vertices and triangular faces.
/// </summary>
public class TerrainMesh
{
    private readonly List<Vector3> _vertices;
    private readonly List<Triangle> _triangles;
    private readonly List<Vector3> _normals;

    /// <summary>
    /// Gets the vertices of the mesh.
    /// </summary>
    public IReadOnlyList<Vector3> Vertices => _vertices;

    /// <summary>
    /// Gets the triangles of the mesh.
    /// </summary>
    public IReadOnlyList<Triangle> Triangles => _triangles;

    /// <summary>
    /// Gets the per-triangle normals.
    /// </summary>
    public IReadOnlyList<Vector3> Normals => _normals;

    /// <summary>
    /// Indicates whether the mesh is valid (watertight and manifold).
    /// </summary>
    public bool IsValid { get; private set; }

    /// <summary>
    /// Gets the bounding box minimum point.
    /// </summary>
    public Vector3 BoundsMin { get; private set; }

    /// <summary>
    /// Gets the bounding box maximum point.
    /// </summary>
    public Vector3 BoundsMax { get; private set; }

    public TerrainMesh()
    {
        _vertices = new List<Vector3>();
        _triangles = new List<Triangle>();
        _normals = new List<Vector3>();
        IsValid = false;
    }

    /// <summary>
    /// Adds a vertex to the mesh.
    /// </summary>
    /// <returns>The index of the added vertex.</returns>
    public int AddVertex(Vector3 vertex)
    {
        _vertices.Add(vertex);
        return _vertices.Count - 1;
    }

    /// <summary>
    /// Adds a vertex to the mesh.
    /// </summary>
    public int AddVertex(float x, float y, float z)
    {
        return AddVertex(new Vector3(x, y, z));
    }

    /// <summary>
    /// Adds a triangle to the mesh.
    /// </summary>
    public void AddTriangle(Triangle triangle)
    {
        _triangles.Add(triangle);
    }

    /// <summary>
    /// Adds a triangle to the mesh using vertex indices.
    /// </summary>
    public void AddTriangle(int v0, int v1, int v2)
    {
        _triangles.Add(new Triangle(v0, v1, v2));
    }

    /// <summary>
    /// Calculates normals for all triangles.
    /// </summary>
    public void CalculateNormals()
    {
        _normals.Clear();

        foreach (var tri in _triangles)
        {
            var v0 = _vertices[tri.V0];
            var v1 = _vertices[tri.V1];
            var v2 = _vertices[tri.V2];

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var normal = Vector3.Cross(edge1, edge2).Normalize();

            _normals.Add(normal);
        }
    }

    /// <summary>
    /// Updates the bounding box based on current vertices.
    /// </summary>
    public void UpdateBounds()
    {
        if (_vertices.Count == 0)
        {
            BoundsMin = Vector3.Zero;
            BoundsMax = Vector3.Zero;
            return;
        }

        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

        foreach (var v in _vertices)
        {
            minX = Math.Min(minX, v.X);
            minY = Math.Min(minY, v.Y);
            minZ = Math.Min(minZ, v.Z);
            maxX = Math.Max(maxX, v.X);
            maxY = Math.Max(maxY, v.Y);
            maxZ = Math.Max(maxZ, v.Z);
        }

        BoundsMin = new Vector3(minX, minY, minZ);
        BoundsMax = new Vector3(maxX, maxY, maxZ);
    }

    /// <summary>
    /// Validates the mesh for manifold geometry and proper normals.
    /// </summary>
    public MeshValidationResult Validate()
    {
        var result = new MeshValidationResult();

        if (_vertices.Count < 3)
        {
            result.AddError("Mesh has fewer than 3 vertices.");
            IsValid = false;
            return result;
        }

        if (_triangles.Count < 1)
        {
            result.AddError("Mesh has no triangles.");
            IsValid = false;
            return result;
        }

        // Check for degenerate triangles
        foreach (var tri in _triangles)
        {
            if (tri.V0 < 0 || tri.V0 >= _vertices.Count ||
                tri.V1 < 0 || tri.V1 >= _vertices.Count ||
                tri.V2 < 0 || tri.V2 >= _vertices.Count)
            {
                result.AddError($"Triangle references invalid vertex index.");
            }

            if (tri.V0 == tri.V1 || tri.V1 == tri.V2 || tri.V0 == tri.V2)
            {
                result.AddWarning("Degenerate triangle detected (duplicate vertex indices).");
            }
        }

        // Check edge manifold property (each edge should be shared by exactly 2 triangles)
        var edgeCounts = new Dictionary<(int, int), int>();
        foreach (var tri in _triangles)
        {
            CountEdge(edgeCounts, tri.V0, tri.V1);
            CountEdge(edgeCounts, tri.V1, tri.V2);
            CountEdge(edgeCounts, tri.V2, tri.V0);
        }

        int nonManifoldEdges = 0;
        int boundaryEdges = 0;
        foreach (var kvp in edgeCounts)
        {
            if (kvp.Value == 1)
                boundaryEdges++;
            else if (kvp.Value > 2)
                nonManifoldEdges++;
        }

        if (boundaryEdges > 0)
        {
            result.AddWarning($"Mesh has {boundaryEdges} boundary edges (not watertight).");
        }

        if (nonManifoldEdges > 0)
        {
            result.AddError($"Mesh has {nonManifoldEdges} non-manifold edges.");
        }

        IsValid = !result.HasErrors && boundaryEdges == 0;
        result.IsWatertight = boundaryEdges == 0;
        result.IsManifold = nonManifoldEdges == 0;

        return result;
    }

    private static void CountEdge(Dictionary<(int, int), int> edgeCounts, int v1, int v2)
    {
        var edge = v1 < v2 ? (v1, v2) : (v2, v1);
        edgeCounts.TryGetValue(edge, out int count);
        edgeCounts[edge] = count + 1;
    }

    /// <summary>
    /// Gets the number of vertices.
    /// </summary>
    public int VertexCount => _vertices.Count;

    /// <summary>
    /// Gets the number of triangles.
    /// </summary>
    public int TriangleCount => _triangles.Count;
}

/// <summary>
/// Result of mesh validation.
/// </summary>
public class MeshValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool IsWatertight { get; set; }
    public bool IsManifold { get; set; }
    public bool HasErrors => Errors.Count > 0;

    public void AddError(string message) => Errors.Add(message);
    public void AddWarning(string message) => Warnings.Add(message);
}
