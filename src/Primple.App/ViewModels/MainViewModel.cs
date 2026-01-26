using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Primple.App.Services;
using Primple.Core.Enums;
using Primple.Core.Interfaces;
using Primple.Core.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Numerics;

namespace Primple.App.ViewModels;

/// <summary>
/// Main view model for the application.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IElevationDataService _elevationService;
    private readonly IMeshGeneratorService _meshService;
    private readonly IExportService _exportService;
    private readonly IGeocodingService _geocodingService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private TerrainProject _currentProject;

    [ObservableProperty]
    private TerrainMesh? _currentMesh;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private MapSelectionViewModel _mapSelection;

    [ObservableProperty]
    private SettingsViewModel _settings;

    [ObservableProperty]
    private TerrainPreviewViewModel _terrainPreview;

    // Print statistics
    [ObservableProperty]
    private int _triangleCount;

    [ObservableProperty]
    private string _dimensions = "0 × 0 × 0 mm";

    [ObservableProperty]
    private double _estimatedPrintTime;

    [ObservableProperty]
    private double _estimatedFilamentGrams;

    [ObservableProperty]
    private long _estimatedFileSize;

    [ObservableProperty]
    private bool _isMeshValid;

    [ObservableProperty]
    private ObservableCollection<string> _validationMessages;

    public MainViewModel()
    {
        _elevationService = App.Services.GetRequiredService<IElevationDataService>();
        _meshService = App.Services.GetRequiredService<IMeshGeneratorService>();
        _exportService = App.Services.GetRequiredService<IExportService>();
        _geocodingService = App.Services.GetRequiredService<IGeocodingService>();
        _dialogService = App.Services.GetRequiredService<IDialogService>();

        _currentProject = new TerrainProject { Name = "New Project" };
        _validationMessages = new ObservableCollection<string>();

        _mapSelection = new MapSelectionViewModel(_geocodingService);
        _settings = new SettingsViewModel();
        _terrainPreview = new TerrainPreviewViewModel();

        // Subscribe to map selection changes
        _mapSelection.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MapSelectionViewModel.SelectedBounds))
            {
                CurrentProject.Bounds = _mapSelection.SelectedBounds ?? new GeographicBounds();
            }
        };

        // Subscribe to settings changes for live preview
        _settings.PropertyChanged += async (s, e) =>
        {
            if (CurrentProject.ElevationData != null)
            {
                UpdateProjectSettings();
                await GenerateMeshAsync();
            }
        };
    }

    private void UpdateProjectSettings()
    {
        CurrentProject.Settings = new TerrainSettings
        {
            VerticalExaggeration = Settings.VerticalExaggeration,
            SmoothingMethod = Settings.SmoothingMethod,
            SmoothingRadius = Settings.SmoothingRadius,
            BaseType = Settings.BaseType,
            BaseThickness = Settings.BaseThickness,
            EdgeTreatment = Settings.EdgeTreatment,
            SizePreset = Settings.SizePreset,
            CustomWidth = Settings.CustomWidth,
            CustomLength = Settings.CustomLength,
            MaxHeight = Settings.MaxHeight,
            MeshResolution = Settings.MeshResolution,
            MaintainAspectRatio = Settings.MaintainAspectRatio
        };

        CurrentProject.PrinterProfile = new PrinterProfile
        {
            Model = Settings.SelectedPrinter,
            Material = Settings.SelectedMaterial,
            LayerHeight = Settings.LayerHeight,
            InfillPercentage = Settings.InfillPercentage
        };
    }

    [RelayCommand]
    private async Task FetchElevationDataAsync()
    {
        if (MapSelection.SelectedBounds == null)
        {
            _dialogService.ShowMessage("Please select a region on the map first.", "No Region Selected", icon: System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Fetching elevation data...";
            Progress = 0;

            CurrentProject.Bounds = MapSelection.SelectedBounds;
            CurrentProject.ElevationData = await _elevationService.GetElevationDataAsync(MapSelection.SelectedBounds);

            StatusMessage = $"Loaded elevation data: {CurrentProject.ElevationData.Width}x{CurrentProject.ElevationData.Height} points, " +
                          $"Range: {CurrentProject.ElevationData.MinElevation:F0}m - {CurrentProject.ElevationData.MaxElevation:F0}m";

            await GenerateMeshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Error fetching elevation data";
            _dialogService.ShowMessage($"Failed to fetch elevation data: {ex.Message}", "Error", icon: System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
            Progress = 100;
        }
    }

    [RelayCommand]
    private async Task GenerateMeshAsync()
    {
        if (CurrentProject.ElevationData == null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Generating terrain mesh...";

            UpdateProjectSettings();

            var progress = new Progress<double>(p => Progress = p * 100);

            await Task.Run(() =>
            {
                CurrentMesh = _meshService.GenerateMesh(CurrentProject.ElevationData, CurrentProject.Settings, progress);
            });

            UpdateMeshStatistics();
            TerrainPreview.UpdateMesh(CurrentMesh!);

            StatusMessage = $"Generated mesh with {CurrentMesh!.TriangleCount:N0} triangles";
        }
        catch (Exception ex)
        {
            StatusMessage = "Error generating mesh";
            _dialogService.ShowMessage($"Failed to generate mesh: {ex.Message}", "Error", icon: System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateMeshStatistics()
    {
        if (CurrentMesh == null) return;

        TriangleCount = CurrentMesh.TriangleCount;
        var dims = CurrentMesh.GetDimensions();
        Dimensions = $"{dims.X:F1} × {dims.Y:F1} × {dims.Z:F1} mm";
        EstimatedFileSize = CurrentMesh.EstimateStlFileSize();
        EstimatedPrintTime = CurrentMesh.EstimatePrintTime(CurrentProject.PrinterProfile);
        EstimatedFilamentGrams = CurrentMesh.EstimateFilamentUsage(CurrentProject.PrinterProfile);

        var validation = CurrentMesh.Validate();
        IsMeshValid = validation.IsValid;
        ValidationMessages.Clear();
        foreach (var error in validation.Errors)
            ValidationMessages.Add($"❌ {error}");
        foreach (var warning in validation.Warnings)
            ValidationMessages.Add($"⚠️ {warning}");
        if (validation.IsValid)
            ValidationMessages.Add("✓ Mesh is valid for printing");
    }

    [RelayCommand]
    private async Task ExportStlAsync()
    {
        if (CurrentMesh == null)
        {
            _dialogService.ShowMessage("Please generate a mesh first.", "No Mesh", icon: System.Windows.MessageBoxImage.Warning);
            return;
        }

        var fileName = _dialogService.SaveFile(
            "Export STL",
            "STL Files (*.stl)|*.stl|All Files (*.*)|*.*",
            $"{CurrentProject.Name}.stl");

        if (string.IsNullOrEmpty(fileName)) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Exporting STL...";

            await _exportService.ExportStlAsync(CurrentMesh, fileName);

            CurrentProject.LastExportPath = fileName;
            CurrentProject.LastExportFormat = ExportFormat.STLBinary;

            StatusMessage = $"Exported to {Path.GetFileName(fileName)}";
            _dialogService.ShowMessage($"Successfully exported to:\n{fileName}", "Export Complete");
        }
        catch (Exception ex)
        {
            StatusMessage = "Export failed";
            _dialogService.ShowMessage($"Failed to export: {ex.Message}", "Error", icon: System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Export3MFAsync()
    {
        if (CurrentMesh == null)
        {
            _dialogService.ShowMessage("Please generate a mesh first.", "No Mesh", icon: System.Windows.MessageBoxImage.Warning);
            return;
        }

        var fileName = _dialogService.SaveFile(
            "Export 3MF",
            "3MF Files (*.3mf)|*.3mf|All Files (*.*)|*.*",
            $"{CurrentProject.Name}.3mf");

        if (string.IsNullOrEmpty(fileName)) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Exporting 3MF...";

            await _exportService.Export3MFAsync(CurrentMesh, fileName, CurrentProject);

            CurrentProject.LastExportPath = fileName;
            CurrentProject.LastExportFormat = ExportFormat.ThreeMF;

            StatusMessage = $"Exported to {Path.GetFileName(fileName)}";
            _dialogService.ShowMessage($"Successfully exported to:\n{fileName}", "Export Complete");
        }
        catch (Exception ex)
        {
            StatusMessage = "Export failed";
            _dialogService.ShowMessage($"Failed to export: {ex.Message}", "Error", icon: System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ImportElevationFileAsync()
    {
        var fileName = _dialogService.OpenFile(
            "Import Elevation Data",
            "All Supported Files|*.tif;*.tiff;*.asc;*.grd;*.hgt;*.xyz|" +
            "GeoTIFF (*.tif;*.tiff)|*.tif;*.tiff|" +
            "ASCII Grid (*.asc;*.grd)|*.asc;*.grd|" +
            "SRTM HGT (*.hgt)|*.hgt|" +
            "XYZ Point Cloud (*.xyz)|*.xyz|" +
            "All Files (*.*)|*.*");

        if (string.IsNullOrEmpty(fileName)) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Loading elevation file...";

            CurrentProject.ElevationData = await _elevationService.LoadFromFileAsync(fileName);
            MapSelection.SelectedBounds = CurrentProject.ElevationData.Bounds;

            StatusMessage = $"Loaded: {CurrentProject.ElevationData.Width}x{CurrentProject.ElevationData.Height} points";

            await GenerateMeshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Import failed";
            _dialogService.ShowMessage($"Failed to import file: {ex.Message}", "Error", icon: System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void NewProject()
    {
        if (CurrentMesh != null)
        {
            if (!_dialogService.ShowConfirmation("Create a new project? Unsaved changes will be lost.", "New Project"))
                return;
        }

        CurrentProject = new TerrainProject { Name = "New Project" };
        CurrentMesh = null;
        MapSelection.ClearSelection();
        TerrainPreview.ClearMesh();
        ValidationMessages.Clear();
        StatusMessage = "Ready";
    }
}
