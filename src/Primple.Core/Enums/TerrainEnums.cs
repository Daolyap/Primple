namespace Primple.Core.Enums;

/// <summary>
/// Types of base for terrain models.
/// </summary>
public enum BaseType
{
    Flat,
    Tapered,
    Contoured,
    Minimal,
    Floating,
    Custom
}

/// <summary>
/// Edge treatment options for terrain models.
/// </summary>
public enum EdgeTreatment
{
    Vertical,
    Beveled15,
    Beveled30,
    Beveled45,
    Rounded,
    NaturalFade,
    DecorativeFrame,
    Chamfered
}

/// <summary>
/// Export file formats.
/// </summary>
public enum ExportFormat
{
    STLBinary,
    STLAscii,
    ThreeMF,
    OBJ,
    PLY,
    AMF,
    STEP
}

/// <summary>
/// Elevation data sources.
/// </summary>
public enum ElevationDataSource
{
    SRTM30m,
    SRTM90m,
    ASTERGdem,
    OpenTopography,
    MapboxTerrain,
    LocalFile,
    LIDAR
}

/// <summary>
/// Map size presets.
/// </summary>
public enum MapSizePreset
{
    Small,      // 100mm x 100mm
    Medium,     // 150mm x 150mm
    Large,      // 200mm x 200mm
    ExtraLarge, // 250mm x 250mm
    BambuMax,   // 256mm x 256mm
    A1MiniMax,  // 180mm x 180mm
    Custom
}

/// <summary>
/// Terrain smoothing methods.
/// </summary>
public enum SmoothingMethod
{
    None,
    Gaussian,
    Median,
    Adaptive,
    RidgePreserving
}

/// <summary>
/// Color mapping strategies for multi-material printing.
/// </summary>
public enum ColorMappingStrategy
{
    Elevation,
    TopographicStandard,
    Hypsometric,
    Custom,
    LandCover,
    SatelliteTexture,
    Hillshade,
    Aspect
}
