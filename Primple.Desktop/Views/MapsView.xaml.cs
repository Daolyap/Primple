using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using Primple.Core.Services;
using Microsoft.Extensions.DependencyInjection; // Ensure using for GetService/GetRequiredService

namespace Primple.Desktop.Views;

public partial class MapsView : UserControl
{
    private double _currentLat;
    private double _currentLon;
    private bool _hasLocation;
    private Model3DGroup? _currentModel;

    public void SetLocation(double lat, double lon, string name, double radius)
    {
        _currentLat = lat;
        _currentLon = lon;
        _hasLocation = true;
        LocationText.Text = name;
        SearchBox.Text = name;
        RadiusSlider.Value = radius;
        StatusText.Text = "Template parameters loaded.";
    }

    public void SetLocationWithElevation(double lat, double lon, string name, double radius)
    {
        SetLocation(lat, lon, name, radius);
        // Enable elevation for natural features
        CheckElevation.IsChecked = true;
        StatusText.Text = "Template loaded with elevation enabled.";
    }

    public MapsView()
    {
        InitializeComponent();
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await PerformSearch();
    }

    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await PerformSearch();
        }
    }

    private async Task PerformSearch()
    {
        string query = SearchBox.Text;
        if (string.IsNullOrWhiteSpace(query)) return;

        SetLoading(true, "Searching...");
        
        try
        {
            if (App.AppHost != null)
            {
                var mapsService = App.AppHost.Services.GetRequiredService<IMapsService>();
                var (lat, lon, name, bbox) = await mapsService.SearchLocation(query);
                
                if (name != null)
                {
                    _currentLat = lat;
                    _currentLon = lon;
                    _hasLocation = true;
                    LocationText.Text = name;
                    
                    // Auto-calculate suggested radius if bounding box is available
                    if (bbox != null && bbox.Length == 4)
                    {
                        // bbox: [minlat, maxlat, minlon, maxlon]
                        double latDiff = Math.Abs(bbox[1] - bbox[0]);
                        double lonDiff = Math.Abs(bbox[3] - bbox[2]);
                        
                        // Approx meters per degree at equator
                        double latMeters = latDiff * 111320;
                        double lonMeters = lonDiff * 111320 * Math.Cos(lat * Math.PI / 180);
                        
                        // Suggest radius as half of the larger dimension, clamped to slider range
                        double suggestedRadius = Math.Max(latMeters, lonMeters) / 2.0;
                        RadiusSlider.Value = Math.Clamp(suggestedRadius, RadiusSlider.Minimum, RadiusSlider.Maximum);
                    }
                    
                    StatusText.Text = "Location found & parameters auto-set.";
                }
                else
                {
                    StatusText.Text = "Location not found.";
                }
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error: " + ex.Message;
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasLocation)
        {
            MessageBox.Show("Please search for a location first.");
            return;
        }

        // Performance warning for large areas without dedicated GPU
        if (RadiusSlider.Value > 3000 && GetRenderTier() < 2)
        {
            var result = MessageBox.Show(
                "Large area generation (>3000m) detected on a system without dedicated GPU hardware.\n\n" +
                "This may result in very slow generation and preview performance. Do you wish to continue?",
                "Performance Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.No)
                return;
        }

        SetLoading(true, "Fetching data & Generating...");

        try
        {
            if (App.AppHost != null)
            {
                var mapsService = App.AppHost.Services.GetRequiredService<IMapsService>();
                
                var options = new MapGenerationOptions
                {
                    CenterLat = _currentLat,
                    CenterLon = _currentLon,
                    RadiusMeters = RadiusSlider.Value,
                    IncludeBuildings = CheckBuildings.IsChecked == true,
                    IncludeRoads = CheckRoads.IsChecked == true,
                    Is3DMode = Check3D.IsChecked == true,
                    Resolution = (int)ResolutionSlider.Value,
                    BaseColor = ParseColor(BaseColorHex.Text, Color.FromRgb(200, 200, 200)),
                    BuildingColor = ParseColor(BuildingColorHex.Text, Color.FromRgb(255, 100, 100)),
                    RoadColor = ParseColor(RoadColorHex.Text, Color.FromRgb(50, 50, 50)),
                    WaterColor = Colors.Blue, // Placeholder if needed in future
                    BaseShape = BaseShapeCombo.SelectedIndex == 1 ? Primple.Core.Services.BaseShape.Circular : Primple.Core.Services.BaseShape.Square,
                    IncludeElevation = CheckElevation.IsChecked == true,
                    UseGroundLevel = CheckGroundLevel.IsChecked == true,
                    GroundLevel = GroundLevelSlider.Value,
                    BaseThickness = BaseThicknessSlider.Value
                };

                _currentModel = await mapsService.GenerateMapModel(options);
                
                // Update Preview
                var visual = new ModelVisual3D();
                visual.Content = _currentModel;
                previewVisual.Children.Clear();
                previewVisual.Children.Add(visual);
                
                viewport.ZoomExtents();
                StatusText.Text = "Model Generated.";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Generation Error: " + ex.Message;
            MessageBox.Show(ex.ToString());
        }
        finally
        {
            SetLoading(false);
        }
    }

    private System.Windows.Media.Color ParseColor(string hex, System.Windows.Media.Color fallback)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            var obj = System.Windows.Media.ColorConverter.ConvertFromString(hex);
            if (obj is System.Windows.Media.Color color) return color;
        }
        catch {}
        return fallback;
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentModel == null)
        {
            MessageBox.Show("Generate a model first.");
            return;
        }

        try
        {
            // Get scale from UI
            double scale = 1.0;
            if (double.TryParse(ScaleTextBox.Text, out double s)) scale = s;

            // Save file dialog
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "STL Files (*.stl)|*.stl",
                DefaultExt = ".stl",
                FileName = $"Map_{SearchBox.Text.Replace(" ", "_")}.stl"
            };

            if (dialog.ShowDialog() == true)
            {
                // Export to STL using HelixToolkit or custom exporter
                StatusText.Text = "Exporting STL...";
                ExportModelToSTL(_currentModel, dialog.FileName, scale);
                StatusText.Text = $"Exported to {System.IO.Path.GetFileName(dialog.FileName)}";
                MessageBox.Show($"Model exported successfully to:\n{dialog.FileName}", "Export Complete");
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Export Error: " + ex.Message;
            MessageBox.Show($"Error exporting STL:\n{ex.Message}", "Export Error");
        }
    }

    private void ExportModelToSTL(Model3DGroup model, string fileName, double scale)
    {
        using (var writer = new System.IO.StreamWriter(fileName))
        {
            writer.WriteLine("solid map_export");

            foreach (var child in model.Children)
            {
                if (child is GeometryModel3D geoModel && geoModel.Geometry is MeshGeometry3D mesh)
                {
                    // Write triangles
                    for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
                    {
                        var p1 = mesh.Positions[mesh.TriangleIndices[i]];
                        var p2 = mesh.Positions[mesh.TriangleIndices[i + 1]];
                        var p3 = mesh.Positions[mesh.TriangleIndices[i + 2]];

                        // Calculate normal
                        var v1 = new Vector3D(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
                        var v2 = new Vector3D(p3.X - p1.X, p3.Y - p1.Y, p3.Z - p1.Z);
                        var normal = Vector3D.CrossProduct(v1, v2);
                        normal.Normalize();

                        writer.WriteLine($"  facet normal {normal.X:F6} {normal.Y:F6} {normal.Z:F6}");
                        writer.WriteLine("    outer loop");
                        writer.WriteLine($"      vertex {p1.X * scale:F6} {p1.Y * scale:F6} {p1.Z * scale:F6}");
                        writer.WriteLine($"      vertex {p2.X * scale:F6} {p2.Y * scale:F6} {p2.Z * scale:F6}");
                        writer.WriteLine($"      vertex {p3.X * scale:F6} {p3.Y * scale:F6} {p3.Z * scale:F6}");
                        writer.WriteLine("    endloop");
                        writer.WriteLine("  endfacet");
                    }
                }
            }

            writer.WriteLine("endsolid map_export");
        }
    }

    private void SetLoading(bool isLoading, string msg = "")
    {
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        if (isLoading && !string.IsNullOrEmpty(msg)) StatusText.Text = msg;
    }

    private int GetRenderTier()
    {
        try
        {
            // Tier 0: No hardware acceleration
            // Tier 1: Some hardware acceleration (DirectX 7.0/8.0)
            // Tier 2: Full hardware acceleration (DirectX 9.0+)
            return RenderCapability.Tier >> 16;
        }
        catch
        {
            return 0;
        }
    }
}
