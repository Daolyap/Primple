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
                case "EiffelTower":
                    var mapsView = mainWindow.GetView("Maps") as MapsView;
                    if (mapsView != null)
                    {
                        mapsView.SetLocation(48.8584, 2.2945, "Eiffel Tower, Paris", 300);
                        mainWindow.Navigate("Maps");
                    }
                    break;
                case "TimesSquare":
                    var mapsView2 = mainWindow.GetView("Maps") as MapsView;
                    if (mapsView2 != null)
                    {
                        mapsView2.SetLocation(40.7580, -73.9855, "Times Square, NYC", 400);
                        mainWindow.Navigate("Maps");
                    }
                    break;
                case "Colosseum":
                    var mapsView3 = mainWindow.GetView("Maps") as MapsView;
                    if (mapsView3 != null)
                    {
                        mapsView3.SetLocation(41.8902, 12.4922, "Colosseum, Rome", 250);
                        mainWindow.Navigate("Maps");
                    }
                    break;
                case "Logo3D":
                    mainWindow.Navigate("ImageTo3d");
                    break;
                case "HeightmapArt":
                    mainWindow.Navigate("ImageTo3d");
                    break;
            }
        }
    }
}
