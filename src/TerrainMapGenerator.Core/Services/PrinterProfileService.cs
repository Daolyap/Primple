using TerrainMapGenerator.Core.Enums;
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Services;

/// <summary>
/// Service for managing Bambu Labs printer profiles.
/// </summary>
public class PrinterProfileService : IPrinterProfileService
{
    private readonly List<PrinterProfile> _profiles;

    public PrinterProfileService()
    {
        _profiles = CreateProfiles();
    }

    public IReadOnlyList<PrinterProfile> GetAllProfiles() => _profiles;

    public PrinterProfile? GetProfile(BambuPrinterModel model)
    {
        return _profiles.FirstOrDefault(p => p.Model == model);
    }

    public PrinterProfile? GetProfileByName(string name)
    {
        return _profiles.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            p.Model.ToString().Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public PrinterProfile GetDefaultProfile()
    {
        return GetProfile(BambuPrinterModel.X1C) ?? _profiles[0];
    }

    private static List<PrinterProfile> CreateProfiles()
    {
        return new List<PrinterProfile>
        {
            new PrinterProfile
            {
                Model = BambuPrinterModel.X1C,
                Name = "Bambu Lab X1 Carbon",
                BuildVolumeX = 256,
                BuildVolumeY = 256,
                BuildVolumeZ = 256,
                SupportsMultiMaterial = true,
                MaxMaterialSlots = 16, // With 4 AMS units
                HasEnclosure = true,
                DefaultNozzleDiameter = 0.4,
                MaxPrintSpeed = 500
            },
            new PrinterProfile
            {
                Model = BambuPrinterModel.X1E,
                Name = "Bambu Lab X1E",
                BuildVolumeX = 256,
                BuildVolumeY = 256,
                BuildVolumeZ = 256,
                SupportsMultiMaterial = true,
                MaxMaterialSlots = 16,
                HasEnclosure = true,
                DefaultNozzleDiameter = 0.4,
                MaxPrintSpeed = 500
            },
            new PrinterProfile
            {
                Model = BambuPrinterModel.P1P,
                Name = "Bambu Lab P1P",
                BuildVolumeX = 256,
                BuildVolumeY = 256,
                BuildVolumeZ = 256,
                SupportsMultiMaterial = true,
                MaxMaterialSlots = 4,
                HasEnclosure = false,
                DefaultNozzleDiameter = 0.4,
                MaxPrintSpeed = 500
            },
            new PrinterProfile
            {
                Model = BambuPrinterModel.P1S,
                Name = "Bambu Lab P1S",
                BuildVolumeX = 256,
                BuildVolumeY = 256,
                BuildVolumeZ = 256,
                SupportsMultiMaterial = true,
                MaxMaterialSlots = 4,
                HasEnclosure = true,
                DefaultNozzleDiameter = 0.4,
                MaxPrintSpeed = 500
            },
            new PrinterProfile
            {
                Model = BambuPrinterModel.A1,
                Name = "Bambu Lab A1",
                BuildVolumeX = 256,
                BuildVolumeY = 256,
                BuildVolumeZ = 256,
                SupportsMultiMaterial = true,
                MaxMaterialSlots = 4,
                HasEnclosure = false,
                DefaultNozzleDiameter = 0.4,
                MaxPrintSpeed = 500
            },
            new PrinterProfile
            {
                Model = BambuPrinterModel.A1Mini,
                Name = "Bambu Lab A1 mini",
                BuildVolumeX = 180,
                BuildVolumeY = 180,
                BuildVolumeZ = 180,
                SupportsMultiMaterial = true,
                MaxMaterialSlots = 4,
                HasEnclosure = false,
                DefaultNozzleDiameter = 0.4,
                MaxPrintSpeed = 500
            },
            new PrinterProfile
            {
                Model = BambuPrinterModel.P2S,
                Name = "Bambu Lab P2S",
                BuildVolumeX = 256,
                BuildVolumeY = 256,
                BuildVolumeZ = 256,
                SupportsMultiMaterial = true,
                MaxMaterialSlots = 4, // With AMS Lite
                HasEnclosure = true,
                DefaultNozzleDiameter = 0.4,
                MaxPrintSpeed = 500
            }
        };
    }
}
