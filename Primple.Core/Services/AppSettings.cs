using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Primple.Core.Services;

/// <summary>
/// Application settings service with persistence
/// </summary>
public interface IAppSettings : INotifyPropertyChanged
{
    bool Enable3DAcceleration { get; set; }
    double DefaultExportScale { get; set; }
    string DefaultBaseColor { get; set; }
    string DefaultBuildingColor { get; set; }
    string DefaultRoadColor { get; set; }
    bool DebugMode { get; set; }
    bool StartMaximized { get; set; }
    double WaterDepth { get; set; }
    double BuildingElevationOffset { get; set; }
    
    void Save();
    void Load();
}

public class AppSettings : IAppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Primple",
        "settings.json"
    );

    private bool _enable3DAcceleration = true;
    private double _defaultExportScale = 1.0;
    private string _defaultBaseColor = "#C8C8C8";
    private string _defaultBuildingColor = "#FF6464";
    private string _defaultRoadColor = "#323232";
    private bool _debugMode = false;
    private bool _startMaximized = true;
    private double _waterDepth = 2.0;
    private double _buildingElevationOffset = 0.0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Enable3DAcceleration
    {
        get => _enable3DAcceleration;
        set
        {
            if (_enable3DAcceleration != value)
            {
                _enable3DAcceleration = value;
                OnPropertyChanged(nameof(Enable3DAcceleration));
            }
        }
    }

    public double DefaultExportScale
    {
        get => _defaultExportScale;
        set
        {
            if (Math.Abs(_defaultExportScale - value) > 0.001)
            {
                _defaultExportScale = value;
                OnPropertyChanged(nameof(DefaultExportScale));
            }
        }
    }

    public string DefaultBaseColor
    {
        get => _defaultBaseColor;
        set
        {
            if (_defaultBaseColor != value)
            {
                _defaultBaseColor = value;
                OnPropertyChanged(nameof(DefaultBaseColor));
            }
        }
    }

    public string DefaultBuildingColor
    {
        get => _defaultBuildingColor;
        set
        {
            if (_defaultBuildingColor != value)
            {
                _defaultBuildingColor = value;
                OnPropertyChanged(nameof(DefaultBuildingColor));
            }
        }
    }

    public string DefaultRoadColor
    {
        get => _defaultRoadColor;
        set
        {
            if (_defaultRoadColor != value)
            {
                _defaultRoadColor = value;
                OnPropertyChanged(nameof(DefaultRoadColor));
            }
        }
    }

    public bool DebugMode
    {
        get => _debugMode;
        set
        {
            if (_debugMode != value)
            {
                _debugMode = value;
                OnPropertyChanged(nameof(DebugMode));
            }
        }
    }

    public bool StartMaximized
    {
        get => _startMaximized;
        set
        {
            if (_startMaximized != value)
            {
                _startMaximized = value;
                OnPropertyChanged(nameof(StartMaximized));
            }
        }
    }

    public double WaterDepth
    {
        get => _waterDepth;
        set
        {
            if (Math.Abs(_waterDepth - value) > 0.001)
            {
                _waterDepth = value;
                OnPropertyChanged(nameof(WaterDepth));
            }
        }
    }

    public double BuildingElevationOffset
    {
        get => _buildingElevationOffset;
        set
        {
            if (Math.Abs(_buildingElevationOffset - value) > 0.001)
            {
                _buildingElevationOffset = value;
                OnPropertyChanged(nameof(BuildingElevationOffset));
            }
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var data = new SettingsData
            {
                Enable3DAcceleration = this.Enable3DAcceleration,
                DefaultExportScale = this.DefaultExportScale,
                DefaultBaseColor = this.DefaultBaseColor,
                DefaultBuildingColor = this.DefaultBuildingColor,
                DefaultRoadColor = this.DefaultRoadColor,
                DebugMode = this.DebugMode,
                StartMaximized = this.StartMaximized,
                WaterDepth = this.WaterDepth,
                BuildingElevationOffset = this.BuildingElevationOffset
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Silently fail - settings not critical
        }
    }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var data = JsonSerializer.Deserialize<SettingsData>(json);
                
                if (data != null)
                {
                    Enable3DAcceleration = data.Enable3DAcceleration;
                    DefaultExportScale = data.DefaultExportScale;
                    DefaultBaseColor = data.DefaultBaseColor ?? "#C8C8C8";
                    DefaultBuildingColor = data.DefaultBuildingColor ?? "#FF6464";
                    DefaultRoadColor = data.DefaultRoadColor ?? "#323232";
                    DebugMode = data.DebugMode;
                    StartMaximized = data.StartMaximized;
                    WaterDepth = data.WaterDepth;
                    BuildingElevationOffset = data.BuildingElevationOffset;
                }
            }
        }
        catch
        {
            // Silently fail - use defaults
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private class SettingsData
    {
        [JsonPropertyName("enable3DAcceleration")]
        public bool Enable3DAcceleration { get; set; } = true;
        
        [JsonPropertyName("defaultExportScale")]
        public double DefaultExportScale { get; set; } = 1.0;
        
        [JsonPropertyName("defaultBaseColor")]
        public string DefaultBaseColor { get; set; } = "#C8C8C8";
        
        [JsonPropertyName("defaultBuildingColor")]
        public string DefaultBuildingColor { get; set; } = "#FF6464";
        
        [JsonPropertyName("defaultRoadColor")]
        public string DefaultRoadColor { get; set; } = "#323232";

        [JsonPropertyName("debugMode")]
        public bool DebugMode { get; set; } = false;

        [JsonPropertyName("startMaximized")]
        public bool StartMaximized { get; set; } = true;

        [JsonPropertyName("waterDepth")]
        public double WaterDepth { get; set; } = 2.0;

        [JsonPropertyName("buildingElevationOffset")]
        public double BuildingElevationOffset { get; set; } = 0.0;
    }
}
