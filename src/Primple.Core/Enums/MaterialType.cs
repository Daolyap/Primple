namespace Primple.Core.Enums;

/// <summary>
/// Supported filament/material types for 3D printing.
/// </summary>
public enum MaterialType
{
    PLA,
    PETG,
    ASA,
    ABS,
    TPU,
    WoodPLA,
    StonePLA,
    MetallicPLA,
    CarbonFiberPLA,
    GlowInDarkPLA,
    SilkPLA
}

/// <summary>
/// Extension methods for MaterialType.
/// </summary>
public static class MaterialTypeExtensions
{
    /// <summary>
    /// Gets the display name for the material type.
    /// </summary>
    public static string GetDisplayName(this MaterialType material) => material switch
    {
        MaterialType.PLA => "PLA",
        MaterialType.PETG => "PETG",
        MaterialType.ASA => "ASA",
        MaterialType.ABS => "ABS",
        MaterialType.TPU => "TPU (Flexible)",
        MaterialType.WoodPLA => "Wood PLA",
        MaterialType.StonePLA => "Stone PLA",
        MaterialType.MetallicPLA => "Metallic PLA",
        MaterialType.CarbonFiberPLA => "Carbon Fiber PLA",
        MaterialType.GlowInDarkPLA => "Glow in Dark PLA",
        MaterialType.SilkPLA => "Silk PLA",
        _ => material.ToString()
    };

    /// <summary>
    /// Gets recommended print temperature in Celsius.
    /// </summary>
    public static (int Min, int Max) GetPrintTemperature(this MaterialType material) => material switch
    {
        MaterialType.PLA => (190, 220),
        MaterialType.PETG => (220, 250),
        MaterialType.ASA => (240, 260),
        MaterialType.ABS => (230, 260),
        MaterialType.TPU => (210, 230),
        MaterialType.WoodPLA => (190, 220),
        MaterialType.StonePLA => (200, 230),
        MaterialType.MetallicPLA => (195, 220),
        MaterialType.CarbonFiberPLA => (200, 230),
        MaterialType.GlowInDarkPLA => (190, 220),
        MaterialType.SilkPLA => (200, 230),
        _ => (200, 220)
    };

    /// <summary>
    /// Gets recommended bed temperature in Celsius.
    /// </summary>
    public static (int Min, int Max) GetBedTemperature(this MaterialType material) => material switch
    {
        MaterialType.PLA => (45, 60),
        MaterialType.PETG => (70, 85),
        MaterialType.ASA => (90, 110),
        MaterialType.ABS => (90, 110),
        MaterialType.TPU => (30, 50),
        MaterialType.WoodPLA => (45, 60),
        MaterialType.StonePLA => (50, 65),
        MaterialType.MetallicPLA => (50, 60),
        MaterialType.CarbonFiberPLA => (50, 65),
        MaterialType.GlowInDarkPLA => (45, 60),
        MaterialType.SilkPLA => (50, 65),
        _ => (50, 60)
    };

    /// <summary>
    /// Gets the recommended use case description.
    /// </summary>
    public static string GetUseCase(this MaterialType material) => material switch
    {
        MaterialType.PLA => "Display pieces, general purpose",
        MaterialType.PETG => "Durable prints, moderate weather resistance",
        MaterialType.ASA => "Outdoor use, UV resistant",
        MaterialType.ABS => "Durable, heat resistant",
        MaterialType.TPU => "Flexible maps, bendable terrain",
        MaterialType.WoodPLA => "Aesthetic wood-like finish",
        MaterialType.StonePLA => "Stone-like aesthetic finish",
        MaterialType.MetallicPLA => "Metallic appearance",
        MaterialType.CarbonFiberPLA => "Strong, stiff prints",
        MaterialType.GlowInDarkPLA => "Night display, special effects",
        MaterialType.SilkPLA => "Shiny, smooth finish",
        _ => "General purpose"
    };
}
