using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerrainMapGenerator.Core.Enums;
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;
using TerrainMapGenerator.Core.Services;

namespace TerrainMapGenerator.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IPrinterProfileService _printerService;
    private readonly IElevationDataService _elevationService;
    private readonly IMeshGenerator _meshGenerator;
    private readonly IStlExporter _stlExporter;

    [ObservableProperty]
    private string _inputFilePath = string.Empty;

    [ObservableProperty]
    private string _outputFilePath = string.Empty;

    [ObservableProperty]
    private PrinterProfile? _selectedPrinter;

    [ObservableProperty]
    private double _outputWidth = 150;

    [ObservableProperty]
    private double _outputHeight = 150;

    [ObservableProperty]
    private double _maxPrintHeight = 30;

    [ObservableProperty]
    private double _verticalExaggeration = 1.5;

    [ObservableProperty]
    private BaseType _selectedBaseType = BaseType.Flat;

    [ObservableProperty]
    private double _baseThickness = 3;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public ObservableCollection<PrinterProfile> PrinterProfiles { get; }
    public ObservableCollection<BaseType> BaseTypes { get; }

    public MainWindowViewModel()
        : this(new PrinterProfileService(), new ElevationDataService(), new MeshGenerator(), new StlExporter())
    {
    }

    public MainWindowViewModel(
        IPrinterProfileService printerService,
        IElevationDataService elevationService,
        IMeshGenerator meshGenerator,
        IStlExporter stlExporter)
    {
        _printerService = printerService;
        _elevationService = elevationService;
        _meshGenerator = meshGenerator;
        _stlExporter = stlExporter;

        PrinterProfiles = new ObservableCollection<PrinterProfile>(_printerService.GetAllProfiles());
        SelectedPrinter = _printerService.GetDefaultProfile();

        BaseTypes = new ObservableCollection<BaseType>(Enum.GetValues<BaseType>());
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        if (string.IsNullOrEmpty(InputFilePath) || string.IsNullOrEmpty(OutputFilePath))
        {
            StatusMessage = "Please select input and output files";
            return;
        }

        try
        {
            IsGenerating = true;
            Progress = 0;
            StatusMessage = "Loading elevation data...";

            var elevationData = await _elevationService.LoadFromFileAsync(InputFilePath);

            Progress = 25;
            StatusMessage = "Configuring map settings...";

            var config = new MapConfiguration
            {
                OutputWidthMm = OutputWidth,
                OutputHeightMm = OutputHeight,
                MaxPrintHeightMm = MaxPrintHeight,
                VerticalExaggeration = VerticalExaggeration,
                BaseType = SelectedBaseType,
                BaseThicknessMm = BaseThickness
            };

            if (SelectedPrinter != null)
            {
                config = SelectedPrinter.CreateConstrainedConfiguration(config);
            }

            Progress = 50;
            StatusMessage = "Generating terrain mesh...";

            var mesh = await _meshGenerator.GenerateAsync(elevationData, config);

            Progress = 75;
            StatusMessage = "Exporting to STL...";

            await _stlExporter.ExportBinaryAsync(mesh, OutputFilePath);

            Progress = 100;
            StatusMessage = $"Successfully generated: {OutputFilePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private bool CanGenerate()
    {
        return !IsGenerating && !string.IsNullOrEmpty(InputFilePath) && !string.IsNullOrEmpty(OutputFilePath);
    }

    partial void OnInputFilePathChanged(string value)
    {
        GenerateCommand.NotifyCanExecuteChanged();
        if (!string.IsNullOrEmpty(value))
        {
            StatusMessage = $"Input: {System.IO.Path.GetFileName(value)}";
        }
    }

    partial void OnOutputFilePathChanged(string value)
    {
        GenerateCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPrinterChanged(PrinterProfile? value)
    {
        if (value != null)
        {
            StatusMessage = $"Selected printer: {value.Name} ({value.BuildVolumeX}x{value.BuildVolumeY}x{value.BuildVolumeZ}mm)";
        }
    }
}
