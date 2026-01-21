using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Interfaces;

/// <summary>
/// Service for generating text labels (peaks, places, annotations) on terrain meshes.
/// </summary>
public interface ILabelGenerator
{
    /// <summary>
    /// Generates labels for peaks and high points from elevation data.
    /// </summary>
    /// <param name="elevationData">Source elevation data.</param>
    /// <param name="options">Label generation options.</param>
    /// <returns>Generated labels.</returns>
    LabelResult GeneratePeakLabels(ElevationData elevationData, LabelOptions options);

    /// <summary>
    /// Generates labels asynchronously.
    /// </summary>
    Task<LabelResult> GeneratePeakLabelsAsync(ElevationData elevationData, LabelOptions options);

    /// <summary>
    /// Creates a label from text at a specific position.
    /// </summary>
    /// <param name="text">Text to display.</param>
    /// <param name="position">Position on the terrain (X, Y in model coordinates).</param>
    /// <param name="options">Label styling options.</param>
    /// <returns>Generated label.</returns>
    TerrainLabel CreateLabel(string text, (float X, float Y) position, LabelOptions options);

    /// <summary>
    /// Applies labels to a terrain mesh as embossed or debossed text.
    /// </summary>
    /// <param name="mesh">The terrain mesh to modify.</param>
    /// <param name="labels">Labels to apply.</param>
    /// <param name="emboss">True for embossed (raised), false for debossed (sunken).</param>
    /// <returns>Modified mesh with labels.</returns>
    TerrainMesh ApplyLabelsToMesh(TerrainMesh mesh, LabelResult labels, bool emboss = true);

    /// <summary>
    /// Applies a single label to a terrain mesh.
    /// </summary>
    TerrainMesh ApplyLabelToMesh(TerrainMesh mesh, TerrainLabel label, bool emboss = true);
}

/// <summary>
/// Options for label generation.
/// </summary>
public class LabelOptions
{
    /// <summary>
    /// Font size in millimeters.
    /// </summary>
    public float FontSize { get; set; } = 3.0f;

    /// <summary>
    /// Depth/height of embossed/debossed text in millimeters.
    /// </summary>
    public float TextDepth { get; set; } = 0.5f;

    /// <summary>
    /// Font family name. 
    /// Note: Only simple sans-serif style is supported for mesh generation.
    /// </summary>
    public string FontFamily { get; set; } = "Sans";

    /// <summary>
    /// Whether to use bold text.
    /// </summary>
    public bool Bold { get; set; } = false;

    /// <summary>
    /// Text horizontal alignment.
    /// </summary>
    public TextAlignment Alignment { get; set; } = TextAlignment.Center;

    /// <summary>
    /// Maximum number of peak labels to generate.
    /// </summary>
    public int MaxPeakLabels { get; set; } = 10;

    /// <summary>
    /// Minimum prominence for peak detection (in elevation units).
    /// </summary>
    public float MinProminence { get; set; } = 50.0f;

    /// <summary>
    /// Minimum distance between labels in millimeters.
    /// </summary>
    public float MinLabelSpacing { get; set; } = 20.0f;

    /// <summary>
    /// Format string for elevation labels. {0} = elevation value.
    /// </summary>
    public string ElevationFormat { get; set; } = "{0:F0}m";

    /// <summary>
    /// Whether to include elevation value in peak labels.
    /// </summary>
    public bool IncludeElevation { get; set; } = true;

    /// <summary>
    /// Rotation angle in degrees (clockwise).
    /// </summary>
    public float Rotation { get; set; } = 0.0f;

    /// <summary>
    /// Letter spacing multiplier (1.0 = normal).
    /// </summary>
    public float LetterSpacing { get; set; } = 1.0f;
}

/// <summary>
/// Text alignment options.
/// </summary>
public enum TextAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Result of label generation.
/// </summary>
public class LabelResult
{
    /// <summary>
    /// Generated labels.
    /// </summary>
    public List<TerrainLabel> Labels { get; set; } = new();

    /// <summary>
    /// Number of labels generated.
    /// </summary>
    public int Count => Labels.Count;
}

/// <summary>
/// Represents a text label on the terrain.
/// </summary>
public class TerrainLabel
{
    /// <summary>
    /// Text content of the label.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Position on the terrain (model coordinates).
    /// </summary>
    public (float X, float Y) Position { get; set; }

    /// <summary>
    /// Elevation at the label position.
    /// </summary>
    public float Elevation { get; set; }

    /// <summary>
    /// Font size in millimeters.
    /// </summary>
    public float FontSize { get; set; }

    /// <summary>
    /// Text depth in millimeters.
    /// </summary>
    public float Depth { get; set; }

    /// <summary>
    /// Rotation angle in degrees.
    /// </summary>
    public float Rotation { get; set; }

    /// <summary>
    /// Label type for categorization.
    /// </summary>
    public LabelType Type { get; set; }

    /// <summary>
    /// Text alignment.
    /// </summary>
    public TextAlignment Alignment { get; set; }
}

/// <summary>
/// Types of labels.
/// </summary>
public enum LabelType
{
    Peak,
    Valley,
    Place,
    Custom,
    Elevation,
    Title
}
