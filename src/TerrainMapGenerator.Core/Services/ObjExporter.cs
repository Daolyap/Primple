using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Services;

/// <summary>
/// Service for exporting meshes to OBJ format with MTL material support.
/// </summary>
public class ObjExporter : IObjExporter
{
    private readonly ILogger<ObjExporter> _logger;

    public ObjExporter() : this(NullLogger<ObjExporter>.Instance) { }

    public ObjExporter(ILogger<ObjExporter> logger)
    {
        _logger = logger;
    }

    public async Task ExportAsync(TerrainMesh mesh, string filePath)
    {
        await Task.Run(() => Export(mesh, filePath));
    }

    public void Export(TerrainMesh mesh, string filePath)
    {
        Export(mesh, filePath, new ObjExportOptions());
    }

    public async Task ExportAsync(TerrainMesh mesh, string filePath, ObjExportOptions options)
    {
        await Task.Run(() => Export(mesh, filePath, options));
    }

    public void Export(TerrainMesh mesh, string filePath, ObjExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(options);

        if (mesh.TriangleCount == 0)
            throw new InvalidOperationException("Cannot export empty mesh.");

        _logger.LogInformation("Exporting mesh to OBJ: {FilePath} ({VertexCount} vertices, {TriangleCount} triangles)",
            filePath, mesh.VertexCount, mesh.TriangleCount);

        // Ensure normals are calculated if needed
        if (options.IncludeNormals && mesh.Normals.Count != mesh.TriangleCount)
        {
            mesh.CalculateNormals();
        }

        var objContent = GenerateObjContent(mesh, options);
        File.WriteAllText(filePath, objContent, Encoding.ASCII);

        // Generate MTL file if requested
        if (options.GenerateMtl)
        {
            var mtlPath = Path.ChangeExtension(filePath, ".mtl");
            var mtlContent = GenerateMtlContent(options);
            File.WriteAllText(mtlPath, mtlContent, Encoding.ASCII);
            _logger.LogInformation("MTL file created: {MtlPath}", mtlPath);
        }

        _logger.LogInformation("OBJ export complete");
    }

    public string ExportToString(TerrainMesh mesh)
    {
        return GenerateObjContent(mesh, new ObjExportOptions { GenerateMtl = false });
    }

    private string GenerateObjContent(TerrainMesh mesh, ObjExportOptions options)
    {
        var sb = new StringBuilder();

        // Header comment
        sb.AppendLine("# Terrain Map Generator - OBJ Export");
        sb.AppendLine($"# Vertices: {mesh.VertexCount}");
        sb.AppendLine($"# Triangles: {mesh.TriangleCount}");
        sb.AppendLine();

        // Reference MTL file if generating
        if (options.GenerateMtl)
        {
            sb.AppendLine($"mtllib {options.ObjectName}.mtl");
            sb.AppendLine();
        }

        // Object name
        sb.AppendLine($"o {options.ObjectName}");
        sb.AppendLine();

        // Vertices
        sb.AppendLine("# Vertices");
        foreach (var vertex in mesh.Vertices)
        {
            sb.AppendLine($"v {FormatFloat(vertex.X)} {FormatFloat(vertex.Y)} {FormatFloat(vertex.Z)}");
        }
        sb.AppendLine();

        // Vertex normals (if included)
        if (options.IncludeNormals && mesh.Normals.Count > 0)
        {
            sb.AppendLine("# Vertex Normals");
            foreach (var normal in mesh.Normals)
            {
                sb.AppendLine($"vn {FormatFloat(normal.X)} {FormatFloat(normal.Y)} {FormatFloat(normal.Z)}");
            }
            sb.AppendLine();
        }

        // Texture coordinates (if included)
        if (options.IncludeTextureCoords)
        {
            sb.AppendLine("# Texture Coordinates");
            mesh.UpdateBounds();
            var bounds = mesh.BoundsMax - mesh.BoundsMin;
            
            foreach (var vertex in mesh.Vertices)
            {
                // Map X, Y coordinates to UV (0-1)
                float u = bounds.X > 0 ? (vertex.X - mesh.BoundsMin.X) / bounds.X : 0;
                float v = bounds.Y > 0 ? (vertex.Y - mesh.BoundsMin.Y) / bounds.Y : 0;
                sb.AppendLine($"vt {FormatFloat(u)} {FormatFloat(v)}");
            }
            sb.AppendLine();
        }

        // Use material
        if (options.GenerateMtl)
        {
            sb.AppendLine($"usemtl {options.MaterialName}");
        }

        // Faces
        sb.AppendLine("# Faces");
        for (int i = 0; i < mesh.TriangleCount; i++)
        {
            var tri = mesh.Triangles[i];
            
            // OBJ indices are 1-based
            int v0 = tri.V0 + 1;
            int v1 = tri.V1 + 1;
            int v2 = tri.V2 + 1;

            if (options.IncludeNormals && options.IncludeTextureCoords)
            {
                // f v/vt/vn format - normal index matches triangle index (1-based)
                int ni = i + 1;
                sb.AppendLine($"f {v0}/{v0}/{ni} {v1}/{v1}/{ni} {v2}/{v2}/{ni}");
            }
            else if (options.IncludeNormals)
            {
                // f v//vn format
                int ni = i + 1;
                sb.AppendLine($"f {v0}//{ni} {v1}//{ni} {v2}//{ni}");
            }
            else if (options.IncludeTextureCoords)
            {
                // f v/vt format
                sb.AppendLine($"f {v0}/{v0} {v1}/{v1} {v2}/{v2}");
            }
            else
            {
                // f v format
                sb.AppendLine($"f {v0} {v1} {v2}");
            }
        }

        return sb.ToString();
    }

    public string GenerateMtlContent(ObjExportOptions options)
    {
        var sb = new StringBuilder();
        var mat = options.Material;

        sb.AppendLine("# Terrain Map Generator - MTL Material File");
        sb.AppendLine();
        sb.AppendLine($"newmtl {options.MaterialName}");
        sb.AppendLine($"Ka {FormatFloat(mat.AmbientColor.R)} {FormatFloat(mat.AmbientColor.G)} {FormatFloat(mat.AmbientColor.B)}");
        sb.AppendLine($"Kd {FormatFloat(mat.DiffuseColor.R)} {FormatFloat(mat.DiffuseColor.G)} {FormatFloat(mat.DiffuseColor.B)}");
        sb.AppendLine($"Ks {FormatFloat(mat.SpecularColor.R)} {FormatFloat(mat.SpecularColor.G)} {FormatFloat(mat.SpecularColor.B)}");
        sb.AppendLine($"Ns {FormatFloat(mat.SpecularExponent)}");
        sb.AppendLine($"d {FormatFloat(mat.Dissolve)}");
        sb.AppendLine($"illum {mat.IlluminationModel}");

        if (!string.IsNullOrEmpty(mat.TextureMap))
        {
            sb.AppendLine($"map_Kd {mat.TextureMap}");
        }

        return sb.ToString();
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("F6", CultureInfo.InvariantCulture);
    }
}
