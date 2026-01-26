using System.Globalization;
using System.Numerics;
using System.Text;
using Primple.Core.Interfaces;
using Primple.Core.Models;

namespace Primple.Core.Services;

/// <summary>
/// Service for exporting terrain meshes to various 3D file formats.
/// </summary>
public class ExportService : IExportService
{
    /// <summary>
    /// Exports mesh to STL format.
    /// </summary>
    public async Task ExportStlAsync(TerrainMesh mesh, string filePath, bool binary = true, CancellationToken cancellationToken = default)
    {
        if (binary)
        {
            await ExportBinaryStlAsync(mesh, filePath, cancellationToken);
        }
        else
        {
            await ExportAsciiStlAsync(mesh, filePath, cancellationToken);
        }
    }

    private async Task ExportBinaryStlAsync(TerrainMesh mesh, string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await using var writer = new BinaryWriter(stream);

        // 80-byte header
        var header = new byte[80];
        var headerText = Encoding.ASCII.GetBytes("Primple Terrain Generator - Binary STL");
        Array.Copy(headerText, header, Math.Min(headerText.Length, 80));
        await stream.WriteAsync(header, cancellationToken);

        // Triangle count (4 bytes)
        writer.Write((uint)mesh.TriangleCount);

        // Write triangles
        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var v0 = mesh.Vertices[mesh.Indices[i]];
            var v1 = mesh.Vertices[mesh.Indices[i + 1]];
            var v2 = mesh.Vertices[mesh.Indices[i + 2]];

            // Calculate face normal
            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));

            // Normal
            writer.Write(normal.X);
            writer.Write(normal.Y);
            writer.Write(normal.Z);

            // Vertices
            writer.Write(v0.X);
            writer.Write(v0.Y);
            writer.Write(v0.Z);

            writer.Write(v1.X);
            writer.Write(v1.Y);
            writer.Write(v1.Z);

            writer.Write(v2.X);
            writer.Write(v2.Y);
            writer.Write(v2.Z);

            // Attribute byte count (unused)
            writer.Write((ushort)0);
        }
    }

    private async Task ExportAsciiStlAsync(TerrainMesh mesh, string filePath, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("solid primple_terrain");

        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var v0 = mesh.Vertices[mesh.Indices[i]];
            var v1 = mesh.Vertices[mesh.Indices[i + 1]];
            var v2 = mesh.Vertices[mesh.Indices[i + 2]];

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  facet normal {0:G} {1:G} {2:G}", normal.X, normal.Y, normal.Z));
            sb.AppendLine("    outer loop");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      vertex {0:G} {1:G} {2:G}", v0.X, v0.Y, v0.Z));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      vertex {0:G} {1:G} {2:G}", v1.X, v1.Y, v1.Z));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      vertex {0:G} {1:G} {2:G}", v2.X, v2.Y, v2.Z));
            sb.AppendLine("    endloop");
            sb.AppendLine("  endfacet");
        }

        sb.AppendLine("endsolid primple_terrain");

        await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);
    }

    /// <summary>
    /// Exports mesh to 3MF format with color data.
    /// </summary>
    public async Task Export3MFAsync(TerrainMesh mesh, string filePath, TerrainProject project, CancellationToken cancellationToken = default)
    {
        // 3MF is a ZIP-based format containing XML files
        using var archive = new System.IO.Compression.ZipArchive(
            new FileStream(filePath, FileMode.Create),
            System.IO.Compression.ZipArchiveMode.Create);

        // Create the main model XML
        var modelXml = Generate3MFModelXml(mesh, project);

        var modelEntry = archive.CreateEntry("3D/3dmodel.model");
        await using (var entryStream = modelEntry.Open())
        await using (var writer = new StreamWriter(entryStream))
        {
            await writer.WriteAsync(modelXml);
        }

        // Create content types
        var contentTypesXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml"" />
  <Default Extension=""model"" ContentType=""application/vnd.ms-package.3dmanufacturing-3dmodel+xml"" />
</Types>";

        var contentTypesEntry = archive.CreateEntry("[Content_Types].xml");
        await using (var entryStream = contentTypesEntry.Open())
        await using (var writer = new StreamWriter(entryStream))
        {
            await writer.WriteAsync(contentTypesXml);
        }

        // Create relationships
        var relsXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Target=""/3D/3dmodel.model"" Id=""rel0"" Type=""http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel"" />
</Relationships>";

        var relsEntry = archive.CreateEntry("_rels/.rels");
        await using (var entryStream = relsEntry.Open())
        await using (var writer = new StreamWriter(entryStream))
        {
            await writer.WriteAsync(relsXml);
        }
    }

    private string Generate3MFModelXml(TerrainMesh mesh, TerrainProject project)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
        sb.AppendLine(@"<model unit=""millimeter"" xml:lang=""en-US"" xmlns=""http://schemas.microsoft.com/3dmanufacturing/core/2015/02"">");
        sb.AppendLine(@"  <metadata name=""Title"">" + EscapeXml(project.Name) + "</metadata>");
        sb.AppendLine(@"  <metadata name=""Designer"">Primple Terrain Generator</metadata>");
        sb.AppendLine(@"  <metadata name=""Description"">" + EscapeXml(project.Description) + "</metadata>");
        sb.AppendLine(@"  <metadata name=""CreationDate"">" + project.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ") + "</metadata>");
        sb.AppendLine(@"  <resources>");
        sb.AppendLine(@"    <object id=""1"" type=""model"">");
        sb.AppendLine(@"      <mesh>");

        // Vertices
        sb.AppendLine(@"        <vertices>");
        foreach (var vertex in mesh.Vertices)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                @"          <vertex x=""{0:G9}"" y=""{1:G9}"" z=""{2:G9}"" />",
                vertex.X, vertex.Y, vertex.Z));
        }
        sb.AppendLine(@"        </vertices>");

        // Triangles
        sb.AppendLine(@"        <triangles>");
        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                @"          <triangle v1=""{0}"" v2=""{1}"" v3=""{2}"" />",
                mesh.Indices[i], mesh.Indices[i + 1], mesh.Indices[i + 2]));
        }
        sb.AppendLine(@"        </triangles>");

        sb.AppendLine(@"      </mesh>");
        sb.AppendLine(@"    </object>");
        sb.AppendLine(@"  </resources>");
        sb.AppendLine(@"  <build>");
        sb.AppendLine(@"    <item objectid=""1"" />");
        sb.AppendLine(@"  </build>");
        sb.AppendLine(@"</model>");

        return sb.ToString();
    }

    /// <summary>
    /// Exports mesh to OBJ format.
    /// </summary>
    public async Task ExportObjAsync(TerrainMesh mesh, string filePath, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Primple Terrain Generator - OBJ Export");
        sb.AppendLine($"# Vertices: {mesh.VertexCount}");
        sb.AppendLine($"# Triangles: {mesh.TriangleCount}");
        sb.AppendLine();

        // Vertices
        foreach (var vertex in mesh.Vertices)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:G9} {1:G9} {2:G9}", vertex.X, vertex.Y, vertex.Z));
        }

        sb.AppendLine();

        // Normals (if calculated)
        if (mesh.Normals.Count == mesh.Vertices.Count)
        {
            foreach (var normal in mesh.Normals)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "vn {0:G9} {1:G9} {2:G9}", normal.X, normal.Y, normal.Z));
            }
            sb.AppendLine();
        }

        // Faces (OBJ uses 1-based indexing)
        sb.AppendLine("g terrain");
        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var i1 = mesh.Indices[i] + 1;
            var i2 = mesh.Indices[i + 1] + 1;
            var i3 = mesh.Indices[i + 2] + 1;

            if (mesh.Normals.Count == mesh.Vertices.Count)
            {
                sb.AppendLine($"f {i1}//{i1} {i2}//{i2} {i3}//{i3}");
            }
            else
            {
                sb.AppendLine($"f {i1} {i2} {i3}");
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
