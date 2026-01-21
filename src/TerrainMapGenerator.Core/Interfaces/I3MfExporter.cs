using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Interfaces;

/// <summary>
/// Service for exporting meshes to 3MF format with multi-material support.
/// </summary>
public interface I3MfExporter
{
    /// <summary>
    /// Exports a mesh to a 3MF file.
    /// </summary>
    /// <param name="mesh">The mesh to export.</param>
    /// <param name="filePath">The output file path.</param>
    void Export(TerrainMesh mesh, string filePath);

    /// <summary>
    /// Exports a mesh to a 3MF file asynchronously.
    /// </summary>
    Task ExportAsync(TerrainMesh mesh, string filePath);

    /// <summary>
    /// Exports a mesh to a 3MF file with color support.
    /// </summary>
    /// <param name="mesh">The mesh to export.</param>
    /// <param name="filePath">The output file path.</param>
    /// <param name="options">Export options including color mapping.</param>
    void Export(TerrainMesh mesh, string filePath, ThreeMfExportOptions options);

    /// <summary>
    /// Exports a mesh to a 3MF file with color support asynchronously.
    /// </summary>
    Task ExportAsync(TerrainMesh mesh, string filePath, ThreeMfExportOptions options);

    /// <summary>
    /// Exports a mesh to a byte array in 3MF format.
    /// </summary>
    byte[] ExportToBytes(TerrainMesh mesh);

    /// <summary>
    /// Exports a mesh to a byte array with color support.
    /// </summary>
    byte[] ExportToBytes(TerrainMesh mesh, ThreeMfExportOptions options);
}

/// <summary>
/// Options for 3MF export.
/// </summary>
public class ThreeMfExportOptions
{
    /// <summary>
    /// Model title/name for the 3MF package.
    /// </summary>
    public string Title { get; set; } = "Terrain Map";

    /// <summary>
    /// Enable vertex colors for multi-color printing.
    /// </summary>
    public bool EnableColors { get; set; } = false;

    /// <summary>
    /// Color gradient for elevation-based coloring.
    /// Maps normalized elevation (0-1) to colors.
    /// </summary>
    public List<ColorStop> ColorGradient { get; set; } = new()
    {
        new ColorStop(0.0f, new Color(34, 139, 34)),    // Forest green (low)
        new ColorStop(0.3f, new Color(154, 205, 50)),   // Yellow green
        new ColorStop(0.5f, new Color(210, 180, 140)), // Tan
        new ColorStop(0.7f, new Color(139, 90, 43)),   // Brown
        new ColorStop(0.9f, new Color(169, 169, 169)), // Gray
        new ColorStop(1.0f, new Color(255, 255, 255))  // White (peaks)
    };

    /// <summary>
    /// Unit scale (default: millimeter).
    /// </summary>
    public string Unit { get; set; } = "millimeter";
}

/// <summary>
/// Represents a color stop in a gradient.
/// </summary>
public class ColorStop
{
    public float Position { get; set; }
    public Color Color { get; set; }

    public ColorStop(float position, Color color)
    {
        Position = position;
        Color = color;
    }
}

/// <summary>
/// Simple RGB color representation.
/// </summary>
public readonly struct Color
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte A { get; }

    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
    public string ToHexWithAlpha() => $"#{R:X2}{G:X2}{B:X2}{A:X2}";

    public static Color Lerp(Color a, Color b, float t)
    {
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t)
        );
    }
}
