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

    private void Cube_Click(object sender, RoutedEventArgs e)
    {
        var mesh = _templateService.CreateCube(5);
        GenerateAndSet(mesh, "Cube");
    }

    private void Sphere_Click(object sender, RoutedEventArgs e)
    {
        var mesh = _templateService.CreateSphere(2.5);
        GenerateAndSet(mesh, "Sphere");
    }

    private void Cylinder_Click(object sender, RoutedEventArgs e)
    {
        var mesh = _templateService.CreateCylinder(2.5, 5);
        GenerateAndSet(mesh, "Cylinder");
    }
}
