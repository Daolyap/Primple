using TerrainMapGenerator.Core.Enums;

namespace TerrainMapGenerator.Core.Models;

/// <summary>
/// Configuration settings for terrain map generation.
/// </summary>
public class MapConfiguration
{
    /// <summary>
    /// Output width in millimeters.
    /// </summary>
    public double OutputWidthMm { get; set; } = 150.0;

    /// <summary>
    /// Output height (depth) in millimeters.
    /// </summary>
    public double OutputHeightMm { get; set; } = 150.0;

    /// <summary>
    /// Maximum output Z height (print height) in millimeters.
    /// </summary>
    public double MaxPrintHeightMm { get; set; } = 30.0;

    /// <summary>
    /// Vertical exaggeration factor (1.0 = no exaggeration).
    /// </summary>
    public double VerticalExaggeration { get; set; } = 1.5;

    /// <summary>
    /// Base type for the terrain model.
    /// </summary>
    public BaseType BaseType { get; set; } = BaseType.Flat;

    /// <summary>
    /// Base thickness in millimeters.
    /// </summary>
    public double BaseThicknessMm { get; set; } = 3.0;

    /// <summary>
    /// Edge type for the terrain model.
    /// </summary>
    public EdgeType EdgeType { get; set; } = EdgeType.Vertical;

    /// <summary>
    /// Bevel angle in degrees (used when EdgeType is Beveled).
    /// </summary>
    public double BevelAngle { get; set; } = 45.0;

    /// <summary>
    /// Minimum wall thickness in millimeters.
    /// </summary>
    public double MinWallThicknessMm { get; set; } = 0.8;

    /// <summary>
    /// Whether to automatically scale vertical to fit printer.
    /// </summary>
    public bool AutoScaleVertical { get; set; } = true;

    /// <summary>
    /// Mesh resolution multiplier (1.0 = native, 0.5 = half resolution).
    /// </summary>
    public double MeshResolution { get; set; } = 1.0;

    /// <summary>
    /// Apply smoothing to terrain data.
    /// </summary>
    public bool ApplySmoothing { get; set; } = false;

    /// <summary>
    /// Smoothing radius in grid cells.
    /// </summary>
    public int SmoothingRadius { get; set; } = 1;

    /// <summary>
    /// Normalize elevation (remove base elevation).
    /// </summary>
    public bool NormalizeElevation { get; set; } = true;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public ConfigurationValidationResult Validate()
    {
        var result = new ConfigurationValidationResult();

        if (OutputWidthMm <= 0 || OutputWidthMm > 500)
            result.AddError("Output width must be between 0 and 500 mm.");

        if (OutputHeightMm <= 0 || OutputHeightMm > 500)
            result.AddError("Output height must be between 0 and 500 mm.");

        if (MaxPrintHeightMm <= 0 || MaxPrintHeightMm > 500)
            result.AddError("Max print height must be between 0 and 500 mm.");

        if (VerticalExaggeration < 0.1 || VerticalExaggeration > 50)
            result.AddError("Vertical exaggeration must be between 0.1 and 50.");

        if (BaseThicknessMm < 0 || BaseThicknessMm > 50)
            result.AddError("Base thickness must be between 0 and 50 mm.");

        if (MinWallThicknessMm < 0.4 || MinWallThicknessMm > 5)
            result.AddError("Minimum wall thickness must be between 0.4 and 5 mm.");

        if (MeshResolution <= 0 || MeshResolution > 2)
            result.AddError("Mesh resolution must be between 0 and 2.");

        if (SmoothingRadius < 0 || SmoothingRadius > 10)
            result.AddError("Smoothing radius must be between 0 and 10.");

        return result;
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    public MapConfiguration Clone()
    {
        return new MapConfiguration
        {
            OutputWidthMm = OutputWidthMm,
            OutputHeightMm = OutputHeightMm,
            MaxPrintHeightMm = MaxPrintHeightMm,
            VerticalExaggeration = VerticalExaggeration,
            BaseType = BaseType,
            BaseThicknessMm = BaseThicknessMm,
            EdgeType = EdgeType,
            BevelAngle = BevelAngle,
            MinWallThicknessMm = MinWallThicknessMm,
            AutoScaleVertical = AutoScaleVertical,
            MeshResolution = MeshResolution,
            ApplySmoothing = ApplySmoothing,
            SmoothingRadius = SmoothingRadius,
            NormalizeElevation = NormalizeElevation
        };
    }
}

/// <summary>
/// Result of configuration validation.
/// </summary>
public class ConfigurationValidationResult
{
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;

    public void AddError(string message) => Errors.Add(message);
}
