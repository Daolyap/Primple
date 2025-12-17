
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

    public ImageTo3dView()
    {
        InitializeComponent();
    }

    private void SelectImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dlg.ShowDialog() == true)
        {
            _currentImagePath = dlg.FileName;
            StatusText.Text = Path.GetFileName(_currentImagePath);
        }
    }

    private void Settings_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
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
            if (App.AppHost != null)
            {
                var service = App.AppHost.Services.GetRequiredService<IHeightmapService>();
                var mesh = service.GenerateHeightmap(_currentImagePath, HeightSlider.Value, (int)ResolutionSlider.Value);
                
                var material = GetCurrentMaterial();
                _currentGeometry = new GeometryModel3D(mesh, material);
                _currentGeometry.BackMaterial = material; 

                var group = new Model3DGroup();
                group.Children.Add(_currentGeometry);
                
                var visual = new ModelVisual3D { Content = group };
                modelContainer.Children.Clear();
                modelContainer.Children.Add(visual);
                
                viewport.ZoomExtents();
                StatusText.Text = "Generated!";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error: " + ex.Message;
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

    private void SendToEditor_Click(object sender, RoutedEventArgs e)
    {
        if (_currentGeometry == null)
        {
            MessageBox.Show("Generate a mesh first.");
            return;
        }

        if (App.AppHost != null)
        {
            var projectState = App.AppHost.Services.GetRequiredService<IProjectState>();
            var group = new Model3DGroup();
            group.Children.Add(_currentGeometry);
            
            projectState.UpdateModel(group);
            projectState.CurrentProjectName = "Image3D_" + Path.GetFileNameWithoutExtension(_currentImagePath);
            
            MessageBox.Show("Model sent to STL Editor.", "Success");
        }
    }
}

