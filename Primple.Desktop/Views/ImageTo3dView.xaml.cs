using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using HelixToolkit.Wpf;
using Primple.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Primple.Desktop.Views;

public partial class ImageTo3dView : UserControl
{
    private string? _selectedImagePath;
    private readonly IHeightmapService _heightmapService;

    public ImageTo3dView()
    {
        InitializeComponent();
        if (App.AppHost != null)
        {
            _heightmapService = App.AppHost.Services.GetRequiredService<IHeightmapService>();
        }
        else
        {
            // Fallback for design time or error
             _heightmapService = new HeightmapService();
        }
    }

    private void SelectImage_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|All Files (*.*)|*.*",
            Title = "Select Image"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            _selectedImagePath = openFileDialog.FileName;
            StatusText.Text = $"Selected: {System.IO.Path.GetFileName(_selectedImagePath)}";
        }
    }

    private void Settings_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Optional: Auto-update or just wait for Generate button
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedImagePath))
        {
            MessageBox.Show("Please select an image first.", "Primple Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            StatusText.Text = "Generating mesh...";
            
            double scale = HeightSlider.Value;
            int resolution = (int)ResolutionSlider.Value;

            var mesh = _heightmapService.GenerateHeightmap(_selectedImagePath, scale, resolution);

            var material = new DiffuseMaterial(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 100, 0))); // Orange-ish
            var model = new GeometryModel3D(mesh, material);
            model.BackMaterial = material;

            var visual = new ModelVisual3D();
            visual.Content = model;

            modelContainer.Children.Clear();
            modelContainer.Children.Add(visual);
            
            viewport.ZoomExtents();
            StatusText.Text = "Mesh generated successfully.";
        }
        catch (Exception ex)
        {
             MessageBox.Show($"Error generating mesh: {ex.Message}", "Generation Error", MessageBoxButton.OK, MessageBoxImage.Error);
             StatusText.Text = "Error";
        }
    }
}
