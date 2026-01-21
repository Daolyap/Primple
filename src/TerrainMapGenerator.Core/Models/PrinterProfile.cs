using TerrainMapGenerator.Core.Enums;

namespace TerrainMapGenerator.Core.Models;

/// <summary>
/// Represents a Bambu Labs printer profile with build volume and capabilities.
/// </summary>
public class PrinterProfile
{
    /// <summary>
    /// Printer model identifier.
    /// </summary>
    public BambuPrinterModel Model { get; init; }

    /// <summary>
    /// Display name for the printer.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Build volume width (X) in millimeters.
    /// </summary>
    public double BuildVolumeX { get; init; }

    /// <summary>
    /// Build volume depth (Y) in millimeters.
    /// </summary>
    public double BuildVolumeY { get; init; }

    /// <summary>
    /// Build volume height (Z) in millimeters.
    /// </summary>
    public double BuildVolumeZ { get; init; }

    /// <summary>
    /// Whether the printer supports multiple materials (AMS).
    /// </summary>
    public bool SupportsMultiMaterial { get; init; }

    /// <summary>
    /// Maximum number of colors/materials when using AMS.
    /// </summary>
    public int MaxMaterialSlots { get; init; }

    /// <summary>
    /// Whether the printer has an enclosure.
    /// </summary>
    public bool HasEnclosure { get; init; }

    /// <summary>
    /// Recommended nozzle diameter in millimeters.
    /// </summary>
    public double DefaultNozzleDiameter { get; init; } = 0.4;

    /// <summary>
    /// Maximum recommended print speed in mm/s.
    /// </summary>
    public double MaxPrintSpeed { get; init; } = 500;

    /// <summary>
    /// Creates a map configuration constrained to this printer's build volume.
    /// </summary>
    public MapConfiguration CreateConstrainedConfiguration(MapConfiguration source)
    {
        var config = source.Clone();

        // Constrain dimensions to build volume
        config.OutputWidthMm = Math.Min(config.OutputWidthMm, BuildVolumeX);
        config.OutputHeightMm = Math.Min(config.OutputHeightMm, BuildVolumeY);
        config.MaxPrintHeightMm = Math.Min(config.MaxPrintHeightMm, BuildVolumeZ);

        return config;
    }

    /// <summary>
    /// Validates that a configuration fits within the printer's capabilities.
    /// </summary>
    public PrinterValidationResult ValidateConfiguration(MapConfiguration config)
    {
        var result = new PrinterValidationResult();

        if (config.OutputWidthMm > BuildVolumeX)
            result.AddError($"Width {config.OutputWidthMm}mm exceeds build volume X ({BuildVolumeX}mm).");

        if (config.OutputHeightMm > BuildVolumeY)
            result.AddError($"Height {config.OutputHeightMm}mm exceeds build volume Y ({BuildVolumeY}mm).");

        if (config.MaxPrintHeightMm > BuildVolumeZ)
            result.AddError($"Print height {config.MaxPrintHeightMm}mm exceeds build volume Z ({BuildVolumeZ}mm).");

        if (config.MinWallThicknessMm < DefaultNozzleDiameter * 1.5)
            result.AddWarning($"Minimum wall thickness should be at least {DefaultNozzleDiameter * 1.5}mm for reliable printing.");

        return result;
    }

    public override string ToString() => $"{Name} ({BuildVolumeX}x{BuildVolumeY}x{BuildVolumeZ}mm)";
}

/// <summary>
/// Result of printer validation.
/// </summary>
public class PrinterValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool IsValid => Errors.Count == 0;

    public void AddError(string message) => Errors.Add(message);
    public void AddWarning(string message) => Warnings.Add(message);
}
