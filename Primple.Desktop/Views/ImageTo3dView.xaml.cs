
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using Primple.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using HelixToolkit.Wpf;

namespace Primple.Desktop.Views;

public partial class ImageTo3dView : UserControl
{
    private string _currentImagePath = "";
    private GeometryModel3D? _currentGeometry;
    private ILogService? _logService;

    public ImageTo3dView()
    {
        InitializeComponent();
        
        if (App.AppHost != null)
        {
            _logService = App.AppHost.Services.GetService<ILogService>();
        }
    }

    /// <summary>
    /// Apply preset configuration for different use cases
    /// </summary>
    public void ApplyPreset(string presetName)
    {
        switch (presetName)
        {
            case "QRCode":
                HeightSlider.Value = 0.3;
                ResolutionSlider.Value = 150;
                ShapeSelector.SelectedIndex = 0; // Plane
                AddBaseCheckBox.IsChecked = true;
                BaseThicknessSlider.Value = 1.5;
                StatusText.Text = "QR Code preset applied. Use high-contrast black/white image for best results.";
                break;
            case "Lithophane":
                HeightSlider.Value = 2.5;
                ResolutionSlider.Value = 200;
                ShapeSelector.SelectedIndex = 0; // Plane
                AddBaseCheckBox.IsChecked = true;
                BaseThicknessSlider.Value = 0.8;
                // Set to white color for lithophane
                SliderR.Value = 255;
                SliderG.Value = 255;
                SliderB.Value = 255;
                StatusText.Text = "Lithophane preset applied. Print in white filament for best backlit effect.";
                break;
            case "CoinDesign":
                HeightSlider.Value = 0.5;
                ResolutionSlider.Value = 150;
                ShapeSelector.SelectedIndex = 0; // Plane
                AddBaseCheckBox.IsChecked = true;
                BaseThicknessSlider.Value = 2.0;
                // Gold-ish color
                SliderR.Value = 212;
                SliderG.Value = 175;
                SliderB.Value = 55;
                StatusText.Text = "Coin/Medal preset applied. Import a circular design for best results.";
                break;
            case "TextRelief":
                HeightSlider.Value = 1.0;
                ResolutionSlider.Value = 120;
                ShapeSelector.SelectedIndex = 0; // Plane
                AddBaseCheckBox.IsChecked = true;
                BaseThicknessSlider.Value = 1.5;
                StatusText.Text = "Text Relief preset applied. Use white text on black background.";
                break;
            case "Topographic":
                HeightSlider.Value = 3.0;
                ResolutionSlider.Value = 180;
                ShapeSelector.SelectedIndex = 0; // Plane
                AddBaseCheckBox.IsChecked = true;
                BaseThicknessSlider.Value = 2.0;
                // Earth tones
                SliderR.Value = 139;
                SliderG.Value = 119;
                SliderB.Value = 101;
                StatusText.Text = "Topographic preset applied. Use grayscale heightmap (black=low, white=high).";
                break;
            case "Logo3D":
                HeightSlider.Value = 1.5;
                ResolutionSlider.Value = 150;
                ShapeSelector.SelectedIndex = 0; // Plane
                AddBaseCheckBox.IsChecked = true;
                BaseThicknessSlider.Value = 2.0;
                StatusText.Text = "Logo preset applied. PNG with transparency works best.";
                break;
            case "HeightmapArt":
                HeightSlider.Value = 2.5;
                ResolutionSlider.Value = 200;
                ShapeSelector.SelectedIndex = 0; // Plane
                AddBaseCheckBox.IsChecked = true;
                BaseThicknessSlider.Value = 2.0;
                // Natural tones for artistic terrain
                SliderR.Value = 160;
                SliderG.Value = 160;
                SliderB.Value = 140;
                StatusText.Text = "Heightmap Art preset applied. Import grayscale image for smooth terrain.";
                break;
            default:
                StatusText.Text = $"Ready for {presetName}";
                break;
        }
    }

    private void Log(string message, string level = "INFO")
    {
        _logService?.Log(message, "ImageTo3D", level);
    }

    private void SelectImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*" };
        if (dlg.ShowDialog() == true)
        {
            _currentImagePath = dlg.FileName;
            var fileInfo = new FileInfo(_currentImagePath);
            ImageInfoText.Text = $"{Path.GetFileName(_currentImagePath)}\n{fileInfo.Length / 1024} KB";
            StatusText.Text = "Image selected. Click 'Generate Mesh' to create 3D model.";
            Log($"Image selected: {_currentImagePath}");
        }
    }

    private void Settings_Changed(object sender, EventArgs e)
    {
        // No auto-update to avoid lag
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentImagePath))
        {
             MessageBox.Show("Select an image first.");
             return;
        }

        try
        {
            StatusText.Text = "Generating...";
            Log("Starting mesh generation...");
            
            if (App.AppHost != null)
            {
                var service = App.AppHost.Services.GetRequiredService<IHeightmapService>();
                
                // Get selected shape
                ProjectionShape selectedShape = ProjectionShape.Plane;
                if (ShapeSelector.SelectedIndex == 1) selectedShape = ProjectionShape.Sphere;
                else if (ShapeSelector.SelectedIndex == 2) selectedShape = ProjectionShape.Cube;
                else if (ShapeSelector.SelectedIndex == 3) selectedShape = ProjectionShape.Cylinder;

                // Get base options
                bool addBase = AddBaseCheckBox.IsChecked == true;
                double baseThickness = BaseThicknessSlider.Value;

                var mesh = service.GenerateHeightmap(
                    _currentImagePath, 
                    HeightSlider.Value, 
                    (int)ResolutionSlider.Value, 
                    selectedShape,
                    addBase,
                    baseThickness);
                
                var material = GetCurrentMaterial();
                _currentGeometry = new GeometryModel3D(mesh, material);
                _currentGeometry.BackMaterial = material; 

                var group = new Model3DGroup();
                group.Children.Add(_currentGeometry);
                
                var visual = new ModelVisual3D { Content = group };
                modelContainer.Children.Clear();
                modelContainer.Children.Add(visual);
                
                viewport.ZoomExtents();
                
                int triangleCount = mesh.TriangleIndices.Count / 3;
                StatusText.Text = $"Generated! {triangleCount:N0} triangles";
                Log($"Mesh generated with {triangleCount} triangles");
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error: " + ex.Message;
            Log($"Generation error: {ex.Message}", "ERROR");
            MessageBox.Show($"Error generating mesh:\n{ex.Message}", "Generation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Color_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_currentGeometry != null)
        {
            var mat = GetCurrentMaterial();
            _currentGeometry.Material = mat;
            _currentGeometry.BackMaterial = mat;
        }
    }

    private Material GetCurrentMaterial()
    {
        // Safe access to sliders. If not initialized, default to 200.
        byte r = (byte)(SliderR?.Value ?? 200);
        byte g = (byte)(SliderG?.Value ?? 200);
        byte b = (byte)(SliderB?.Value ?? 200);
        return new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(r, g, b)));
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentGeometry == null || _currentGeometry.Geometry is not MeshGeometry3D mesh)
        {
            MessageBox.Show("Generate a mesh first.");
            return;
        }

        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "STL Files (*.stl)|*.stl",
                DefaultExt = ".stl",
                FileName = $"Image3D_{Path.GetFileNameWithoutExtension(_currentImagePath)}.stl"
            };

            if (dialog.ShowDialog() == true)
            {
                StatusText.Text = "Exporting STL...";
                Log($"Exporting to {dialog.FileName}");
                ExportMeshToSTL(mesh, dialog.FileName);
                StatusText.Text = "Exported!";
                Log("Export complete");
                MessageBox.Show($"Model exported successfully to:\n{dialog.FileName}", "Export Complete");
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Export Error: " + ex.Message;
            Log($"Export error: {ex.Message}", "ERROR");
        }
    }

    private void ExportMeshToSTL(MeshGeometry3D mesh, string fileName)
    {
        using (var writer = new StreamWriter(fileName))
        {
            writer.WriteLine("solid image_export");

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

                // Handle NaN normals for degenerate triangles
                if (double.IsNaN(normal.X)) normal = new Vector3D(0, 1, 0);

                writer.WriteLine($"  facet normal {normal.X:F6} {normal.Y:F6} {normal.Z:F6}");
                writer.WriteLine("    outer loop");
                writer.WriteLine($"      vertex {p1.X:F6} {p1.Y:F6} {p1.Z:F6}");
                writer.WriteLine($"      vertex {p2.X:F6} {p2.Y:F6} {p2.Z:F6}");
                writer.WriteLine($"      vertex {p3.X:F6} {p3.Y:F6} {p3.Z:F6}");
                writer.WriteLine("    endloop");
                writer.WriteLine("  endfacet");
            }

            writer.WriteLine("endsolid image_export");
        }
    }
}

