using TerrainMapGenerator.Core.Enums;
using TerrainMapGenerator.Core.Models;
using TerrainMapGenerator.Core.Services;
using Xunit;

namespace TerrainMapGenerator.Tests;

public class PrinterProfileServiceTests
{
    private readonly PrinterProfileService _service = new();

    [Fact]
    public void GetAllProfiles_ReturnsAllBambuPrinters()
    {
        var profiles = _service.GetAllProfiles();

        Assert.NotEmpty(profiles);
        Assert.Equal(6, profiles.Count); // X1C, X1E, P1P, P1S, A1, A1Mini
    }

    [Theory]
    [InlineData(BambuPrinterModel.X1C)]
    [InlineData(BambuPrinterModel.X1E)]
    [InlineData(BambuPrinterModel.P1P)]
    [InlineData(BambuPrinterModel.P1S)]
    [InlineData(BambuPrinterModel.A1)]
    [InlineData(BambuPrinterModel.A1Mini)]
    public void GetProfile_ReturnsCorrectProfile(BambuPrinterModel model)
    {
        var profile = _service.GetProfile(model);

        Assert.NotNull(profile);
        Assert.Equal(model, profile.Model);
    }

    [Theory]
    [InlineData("X1C")]
    [InlineData("x1c")]
    [InlineData("Bambu Lab X1 Carbon")]
    public void GetProfileByName_FindsProfile(string name)
    {
        var profile = _service.GetProfileByName(name);

        Assert.NotNull(profile);
        Assert.Equal(BambuPrinterModel.X1C, profile.Model);
    }

    [Fact]
    public void GetProfileByName_InvalidName_ReturnsNull()
    {
        var profile = _service.GetProfileByName("InvalidPrinter");

        Assert.Null(profile);
    }

    [Fact]
    public void GetDefaultProfile_ReturnsX1C()
    {
        var profile = _service.GetDefaultProfile();

        Assert.NotNull(profile);
        Assert.Equal(BambuPrinterModel.X1C, profile.Model);
    }

    [Fact]
    public void X1C_HasCorrectBuildVolume()
    {
        var profile = _service.GetProfile(BambuPrinterModel.X1C);

        Assert.NotNull(profile);
        Assert.Equal(256, profile.BuildVolumeX);
        Assert.Equal(256, profile.BuildVolumeY);
        Assert.Equal(256, profile.BuildVolumeZ);
    }

    [Fact]
    public void A1Mini_HasSmallerBuildVolume()
    {
        var profile = _service.GetProfile(BambuPrinterModel.A1Mini);

        Assert.NotNull(profile);
        Assert.Equal(180, profile.BuildVolumeX);
        Assert.Equal(180, profile.BuildVolumeY);
        Assert.Equal(180, profile.BuildVolumeZ);
    }

    [Fact]
    public void X1C_SupportsMultiMaterial()
    {
        var profile = _service.GetProfile(BambuPrinterModel.X1C);

        Assert.NotNull(profile);
        Assert.True(profile.SupportsMultiMaterial);
        Assert.Equal(16, profile.MaxMaterialSlots);
    }

    [Fact]
    public void CreateConstrainedConfiguration_ConstrainsToXBuildVolume()
    {
        var profile = _service.GetProfile(BambuPrinterModel.A1Mini)!;
        var config = new MapConfiguration
        {
            OutputWidthMm = 300,
            OutputHeightMm = 300,
            MaxPrintHeightMm = 300
        };

        var constrained = profile.CreateConstrainedConfiguration(config);

        Assert.Equal(180, constrained.OutputWidthMm);
        Assert.Equal(180, constrained.OutputHeightMm);
        Assert.Equal(180, constrained.MaxPrintHeightMm);
    }

    [Fact]
    public void ValidateConfiguration_TooLarge_ReturnsError()
    {
        var profile = _service.GetProfile(BambuPrinterModel.A1Mini)!;
        var config = new MapConfiguration
        {
            OutputWidthMm = 200 // Exceeds A1 mini build volume
        };

        var result = profile.ValidateConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Width"));
    }

    [Fact]
    public void ValidateConfiguration_ThinWalls_ReturnsWarning()
    {
        var profile = _service.GetProfile(BambuPrinterModel.X1C)!;
        var config = new MapConfiguration
        {
            OutputWidthMm = 100,
            OutputHeightMm = 100,
            MinWallThicknessMm = 0.3 // Less than 1.5x nozzle diameter
        };

        var result = profile.ValidateConfiguration(config);

        Assert.NotEmpty(result.Warnings);
    }
}
