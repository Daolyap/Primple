using CommunityToolkit.Mvvm.ComponentModel;
using Primple.Core.Enums;
using System.Collections.ObjectModel;

namespace Primple.App.ViewModels;

/// <summary>
/// View model for terrain settings panel.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    // Elevation Settings
    [ObservableProperty]
    private double _verticalExaggeration = 2.0;

    [ObservableProperty]
    private SmoothingMethod _smoothingMethod = SmoothingMethod.Gaussian;

    [ObservableProperty]
    private double _smoothingRadius = 1.0;

    [ObservableProperty]
    private bool _removeBaseElevation = true;

    // Base Settings
    [ObservableProperty]
    private BaseType _baseType = BaseType.Flat;

    [ObservableProperty]
    private double _baseThickness = 3.0;

    [ObservableProperty]
    private EdgeTreatment _edgeTreatment = EdgeTreatment.Beveled30;

    // Size Settings
    [ObservableProperty]
    private MapSizePreset _sizePreset = MapSizePreset.Medium;

    [ObservableProperty]
    private double _customWidth = 150;

    [ObservableProperty]
    private double _customLength = 150;

    [ObservableProperty]
    private double _maxHeight = 30;

    [ObservableProperty]
    private bool _maintainAspectRatio = true;

    // Mesh Settings
    [ObservableProperty]
    private int _meshResolution = 500;

    // Printer Settings
    [ObservableProperty]
    private PrinterModel _selectedPrinter = PrinterModel.P2S;

    [ObservableProperty]
    private MaterialType _selectedMaterial = MaterialType.PLA;

    [ObservableProperty]
    private double _layerHeight = 0.2;

    [ObservableProperty]
    private double _infillPercentage = 15;

    // Export Settings
    [ObservableProperty]
    private ExportFormat _exportFormat = ExportFormat.STLBinary;

    // Options lists
    public ObservableCollection<PrinterModel> AvailablePrinters { get; } = new(Enum.GetValues<PrinterModel>());
    public ObservableCollection<MaterialType> AvailableMaterials { get; } = new(Enum.GetValues<MaterialType>());
    public ObservableCollection<BaseType> AvailableBaseTypes { get; } = new(Enum.GetValues<BaseType>());
    public ObservableCollection<EdgeTreatment> AvailableEdgeTreatments { get; } = new(Enum.GetValues<EdgeTreatment>());
    public ObservableCollection<SmoothingMethod> AvailableSmoothingMethods { get; } = new(Enum.GetValues<SmoothingMethod>());
    public ObservableCollection<MapSizePreset> AvailableSizePresets { get; } = new(Enum.GetValues<MapSizePreset>());
    public ObservableCollection<ExportFormat> AvailableExportFormats { get; } = new(Enum.GetValues<ExportFormat>());

    // Computed properties for display
    public string PrinterBuildVolume
    {
        get
        {
            var (x, y, z) = SelectedPrinter.GetBuildVolume();
            return $"{x} × {y} × {z} mm";
        }
    }

    public string MaterialTemperature
    {
        get
        {
            var (min, max) = SelectedMaterial.GetPrintTemperature();
            return $"{min}°C - {max}°C";
        }
    }

    public string MaterialUseCase => SelectedMaterial.GetUseCase();

    public bool IsCustomSize => SizePreset == MapSizePreset.Custom;

    partial void OnSelectedPrinterChanged(PrinterModel value)
    {
        OnPropertyChanged(nameof(PrinterBuildVolume));

        // Update size preset if larger than build volume
        var (maxX, maxY, _) = value.GetBuildVolume();
        if (CustomWidth > maxX) CustomWidth = maxX;
        if (CustomLength > maxY) CustomLength = maxY;
    }

    partial void OnSelectedMaterialChanged(MaterialType value)
    {
        OnPropertyChanged(nameof(MaterialTemperature));
        OnPropertyChanged(nameof(MaterialUseCase));
    }

    partial void OnSizePresetChanged(MapSizePreset value)
    {
        OnPropertyChanged(nameof(IsCustomSize));

        // Update custom dimensions based on preset
        var (width, length) = value switch
        {
            MapSizePreset.Small => (100.0, 100.0),
            MapSizePreset.Medium => (150.0, 150.0),
            MapSizePreset.Large => (200.0, 200.0),
            MapSizePreset.ExtraLarge => (250.0, 250.0),
            MapSizePreset.BambuMax => (256.0, 256.0),
            MapSizePreset.A1MiniMax => (180.0, 180.0),
            _ => (CustomWidth, CustomLength)
        };

        if (value != MapSizePreset.Custom)
        {
            CustomWidth = width;
            CustomLength = length;
        }
    }
}
