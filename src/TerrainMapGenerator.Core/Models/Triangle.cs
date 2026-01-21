namespace TerrainMapGenerator.Core.Models;

/// <summary>
/// Represents a triangle in a 3D mesh with three vertex indices.
/// </summary>
public readonly struct Triangle : IEquatable<Triangle>
{
    public int V0 { get; }
    public int V1 { get; }
    public int V2 { get; }

    public Triangle(int v0, int v1, int v2)
    {
        V0 = v0;
        V1 = v1;
        V2 = v2;
    }

    /// <summary>
    /// Creates a new triangle with reversed winding order (flipped normal).
    /// </summary>
    public Triangle Flip() => new(V0, V2, V1);

    public bool Equals(Triangle other) => V0 == other.V0 && V1 == other.V1 && V2 == other.V2;
    public override bool Equals(object? obj) => obj is Triangle t && Equals(t);
    public override int GetHashCode() => HashCode.Combine(V0, V1, V2);
    public override string ToString() => $"Triangle({V0}, {V1}, {V2})";
}
