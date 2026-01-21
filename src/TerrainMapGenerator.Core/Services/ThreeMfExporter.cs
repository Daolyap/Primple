using System.IO.Compression;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Services;

/// <summary>
/// Service for exporting meshes to 3MF format with multi-material and color support.
/// </summary>
public class ThreeMfExporter : I3MfExporter
{
    private readonly ILogger<ThreeMfExporter> _logger;

    public ThreeMfExporter() : this(NullLogger<ThreeMfExporter>.Instance) { }

    public ThreeMfExporter(ILogger<ThreeMfExporter> logger)
    {
        _logger = logger;
    }

    public async Task ExportAsync(TerrainMesh mesh, string filePath)
    {
        await Task.Run(() => Export(mesh, filePath));
    }

    public void Export(TerrainMesh mesh, string filePath)
    {
        Export(mesh, filePath, new ThreeMfExportOptions());
    }

    public async Task ExportAsync(TerrainMesh mesh, string filePath, ThreeMfExportOptions options)
    {
        await Task.Run(() => Export(mesh, filePath, options));
    }

    public void Export(TerrainMesh mesh, string filePath, ThreeMfExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(options);

        if (mesh.TriangleCount == 0)
            throw new InvalidOperationException("Cannot export empty mesh.");

        _logger.LogInformation("Exporting mesh to 3MF: {FilePath} ({VertexCount} vertices, {TriangleCount} triangles)",
            filePath, mesh.VertexCount, mesh.TriangleCount);

        var bytes = ExportToBytes(mesh, options);
        File.WriteAllBytes(filePath, bytes);

        _logger.LogInformation("3MF export complete: {FileSize} bytes", bytes.Length);
    }

    public byte[] ExportToBytes(TerrainMesh mesh)
    {
        return ExportToBytes(mesh, new ThreeMfExportOptions());
    }

    public byte[] ExportToBytes(TerrainMesh mesh, ThreeMfExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(options);

        if (mesh.TriangleCount == 0)
            throw new InvalidOperationException("Cannot export empty mesh.");

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // Add content types
            AddContentTypes(archive);

            // Add relationships
            AddRelationships(archive);

            // Add 3D model
            Add3DModel(archive, mesh, options);
        }

        return memoryStream.ToArray();
    }

    private static void AddContentTypes(ZipArchive archive)
    {
        var entry = archive.CreateEntry("[Content_Types].xml", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);

        writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""model"" ContentType=""application/vnd.ms-package.3dmanufacturing-3dmodel+xml""/>
</Types>");
    }

    private static void AddRelationships(ZipArchive archive)
    {
        var entry = archive.CreateEntry("_rels/.rels", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);

        writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Target=""/3D/3dmodel.model"" Id=""rel0"" Type=""http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel""/>
</Relationships>");
    }

    private void Add3DModel(ZipArchive archive, TerrainMesh mesh, ThreeMfExportOptions options)
    {
        var entry = archive.CreateEntry("3D/3dmodel.model", CompressionLevel.Optimal);
        using var stream = entry.Open();

        var settings = new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            Indent = true
        };

        using var writer = XmlWriter.Create(stream, settings);

        writer.WriteStartDocument();
        writer.WriteStartElement("model", "http://schemas.microsoft.com/3dmanufacturing/core/2015/02");
        writer.WriteAttributeString("unit", options.Unit);
        writer.WriteAttributeString("xml", "lang", null, "en-US");

        // Add metadata
        writer.WriteStartElement("metadata");
        writer.WriteAttributeString("name", "Title");
        writer.WriteString(options.Title);
        writer.WriteEndElement();

        writer.WriteStartElement("metadata");
        writer.WriteAttributeString("name", "Designer");
        writer.WriteString("Terrain Map Generator");
        writer.WriteEndElement();

        // Resources section
        writer.WriteStartElement("resources");

        // If colors are enabled, add base materials
        if (options.EnableColors && options.ColorGradient.Count > 0)
        {
            writer.WriteStartElement("basematerials");
            writer.WriteAttributeString("id", "1");

            foreach (var colorStop in options.ColorGradient)
            {
                writer.WriteStartElement("base");
                writer.WriteAttributeString("name", $"Elevation_{colorStop.Position:F2}");
                writer.WriteAttributeString("displaycolor", colorStop.Color.ToHex());
                writer.WriteEndElement();
            }

            writer.WriteEndElement(); // basematerials
        }

        // Object (mesh)
        writer.WriteStartElement("object");
        writer.WriteAttributeString("id", "2");
        writer.WriteAttributeString("type", "model");
        writer.WriteAttributeString("name", options.Title);

        writer.WriteStartElement("mesh");

        // Vertices
        writer.WriteStartElement("vertices");
        foreach (var vertex in mesh.Vertices)
        {
            writer.WriteStartElement("vertex");
            writer.WriteAttributeString("x", vertex.X.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteAttributeString("y", vertex.Y.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteAttributeString("z", vertex.Z.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }
        writer.WriteEndElement(); // vertices

        // Triangles
        writer.WriteStartElement("triangles");

        // Calculate elevation range for color mapping
        mesh.UpdateBounds();
        float minZ = mesh.BoundsMin.Z;
        float maxZ = mesh.BoundsMax.Z;
        float zRange = maxZ - minZ;

        foreach (var triangle in mesh.Triangles)
        {
            writer.WriteStartElement("triangle");
            writer.WriteAttributeString("v1", triangle.V0.ToString());
            writer.WriteAttributeString("v2", triangle.V1.ToString());
            writer.WriteAttributeString("v3", triangle.V2.ToString());

            // Add color index if colors are enabled
            if (options.EnableColors && options.ColorGradient.Count > 0 && zRange > 0)
            {
                // Calculate average elevation of triangle
                var avgZ = (mesh.Vertices[triangle.V0].Z + 
                           mesh.Vertices[triangle.V1].Z + 
                           mesh.Vertices[triangle.V2].Z) / 3.0f;
                var normalizedZ = (avgZ - minZ) / zRange;
                var colorIndex = GetColorIndex(normalizedZ, options.ColorGradient);

                writer.WriteAttributeString("pid", "1");
                writer.WriteAttributeString("p1", colorIndex.ToString());
            }

            writer.WriteEndElement();
        }
        writer.WriteEndElement(); // triangles

        writer.WriteEndElement(); // mesh
        writer.WriteEndElement(); // object
        writer.WriteEndElement(); // resources

        // Build section
        writer.WriteStartElement("build");
        writer.WriteStartElement("item");
        writer.WriteAttributeString("objectid", "2");
        writer.WriteEndElement();
        writer.WriteEndElement(); // build

        writer.WriteEndElement(); // model
        writer.WriteEndDocument();
    }

    private static int GetColorIndex(float normalizedValue, List<ColorStop> gradient)
    {
        if (gradient.Count == 0)
            return 0;

        // Sort gradient by position to ensure correct processing
        var sortedGradient = gradient.OrderBy(g => g.Position).ToList();
        
        normalizedValue = Math.Clamp(normalizedValue, 0, 1);

        for (int i = 0; i < sortedGradient.Count - 1; i++)
        {
            if (normalizedValue <= sortedGradient[i + 1].Position)
            {
                // Return the closest color stop index
                var midpoint = (sortedGradient[i].Position + sortedGradient[i + 1].Position) / 2;
                return normalizedValue < midpoint ? i : i + 1;
            }
        }

        return sortedGradient.Count - 1;
    }
}
