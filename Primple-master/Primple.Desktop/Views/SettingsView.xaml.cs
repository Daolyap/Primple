using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Primple.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Primple.Desktop.Views;

public partial class SettingsView : UserControl
{
    private IAppSettings? _settings;
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
        
        if (double.TryParse(DefaultScaleTextBox.Text, out double scale))
        {
            _settings.DefaultExportScale = scale;
        }
        
        _settings.DefaultBaseColor = DefaultBaseColorTextBox.Text;
        _settings.DefaultBuildingColor = DefaultBuildingColorTextBox.Text;
        _settings.DefaultRoadColor = DefaultRoadColorTextBox.Text;
        
        _settings.Save();
        
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
        
        LoadSettings();
        SaveCurrentSettings();
        
        StatusText.Text = "✓ Reset to defaults";
    }
}
