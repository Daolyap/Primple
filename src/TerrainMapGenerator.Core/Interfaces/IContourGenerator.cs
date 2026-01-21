using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Interfaces;

/// <summary>
/// Service for generating embossed or debossed contour lines on terrain meshes.
/// </summary>
public interface IContourGenerator
{
    /// <summary>
    /// Generates contour lines from elevation data.
    /// </summary>
    /// <param name="elevationData">Source elevation data.</param>
    /// <param name="options">Contour generation options.</param>
    /// <returns>Collection of contour lines.</returns>
    ContourResult GenerateContours(ElevationData elevationData, ContourOptions options);

    /// <summary>
    /// Generates contour lines from elevation data asynchronously.
    /// </summary>
    Task<ContourResult> GenerateContoursAsync(ElevationData elevationData, ContourOptions options);

    /// <summary>
    /// Applies contour lines to a terrain mesh as embossed or debossed features.
    /// </summary>
    /// <param name="mesh">The terrain mesh to modify.</param>
    /// <param name="contours">The contour lines to apply.</param>
    /// <param name="depth">Depth/height of the emboss/deboss in millimeters.</param>
    /// <param name="emboss">True for embossed (raised), false for debossed (sunken).</param>
    /// <returns>Modified mesh with contour features.</returns>
    TerrainMesh ApplyContoursToMesh(TerrainMesh mesh, ContourResult contours, float depth, bool emboss = false);
}

/// <summary>
/// Options for contour generation.
/// </summary>
public class ContourOptions
{
    /// <summary>
    /// Interval between contour lines in elevation units (meters).
    /// </summary>
    public float Interval { get; set; } = 100.0f;

    /// <summary>
    /// Minimum elevation for contour generation.
    /// If null, uses the minimum elevation from data.
    /// </summary>
    public float? MinElevation { get; set; }

    /// <summary>
    /// Maximum elevation for contour generation.
    /// If null, uses the maximum elevation from data.
    /// </summary>
    public float? MaxElevation { get; set; }

    /// <summary>
    /// Width of contour lines in millimeters (for mesh application).
    /// </summary>
    public float LineWidth { get; set; } = 0.5f;

    /// <summary>
    /// Interval for major contour lines (typically 5x or 10x regular interval).
    /// Major contours can have different width/depth.
    /// </summary>
    public float MajorInterval { get; set; } = 500.0f;

    /// <summary>
    /// Width multiplier for major contour lines.
    /// </summary>
    public float MajorWidthMultiplier { get; set; } = 1.5f;

    /// <summary>
    /// Minimum length of contour segments to keep (filters noise).
    /// </summary>
    public float MinSegmentLength { get; set; } = 1.0f;

    /// <summary>
    /// Apply smoothing to contour lines.
    /// </summary>
    public bool SmoothContours { get; set; } = true;

    /// <summary>
    /// Number of smoothing iterations.
    /// </summary>
    public int SmoothingIterations { get; set; } = 2;
}

/// <summary>
/// Result of contour generation.
/// </summary>
public class ContourResult
{
    /// <summary>
    /// Regular contour lines.
    /// </summary>
    public List<ContourLine> Contours { get; set; } = new();

    /// <summary>
    /// Major contour lines (at major intervals).
    /// </summary>
    public List<ContourLine> MajorContours { get; set; } = new();

    /// <summary>
    /// Total number of contour segments generated.
    /// </summary>
    public int TotalSegments => Contours.Sum(c => c.Points.Count - 1) + 
                                MajorContours.Sum(c => c.Points.Count - 1);
}

/// <summary>
/// Represents a single contour line.
/// </summary>
public class ContourLine
{
    /// <summary>
    /// Elevation value of this contour line.
    /// </summary>
    public float Elevation { get; set; }

    /// <summary>
    /// Whether this is a major contour line.
    /// </summary>
    public bool IsMajor { get; set; }

    /// <summary>
    /// Points along the contour line.
    /// </summary>
    public List<ContourPoint> Points { get; set; } = new();

    /// <summary>
    /// Whether this contour forms a closed loop.
    /// </summary>
    public bool IsClosed { get; set; }
}

/// <summary>
/// A point on a contour line.
/// </summary>
public readonly struct ContourPoint
{
    public float X { get; }
    public float Y { get; }

    public ContourPoint(float x, float y)
    {
        X = x;
        Y = y;
    }
}
