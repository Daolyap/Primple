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
                    NavigateToImageWithTip(mainWindow, "Import your company logo (PNG with transparency works best). Use 'Plane' projection and adjust height scale for relief depth.");
                    break;
                case "QRCode":
                    NavigateToImageWithTip(mainWindow, "Import a high-contrast QR code image. Use 'Plane' or 'Cube' projection. Low height scale (0.2-0.5) works best for scannable results.");
                    break;
                case "Lithophane":
                    NavigateToImageWithTip(mainWindow, "Import any photo. Use 'Plane' projection with inverted colors (or process image beforehand). Print in white filament for best backlit effect.");
                    break;
                case "Topographic":
                    NavigateToImageWithTip(mainWindow, "Import a grayscale heightmap image. Black = low elevation, White = high elevation. Use 'Plane' projection with higher height scale.");
                    break;
                case "CoinDesign":
                    NavigateToImageWithTip(mainWindow, "Import a circular design image. Use 'Plane' projection with 'Add Base' enabled. Keep height scale low (0.3-0.8) for realistic coin depth.");
                    break;
                case "TextRelief":
                    NavigateToImageWithTip(mainWindow, "Create an image with text in white on black background. Use 'Plane' projection. Text will be raised on the model surface.");
                    break;
                case "HeightmapArt":
                    NavigateToImageWithTip(mainWindow, "Import any grayscale image for artistic terrain. Use 'Plane' projection with high resolution for smooth landscapes.");
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
}
