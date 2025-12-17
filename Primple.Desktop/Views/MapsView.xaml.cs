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
                var (lat, lon, name) = await mapsService.SearchLocation(query);
                
                if (name != null)
                {
                    _currentLat = lat;
                    _currentLon = lon;
                    _hasLocation = true;
                    LocationText.Text = name;
                    StatusText.Text = "Location found.";
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
                    BaseShape = BaseShapeCombo.SelectedIndex == 1 ? Primple.Core.Services.BaseShape.Circular : Primple.Core.Services.BaseShape.Square
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
}
