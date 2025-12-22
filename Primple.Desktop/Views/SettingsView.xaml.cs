using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Primple.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Primple.Desktop.Views;

public partial class SettingsView : UserControl
{
    private IAppSettings? _settings;
    private ILogService? _logService;
    private bool _isLoading = true;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += SettingsView_Loaded;
    }

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (App.AppHost != null)
        {
            _settings = App.AppHost.Services.GetService<IAppSettings>();
            _logService = App.AppHost.Services.GetService<ILogService>();
            if (_settings != null)
            {
                LoadSettings();
            }
        }
        _isLoading = false;
    }

    private void LoadSettings()
    {
        if (_settings == null) return;

        _isLoading = true;
        
        Enable3DAccelerationCheckBox.IsChecked = _settings.Enable3DAcceleration;
        DefaultScaleSlider.Value = _settings.DefaultExportScale;
        DefaultScaleTextBox.Text = _settings.DefaultExportScale.ToString("F2");
        
        DefaultBaseColorTextBox.Text = _settings.DefaultBaseColor;
        DefaultBuildingColorTextBox.Text = _settings.DefaultBuildingColor;
        DefaultRoadColorTextBox.Text = _settings.DefaultRoadColor;

        DebugModeCheckBox.IsChecked = _settings.DebugMode;
        StartMaximizedCheckBox.IsChecked = _settings.StartMaximized;

        WaterDepthSlider.Value = _settings.WaterDepth;
        WaterDepthTextBox.Text = _settings.WaterDepth.ToString("F1");

        BuildingOffsetSlider.Value = _settings.BuildingElevationOffset;
        BuildingOffsetTextBox.Text = _settings.BuildingElevationOffset.ToString("F1");
        
        UpdateColorPreviews();
        
        _isLoading = false;
    }

    private void Settings_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _settings == null) return;
        
        // Auto-save on change
        SaveCurrentSettings();
    }

    private void ScaleSlider_Changed(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        DefaultScaleTextBox.Text = DefaultScaleSlider.Value.ToString("F2");
        SaveCurrentSettings();
    }

    private void WaterDepthSlider_Changed(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || WaterDepthTextBox == null) return;
        WaterDepthTextBox.Text = WaterDepthSlider.Value.ToString("F1");
        SaveCurrentSettings();
    }

    private void BuildingOffsetSlider_Changed(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || BuildingOffsetTextBox == null) return;
        BuildingOffsetTextBox.Text = BuildingOffsetSlider.Value.ToString("F1");
        SaveCurrentSettings();
    }

    private void ColorTextBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        UpdateColorPreviews();
        SaveCurrentSettings();
    }

    private void UpdateColorPreviews()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(DefaultBaseColorTextBox.Text))
            {
                var color = ParseColor(DefaultBaseColorTextBox.Text);
                if (color.HasValue)
                    BaseColorPreview.Color = color.Value;
            }
            
            if (!string.IsNullOrWhiteSpace(DefaultBuildingColorTextBox.Text))
            {
                var color = ParseColor(DefaultBuildingColorTextBox.Text);
                if (color.HasValue)
                    BuildingColorPreview.Color = color.Value;
            }
            
            if (!string.IsNullOrWhiteSpace(DefaultRoadColorTextBox.Text))
            {
                var color = ParseColor(DefaultRoadColorTextBox.Text);
                if (color.HasValue)
                    RoadColorPreview.Color = color.Value;
            }
        }
        catch { }
    }

    private Color? ParseColor(string hex)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            var obj = ColorConverter.ConvertFromString(hex);
            if (obj is Color color) return color;
        }
        catch { }
        return null;
    }

    private void SaveCurrentSettings()
    {
        if (_settings == null) return;

        _settings.Enable3DAcceleration = Enable3DAccelerationCheckBox.IsChecked == true;
        _settings.DebugMode = DebugModeCheckBox.IsChecked == true;
        _settings.StartMaximized = StartMaximizedCheckBox.IsChecked == true;
        
        if (double.TryParse(DefaultScaleTextBox.Text, out double scale))
        {
            _settings.DefaultExportScale = scale;
        }

        if (double.TryParse(WaterDepthTextBox.Text, out double waterDepth))
        {
            _settings.WaterDepth = waterDepth;
        }

        if (double.TryParse(BuildingOffsetTextBox.Text, out double buildingOffset))
        {
            _settings.BuildingElevationOffset = buildingOffset;
        }
        
        _settings.DefaultBaseColor = DefaultBaseColorTextBox.Text;
        _settings.DefaultBuildingColor = DefaultBuildingColorTextBox.Text;
        _settings.DefaultRoadColor = DefaultRoadColorTextBox.Text;
        
        _settings.Save();

        // Update log service debug mode
        if (_logService != null)
        {
            _logService.IsEnabled = _settings.DebugMode;
        }
        
        StatusText.Text = "✓ Settings saved";
        
        // Clear status after 2 seconds
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        timer.Tick += (s, e) =>
        {
            StatusText.Text = "";
            timer.Stop();
        };
        timer.Start();
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentSettings();
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;

        _isLoading = true;
        
        _settings.Enable3DAcceleration = true;
        _settings.DefaultExportScale = 1.0;
        _settings.DefaultBaseColor = "#C8C8C8";
        _settings.DefaultBuildingColor = "#FF6464";
        _settings.DefaultRoadColor = "#323232";
        _settings.DebugMode = false;
        _settings.StartMaximized = true;
        _settings.WaterDepth = 2.0;
        _settings.BuildingElevationOffset = 0.0;
        
        LoadSettings();
        SaveCurrentSettings();
        
        StatusText.Text = "✓ Reset to defaults";
    }

    private void ViewLogs_Click(object sender, RoutedEventArgs e)
    {
        if (_logService == null || App.AppHost == null) 
        {
            MessageBox.Show("Log service is not available.", "Debug Logs", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var logWindow = new LogViewerWindow(_logService);
        logWindow.Owner = Window.GetWindow(this);
        logWindow.Show();
    }
}
