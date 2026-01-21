namespace TerrainMapGenerator.Core.Enums;

/// <summary>
/// Supported elevation data file formats.
/// </summary>
public enum ElevationDataFormat
{
    /// <summary>
    /// GeoTIFF format (.tif, .tiff).
    /// </summary>
    GeoTiff,

    /// <summary>
    /// ASCII Grid format (.asc, .grd).
    /// </summary>
    AsciiGrid,

    /// <summary>
    /// SRTM HGT format (.hgt).
    /// </summary>
    Hgt,

    /// <summary>
    /// XYZ point cloud format (.xyz).
    /// </summary>
    Xyz,

    /// <summary>
    /// PNG heightmap format (.png).
    /// </summary>
    Png,

    /// <summary>
    /// Raw elevation data.
    /// </summary>
    Raw
}
