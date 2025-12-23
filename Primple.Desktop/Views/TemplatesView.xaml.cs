using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using Primple.Core.Services;
using Primple.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using HelixToolkit.Wpf;

namespace Primple.Desktop.Views;

public partial class TemplatesView : UserControl
{
    private readonly ITemplateService _templateService;
    private readonly IProjectState _projectState;

    public TemplatesView()
    {
        InitializeComponent();
        
        if (App.AppHost != null)
        {
            _templateService = App.AppHost.Services.GetRequiredService<ITemplateService>();
            _projectState = App.AppHost.Services.GetRequiredService<IProjectState>();
        }
        else
        {
             // Fallback
             _templateService = new TemplateService();
             _projectState = new ProjectState();
        }
    }

    private void GenerateAndSet(MeshGeometry3D mesh, string name)
    {
        var material = new DiffuseMaterial(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204)));
        var model = new GeometryModel3D(mesh, material);
        model.BackMaterial = material;

        var group = new Model3DGroup();
        group.Children.Add(model);

        _projectState.UpdateModel(group);
        StatusText.Text = $"Created {name}! Switch to 'STL Editor' to view/edit.";
    }

    private void Template_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow == null) return;

            switch (tag)
            {
                // Map Templates
                case "EiffelTower":
                    NavigateToMap(mainWindow, 48.8584, 2.2945, "Eiffel Tower, Paris", 300);
                    break;
                case "TimesSquare":
                    NavigateToMap(mainWindow, 40.7580, -73.9855, "Times Square, NYC", 400);
                    break;
                case "Colosseum":
                    NavigateToMap(mainWindow, 41.8902, 12.4922, "Colosseum, Rome", 250);
                    break;
                case "BigBen":
                    NavigateToMap(mainWindow, 51.5007, -0.1246, "Big Ben, London", 300);
                    break;
                case "SydneyOpera":
                    NavigateToMap(mainWindow, -33.8568, 151.2153, "Sydney Opera House", 400);
                    break;
                case "TajMahal":
                    NavigateToMap(mainWindow, 27.1751, 78.0421, "Taj Mahal, Agra", 500);
                    break;
                    
                // Natural Features
                case "GrandCanyon":
                    NavigateToMapWithElevation(mainWindow, 36.1069, -112.1129, "Grand Canyon", 5000);
                    break;
                case "MtFuji":
                    NavigateToMapWithElevation(mainWindow, 35.3606, 138.7274, "Mount Fuji, Japan", 8000);
                    break;
                case "NiagaraFalls":
                    NavigateToMapWithElevation(mainWindow, 43.0896, -79.0849, "Niagara Falls", 1500);
                    break;
                    
                // Image to 3D Templates
                case "Logo3D":
                    NavigateToImageWithPreset(mainWindow, "Logo3D", "Logo preset applied. Import your logo (PNG with transparency works best).");
                    break;
                case "QRCode":
                    NavigateToImageWithPreset(mainWindow, "QRCode", "QR Code preset applied. Import a high-contrast QR code image.");
                    break;
                case "Lithophane":
                    NavigateToImageWithPreset(mainWindow, "Lithophane", "Lithophane preset applied. Print in white filament for best backlit effect.");
                    break;
                case "Topographic":
                    NavigateToImageWithPreset(mainWindow, "Topographic", "Topographic preset applied. Use grayscale heightmap image.");
                    break;
                case "CoinDesign":
                    NavigateToImageWithPreset(mainWindow, "CoinDesign", "Coin/Medal preset applied. Import a circular design image.");
                    break;
                case "TextRelief":
                    NavigateToImageWithPreset(mainWindow, "TextRelief", "Text Relief preset applied. Use white text on black background.");
                    break;
                case "HeightmapArt":
                    NavigateToImageWithPreset(mainWindow, "HeightmapArt", "Heightmap Art preset applied. Import grayscale image for artistic terrain.");
                    break;
            }
        }
    }

    private void NavigateToMap(MainWindow mainWindow, double lat, double lon, string name, double radius)
    {
        var mapsView = mainWindow.GetView("Maps") as MapsView;
        if (mapsView != null)
        {
            mapsView.SetLocation(lat, lon, name, radius);
            mainWindow.Navigate("Maps");
            StatusText.Text = $"Loaded: {name}";
        }
        else
        {
            StatusText.Text = "Error: Could not load Maps view.";
        }
    }

    private void NavigateToMapWithElevation(MainWindow mainWindow, double lat, double lon, string name, double radius)
    {
        var mapsView = mainWindow.GetView("Maps") as MapsView;
        if (mapsView != null)
        {
            mapsView.SetLocationWithElevation(lat, lon, name, radius);
            mainWindow.Navigate("Maps");
            StatusText.Text = $"Loaded: {name} (with elevation data)";
        }
    }

    private void NavigateToImageWithTip(MainWindow mainWindow, string tip)
    {
        mainWindow.Navigate("ImageTo3d");
        StatusText.Text = $"💡 Tip: {tip}";
    }

    private void NavigateToImageWithPreset(MainWindow mainWindow, string presetName, string tip)
    {
        var imageView = mainWindow.GetView("ImageTo3d") as ImageTo3dView;
        if (imageView != null)
        {
            imageView.ApplyPreset(presetName);
        }
        mainWindow.Navigate("ImageTo3d");
        StatusText.Text = $"💡 {tip}";
    }
}
