namespace TerrainMapGenerator.Core.Enums;

/// <summary>
/// Edge treatment types for terrain maps.
/// </summary>
public enum EdgeType
{
    /// <summary>
    /// Vertical cut (cliff edge).
    /// </summary>
    Vertical,

    /// <summary>
    /// Beveled edge at specified angle.
    /// </summary>
    Beveled,

    /// <summary>
    /// Rounded edge with radius control.
    /// </summary>
    Rounded,

    /// <summary>
    /// Natural terrain fade-out.
    /// </summary>
    Natural
}
