namespace TerrainMapGenerator.Core.Enums;

/// <summary>
/// Types of base structures for terrain maps.
/// </summary>
public enum BaseType
{
    /// <summary>
    /// Flat base with constant thickness.
    /// </summary>
    Flat,

    /// <summary>
    /// Tapered base - thicker at edges, thinner at center.
    /// </summary>
    Tapered,

    /// <summary>
    /// Contoured base that follows terrain at an offset.
    /// </summary>
    Contoured,

    /// <summary>
    /// Minimal base - just enough for structural integrity.
    /// </summary>
    Minimal,

    /// <summary>
    /// No base - terrain only (floating).
    /// </summary>
    None
}
