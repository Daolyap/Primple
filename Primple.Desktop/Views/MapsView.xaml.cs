using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using Primple.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Primple.Desktop.Views;

public partial class MapsView : UserControl
{
    private double _currentLat;
    private double _currentLon;
    private bool _hasLocation;
    private Model3DGroup? _currentModel;

    // Printer profiles: (name, buildVolumeX, buildVolumeY, buildVolumeZ)
    private static readonly (string name, int x, int y, int z)[] PrinterProfiles = new[]
    {
        ("Custom", 0, 0, 0),
        ("Bambu Lab A1", 256, 256, 256),
        ("Bambu Lab P1S", 256, 256, 256),
        ("Bambu Lab X1C", 256, 256, 256),
        ("Prusa MK4", 250, 210, 220),
        ("Prusa Mini", 180, 180, 180),
        ("Creality Ender 3", 220, 220, 250),
        ("Creality K1", 220, 220, 250),
        ("Anycubic Kobra 2", 220, 220, 250),
        ("Elegoo Neptune 4", 225, 225, 265)
    };

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

    private void PrinterProfile_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (PrinterProfileCombo.SelectedIndex > 0 && PrinterProfileCombo.SelectedIndex < PrinterProfiles.Length)
        {
            var profile = PrinterProfiles[PrinterProfileCombo.SelectedIndex];
            // Set output size to the smallest dimension (for square models)
            int smallestDim = Math.Min(profile.x, Math.Min(profile.y, profile.z));
            OutputSizeTextBox.Text = Math.Max(10, smallestDim - 10).ToString(); // Leave 10mm margin, minimum 10mm
        }
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
                    WaterColor = Colors.Blue,
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

    private void ExportMultiColor_Click(object sender, RoutedEventArgs e)
    {
        if (_currentModel == null)
        {
            MessageBox.Show("Generate a model first.");
            return;
        }

        try
        {
            double scale = 1.0;
            if (double.TryParse(ScaleTextBox.Text, out double s)) scale = s;

            // Let user choose a folder
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "STL Files (*.stl)|*.stl",
                DefaultExt = ".stl",
                FileName = $"Map_{SearchBox.Text.Replace(" ", "_")}_terrain.stl",
                Title = "Choose base filename for multi-color export"
            };

            if (dialog.ShowDialog() == true)
            {
                StatusText.Text = "Exporting multi-color STLs...";
                
                string basePath = System.IO.Path.GetDirectoryName(dialog.FileName) ?? "";
                string fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                const string terrainSuffix = "_terrain";
                string baseName = fileNameWithoutExt.EndsWith(terrainSuffix, StringComparison.OrdinalIgnoreCase)
                    ? fileNameWithoutExt.Substring(0, fileNameWithoutExt.Length - terrainSuffix.Length)
                    : fileNameWithoutExt;
                
                // Export each component separately
                // Component order in Model3DGroup: 0=base, 1=ground, 2=water (optional), 3+=buildings, roads
                int exported = 0;
                string[] componentNames = { "base", "terrain", "water", "buildings", "roads" };
                
                for (int i = 0; i < _currentModel.Children.Count && i < componentNames.Length; i++)
                {
                    if (_currentModel.Children[i] is GeometryModel3D geoModel && geoModel.Geometry is MeshGeometry3D mesh)
                    {
                        if (mesh.Positions.Count == 0) continue;
                        
                        string fileName = System.IO.Path.Combine(basePath, $"{baseName}_{componentNames[i]}.stl");
                        ExportSingleMeshToSTL(mesh, fileName, scale, componentNames[i]);
                        exported++;
                    }
                }
                
                StatusText.Text = $"Exported {exported} STL files for multi-color printing!";
                MessageBox.Show(
                    $"Exported {exported} separate STL files to:\n{basePath}\n\n" +
                    "Import all files into your slicer (e.g., Bambu Studio) as a single project to assign different colors/materials.",
                    "Multi-Color Export Complete");
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Export Error: " + ex.Message;
            MessageBox.Show($"Error exporting multi-color STLs:\n{ex.Message}", "Export Error");
        }
    }

    private void ExportSingleMeshToSTL(MeshGeometry3D mesh, string fileName, double scale, string solidName)
    {
        using (var writer = new System.IO.StreamWriter(fileName))
        {
            writer.WriteLine($"solid {solidName}");

            for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
            {
                var p1 = mesh.Positions[mesh.TriangleIndices[i]];
                var p2 = mesh.Positions[mesh.TriangleIndices[i + 1]];
                var p3 = mesh.Positions[mesh.TriangleIndices[i + 2]];

                var v1 = new Vector3D(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
                var v2 = new Vector3D(p3.X - p1.X, p3.Y - p1.Y, p3.Z - p1.Z);
                var normal = Vector3D.CrossProduct(v1, v2);
                normal.Normalize();

                if (double.IsNaN(normal.X) || double.IsNaN(normal.Y) || double.IsNaN(normal.Z)) 
                    normal = new Vector3D(0, 1, 0);

                writer.WriteLine($"  facet normal {normal.X:F6} {normal.Y:F6} {normal.Z:F6}");
                writer.WriteLine("    outer loop");
                writer.WriteLine($"      vertex {p1.X * scale:F6} {p1.Y * scale:F6} {p1.Z * scale:F6}");
                writer.WriteLine($"      vertex {p2.X * scale:F6} {p2.Y * scale:F6} {p2.Z * scale:F6}");
                writer.WriteLine($"      vertex {p3.X * scale:F6} {p3.Y * scale:F6} {p3.Z * scale:F6}");
                writer.WriteLine("    endloop");
                writer.WriteLine("  endfacet");
            }

            writer.WriteLine($"endsolid {solidName}");
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
                    for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
                    {
                        var p1 = mesh.Positions[mesh.TriangleIndices[i]];
                        var p2 = mesh.Positions[mesh.TriangleIndices[i + 1]];
                        var p3 = mesh.Positions[mesh.TriangleIndices[i + 2]];

                        var v1 = new Vector3D(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
                        var v2 = new Vector3D(p3.X - p1.X, p3.Y - p1.Y, p3.Z - p1.Z);
                        var normal = Vector3D.CrossProduct(v1, v2);
                        normal.Normalize();

                        if (double.IsNaN(normal.X) || double.IsNaN(normal.Y) || double.IsNaN(normal.Z)) 
                            normal = new Vector3D(0, 1, 0);

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
            return RenderCapability.Tier >> 16;
        }
        catch
        {
            return 0;
        }
    }
}
