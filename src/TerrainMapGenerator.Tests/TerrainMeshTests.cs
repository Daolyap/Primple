using TerrainMapGenerator.Core.Models;
using Xunit;

namespace TerrainMapGenerator.Tests;

public class TerrainMeshTests
{
    [Fact]
    public void AddVertex_ReturnsCorrectIndex()
    {
        var mesh = new TerrainMesh();

        var idx0 = mesh.AddVertex(0, 0, 0);
        var idx1 = mesh.AddVertex(1, 0, 0);
        var idx2 = mesh.AddVertex(0, 1, 0);

        Assert.Equal(0, idx0);
        Assert.Equal(1, idx1);
        Assert.Equal(2, idx2);
    }

    [Fact]
    public void AddTriangle_IncreasesCount()
    {
        var mesh = new TerrainMesh();
        mesh.AddVertex(0, 0, 0);
        mesh.AddVertex(1, 0, 0);
        mesh.AddVertex(0, 1, 0);

        mesh.AddTriangle(0, 1, 2);

        Assert.Equal(1, mesh.TriangleCount);
    }

    [Fact]
    public void CalculateNormals_CalculatesCorrectNormal()
    {
        var mesh = new TerrainMesh();

        // Create a triangle in the XY plane, facing up (Z+)
        mesh.AddVertex(0, 0, 0);
        mesh.AddVertex(1, 0, 0);
        mesh.AddVertex(0, 1, 0);
        mesh.AddTriangle(0, 1, 2);

        mesh.CalculateNormals();

        Assert.Single(mesh.Normals);
        var normal = mesh.Normals[0];

        // Normal should point up (0, 0, 1) or down depending on winding
        Assert.True(Math.Abs(normal.X) < 0.001f);
        Assert.True(Math.Abs(normal.Y) < 0.001f);
        Assert.True(Math.Abs(Math.Abs(normal.Z) - 1.0f) < 0.001f);
    }

    [Fact]
    public void UpdateBounds_CalculatesCorrectBounds()
    {
        var mesh = new TerrainMesh();

        mesh.AddVertex(-10, -5, 0);
        mesh.AddVertex(20, 30, 50);
        mesh.AddVertex(5, 15, 25);

        mesh.UpdateBounds();

        Assert.Equal(-10, mesh.BoundsMin.X, 0.001f);
        Assert.Equal(-5, mesh.BoundsMin.Y, 0.001f);
        Assert.Equal(0, mesh.BoundsMin.Z, 0.001f);

        Assert.Equal(20, mesh.BoundsMax.X, 0.001f);
        Assert.Equal(30, mesh.BoundsMax.Y, 0.001f);
        Assert.Equal(50, mesh.BoundsMax.Z, 0.001f);
    }

    [Fact]
    public void Validate_EmptyMesh_ReturnsErrors()
    {
        var mesh = new TerrainMesh();

        var result = mesh.Validate();

        Assert.True(result.HasErrors);
        Assert.False(mesh.IsValid);
    }

    [Fact]
    public void Validate_NoTriangles_ReturnsError()
    {
        var mesh = new TerrainMesh();
        mesh.AddVertex(0, 0, 0);
        mesh.AddVertex(1, 0, 0);
        mesh.AddVertex(0, 1, 0);

        var result = mesh.Validate();

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Validate_SingleTriangle_NotWatertight()
    {
        var mesh = new TerrainMesh();
        mesh.AddVertex(0, 0, 0);
        mesh.AddVertex(1, 0, 0);
        mesh.AddVertex(0, 1, 0);
        mesh.AddTriangle(0, 1, 2);

        var result = mesh.Validate();

        Assert.False(result.IsWatertight);
        Assert.True(result.IsManifold);
    }

    [Fact]
    public void Validate_ClosedTetrahedron_IsWatertight()
    {
        var mesh = new TerrainMesh();

        // Create a tetrahedron (4 triangular faces)
        mesh.AddVertex(0, 0, 0);     // 0
        mesh.AddVertex(1, 0, 0);     // 1
        mesh.AddVertex(0.5f, 1, 0);  // 2
        mesh.AddVertex(0.5f, 0.5f, 1); // 3

        // Bottom face
        mesh.AddTriangle(0, 2, 1);
        // Front face
        mesh.AddTriangle(0, 1, 3);
        // Left face
        mesh.AddTriangle(0, 3, 2);
        // Right face
        mesh.AddTriangle(1, 2, 3);

        var result = mesh.Validate();

        Assert.True(result.IsWatertight, "Closed tetrahedron should be watertight");
        Assert.True(result.IsManifold, "Closed tetrahedron should be manifold");
    }

    [Fact]
    public void Validate_DegenerateTriangle_ReturnsWarning()
    {
        var mesh = new TerrainMesh();
        mesh.AddVertex(0, 0, 0);
        mesh.AddVertex(1, 0, 0);
        mesh.AddVertex(0, 1, 0);

        // Degenerate triangle with duplicate vertex index
        mesh.AddTriangle(0, 0, 1);

        var result = mesh.Validate();

        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Vertices_Property_ReturnsReadOnlyList()
    {
        var mesh = new TerrainMesh();
        mesh.AddVertex(0, 0, 0);
        mesh.AddVertex(1, 0, 0);

        var vertices = mesh.Vertices;

        Assert.Equal(2, vertices.Count);
        Assert.IsAssignableFrom<IReadOnlyList<Vector3>>(vertices);
    }

    [Fact]
    public void Triangles_Property_ReturnsReadOnlyList()
    {
        var mesh = new TerrainMesh();
        mesh.AddVertex(0, 0, 0);
        mesh.AddVertex(1, 0, 0);
        mesh.AddVertex(0, 1, 0);
        mesh.AddTriangle(0, 1, 2);

        var triangles = mesh.Triangles;

        Assert.Single(triangles);
        Assert.IsAssignableFrom<IReadOnlyList<Triangle>>(triangles);
    }
}
