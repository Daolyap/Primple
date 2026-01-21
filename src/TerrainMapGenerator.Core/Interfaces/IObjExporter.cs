using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Interfaces;

/// <summary>
/// Service for exporting meshes to OBJ format with MTL material support.
/// </summary>
public interface IObjExporter
{
    /// <summary>
    /// Exports a mesh to an OBJ file.
    /// </summary>
    /// <param name="mesh">The mesh to export.</param>
    /// <param name="filePath">The output file path (.obj).</param>
    void Export(TerrainMesh mesh, string filePath);

    /// <summary>
    /// Exports a mesh to an OBJ file asynchronously.
    /// </summary>
    Task ExportAsync(TerrainMesh mesh, string filePath);

    /// <summary>
    /// Exports a mesh to an OBJ file with material options.
    /// </summary>
    /// <param name="mesh">The mesh to export.</param>
    /// <param name="filePath">The output file path (.obj).</param>
    /// <param name="options">Export options including material settings.</param>
    void Export(TerrainMesh mesh, string filePath, ObjExportOptions options);

    /// <summary>
    /// Exports a mesh to an OBJ file with material options asynchronously.
    /// </summary>
    Task ExportAsync(TerrainMesh mesh, string filePath, ObjExportOptions options);

    /// <summary>
    /// Exports a mesh to OBJ format as a string.
    /// </summary>
    string ExportToString(TerrainMesh mesh);

    /// <summary>
    /// Generates the MTL material file content.
    /// </summary>
    string GenerateMtlContent(ObjExportOptions options);
}

/// <summary>
/// Options for OBJ export.
/// </summary>
public class ObjExportOptions
{
    /// <summary>
    /// Object name in the OBJ file.
    /// </summary>
    public string ObjectName { get; set; } = "terrain";

    /// <summary>
    /// Whether to generate a companion MTL material file.
    /// </summary>
    public bool GenerateMtl { get; set; } = true;

    /// <summary>
    /// Material name to use.
    /// </summary>
    public string MaterialName { get; set; } = "terrain_material";

    /// <summary>
    /// Whether to include vertex normals (vn).
    /// </summary>
    public bool IncludeNormals { get; set; } = true;

    /// <summary>
    /// Whether to include texture coordinates (vt).
    /// </summary>
    public bool IncludeTextureCoords { get; set; } = false;

    /// <summary>
    /// Material settings for the MTL file.
    /// </summary>
    public ObjMaterial Material { get; set; } = new();
}

/// <summary>
/// Material definition for OBJ/MTL files.
/// </summary>
public class ObjMaterial
{
    /// <summary>
    /// Ambient color (Ka).
    /// </summary>
    public (float R, float G, float B) AmbientColor { get; set; } = (0.2f, 0.2f, 0.2f);

    /// <summary>
    /// Diffuse color (Kd).
    /// </summary>
    public (float R, float G, float B) DiffuseColor { get; set; } = (0.8f, 0.8f, 0.8f);

    /// <summary>
    /// Specular color (Ks).
    /// </summary>
    public (float R, float G, float B) SpecularColor { get; set; } = (0.1f, 0.1f, 0.1f);

    /// <summary>
    /// Specular exponent/shininess (Ns). Range: 0-1000.
    /// </summary>
    public float SpecularExponent { get; set; } = 10.0f;

    /// <summary>
    /// Transparency (d). 1.0 = opaque, 0.0 = transparent.
    /// </summary>
    public float Dissolve { get; set; } = 1.0f;

    /// <summary>
    /// Illumination model (illum).
    /// 0 = Color on, ambient off
    /// 1 = Color on, ambient on
    /// 2 = Highlight on (Phong)
    /// </summary>
    public int IlluminationModel { get; set; } = 2;

    /// <summary>
    /// Optional texture map file path.
    /// </summary>
    public string? TextureMap { get; set; }
}
