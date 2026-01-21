using TerrainMapGenerator.Core.Enums;
using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Interfaces;

/// <summary>
/// Service for managing Bambu Labs printer profiles.
/// </summary>
public interface IPrinterProfileService
{
    /// <summary>
    /// Gets all available printer profiles.
    /// </summary>
    IReadOnlyList<PrinterProfile> GetAllProfiles();

    /// <summary>
    /// Gets a printer profile by model.
    /// </summary>
    PrinterProfile? GetProfile(BambuPrinterModel model);

    /// <summary>
    /// Gets a printer profile by name.
    /// </summary>
    PrinterProfile? GetProfileByName(string name);

    /// <summary>
    /// Gets the default printer profile.
    /// </summary>
    PrinterProfile GetDefaultProfile();
}
