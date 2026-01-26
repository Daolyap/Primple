using System.Numerics;

namespace Primple.Core.Models;

/// <summary>
/// Represents a 3D mesh for terrain visualization and export.
/// </summary>
public class TerrainMesh
{
    public List<Vector3> Vertices { get; set; } = new();
    public List<int> Indices { get; set; } = new();
    public List<Vector3> Normals { get; set; } = new();
    public List<Vector3> VertexColors { get; set; } = new();

    public int TriangleCount => Indices.Count / 3;
    public int VertexCount => Vertices.Count;

    /// <summary>
    /// Gets the bounding box of the mesh.
    /// </summary>
    public (Vector3 Min, Vector3 Max) GetBounds()
    {
        if (Vertices.Count == 0)
            return (Vector3.Zero, Vector3.Zero);

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var v in Vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        return (min, max);
    }

    /// <summary>
    /// Gets the dimensions of the mesh in mm.
    /// </summary>
    public Vector3 GetDimensions()
    {
        var (min, max) = GetBounds();
        return max - min;
    }

    /// <summary>
    /// Calculate normals for the mesh.
    /// </summary>
    public void CalculateNormals()
    {
        Normals = new List<Vector3>(new Vector3[Vertices.Count]);

        for (int i = 0; i < Indices.Count; i += 3)
        {
            var i0 = Indices[i];
            var i1 = Indices[i + 1];
            var i2 = Indices[i + 2];

            var v0 = Vertices[i0];
            var v1 = Vertices[i1];
            var v2 = Vertices[i2];

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));

            Normals[i0] += normal;
            Normals[i1] += normal;
            Normals[i2] += normal;
        }

        for (int i = 0; i < Normals.Count; i++)
        {
            Normals[i] = Vector3.Normalize(Normals[i]);
        }
    }

    /// <summary>
    /// Validates the mesh for 3D printing compatibility.
    /// </summary>
    public MeshValidationResult Validate()
    {
        var result = new MeshValidationResult();

        // Check for empty mesh
        if (Vertices.Count == 0 || Indices.Count == 0)
        {
            result.Errors.Add("Mesh is empty");
            return result;
        }

        // Check for proper triangle count
        if (Indices.Count % 3 != 0)
        {
            result.Errors.Add("Index count is not a multiple of 3");
        }

        // Check for invalid indices
        foreach (var idx in Indices)
        {
            if (idx < 0 || idx >= Vertices.Count)
            {
                result.Errors.Add($"Invalid vertex index: {idx}");
                break;
            }
        }

        // Check for degenerate triangles
        int degenerateCount = 0;
        for (int i = 0; i < Indices.Count; i += 3)
        {
            var v0 = Vertices[Indices[i]];
            var v1 = Vertices[Indices[i + 1]];
            var v2 = Vertices[Indices[i + 2]];

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var area = Vector3.Cross(edge1, edge2).Length() / 2;

            if (area < 1e-10f)
            {
                degenerateCount++;
            }
        }

        if (degenerateCount > 0)
        {
            result.Warnings.Add($"Found {degenerateCount} degenerate triangles");
        }

        // Check dimensions
        var dims = GetDimensions();
        if (dims.X < 1 || dims.Y < 1 || dims.Z < 0.5f)
        {
            result.Warnings.Add("Model dimensions may be too small for printing");
        }

        result.IsValid = result.Errors.Count == 0;
        result.TriangleCount = TriangleCount;
        result.VertexCount = VertexCount;
        result.Dimensions = dims;

        return result;
    }

    /// <summary>
    /// Estimates file size in bytes for STL export.
    /// </summary>
    public long EstimateStlFileSize(bool binary = true)
    {
        if (binary)
        {
            // Binary STL: 80 bytes header + 4 bytes triangle count + 50 bytes per triangle
            return 84 + (TriangleCount * 50);
        }
        else
        {
            // ASCII STL: approximately 300 bytes per triangle
            return TriangleCount * 300;
        }
    }

    /// <summary>
    /// Estimates print time in minutes.
    /// </summary>
    public double EstimatePrintTime(PrinterProfile profile)
    {
        var dims = GetDimensions();
        var volume = dims.X * dims.Y * dims.Z; // mm³

        // Simplified estimation based on layer height and print speed
        var layerCount = dims.Z / profile.LayerHeight;
        var layerArea = dims.X * dims.Y;

        // Account for infill
        var actualVolume = volume * (profile.InfillPercentage / 100.0);
        var shellVolume = (dims.X * dims.Y * 2 + dims.X * dims.Z * 2 + dims.Y * dims.Z * 2) * 0.8; // shell thickness
        var totalVolume = actualVolume + shellVolume;

        // Estimate based on volume and print speed (very rough)
        var minutes = (totalVolume / (profile.PrintSpeed * 60)) * 2; // multiply by factor for path planning overhead

        return Math.Max(5, minutes); // minimum 5 minutes
    }

    /// <summary>
    /// Estimates filament usage in grams.
    /// </summary>
    public double EstimateFilamentUsage(PrinterProfile profile)
    {
        var dims = GetDimensions();
        var volume = dims.X * dims.Y * dims.Z; // mm³

        // Account for infill
        var actualVolume = volume * (profile.InfillPercentage / 100.0);
        var shellVolume = (dims.X * dims.Y * 2 + dims.X * dims.Z * 2 + dims.Y * dims.Z * 2) * 0.8;
        var totalVolume = actualVolume + shellVolume;

        // PLA density ~1.25 g/cm³
        var density = profile.Material switch
        {
            Enums.MaterialType.PLA => 1.25,
            Enums.MaterialType.PETG => 1.27,
            Enums.MaterialType.ASA => 1.05,
            Enums.MaterialType.ABS => 1.04,
            Enums.MaterialType.TPU => 1.21,
            _ => 1.25
        };

        return (totalVolume / 1000) * density; // convert mm³ to cm³ and multiply by density
    }
}

/// <summary>
/// Result of mesh validation.
/// </summary>
public class MeshValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public int TriangleCount { get; set; }
    public int VertexCount { get; set; }
    public Vector3 Dimensions { get; set; }
}
