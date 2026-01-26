using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Primple.Core.Models;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Primple.App.ViewModels;

/// <summary>
/// View model for the 3D terrain preview.
/// </summary>
public partial class TerrainPreviewViewModel : ObservableObject
{
    [ObservableProperty]
    private Model3DGroup? _model3D;

    [ObservableProperty]
    private Point3D _cameraPosition = new(150, 150, 200);

    [ObservableProperty]
    private Vector3D _cameraLookDirection = new(-1, -1, -1);

    [ObservableProperty]
    private Vector3D _cameraUpDirection = new(0, 0, 1);

    [ObservableProperty]
    private double _cameraFieldOfView = 45;

    [ObservableProperty]
    private bool _showWireframe;

    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _showAxes = true;

    [ObservableProperty]
    private string _viewMode = "Perspective";

    // Lighting
    [ObservableProperty]
    private Color _ambientLightColor = Color.FromRgb(60, 60, 60);

    [ObservableProperty]
    private Color _directionalLightColor = Color.FromRgb(255, 255, 255);

    [ObservableProperty]
    private Vector3D _lightDirection = new(-1, -1, -1);

    // Terrain color
    [ObservableProperty]
    private Color _terrainBaseColor = Color.FromRgb(100, 180, 100);

    [ObservableProperty]
    private Color _terrainPeakColor = Color.FromRgb(255, 255, 255);

    [ObservableProperty]
    private bool _useElevationColoring = true;

    public TerrainPreviewViewModel()
    {
        Model3D = new Model3DGroup();
        CreateDefaultScene();
    }

    private void CreateDefaultScene()
    {
        var group = new Model3DGroup();

        // Add ambient light
        group.Children.Add(new AmbientLight(AmbientLightColor));

        // Add directional light
        group.Children.Add(new DirectionalLight(DirectionalLightColor, LightDirection));

        // Add base plane
        if (ShowGrid)
        {
            var gridMesh = CreateGridMesh(256, 256, 10);
            var gridMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(50, 100, 100, 100)));
            group.Children.Add(new GeometryModel3D(gridMesh, gridMaterial));
        }

        Model3D = group;
    }

    public void UpdateMesh(TerrainMesh mesh)
    {
        if (mesh == null || mesh.Vertices.Count == 0) return;

        var group = new Model3DGroup();

        // Lighting
        group.Children.Add(new AmbientLight(AmbientLightColor));
        group.Children.Add(new DirectionalLight(DirectionalLightColor, LightDirection));

        // Create WPF 3D mesh from terrain mesh
        var mesh3D = new MeshGeometry3D();
        var (minBounds, maxBounds) = mesh.GetBounds();

        // Add vertices
        foreach (var vertex in mesh.Vertices)
        {
            mesh3D.Positions.Add(new Point3D(vertex.X, vertex.Y, vertex.Z));
        }

        // Add triangles
        foreach (var index in mesh.Indices)
        {
            mesh3D.TriangleIndices.Add(index);
        }

        // Add normals if available
        if (mesh.Normals.Count == mesh.Vertices.Count)
        {
            foreach (var normal in mesh.Normals)
            {
                mesh3D.Normals.Add(new Vector3D(normal.X, normal.Y, normal.Z));
            }
        }

        // Create material with gradient based on height
        Material material;
        if (UseElevationColoring)
        {
            // Calculate texture coordinates based on height
            var heightRange = maxBounds.Z - minBounds.Z;
            foreach (var vertex in mesh.Vertices)
            {
                var normalizedHeight = heightRange > 0 ? (vertex.Z - minBounds.Z) / heightRange : 0.5f;
                mesh3D.TextureCoordinates.Add(new System.Windows.Point(normalizedHeight, 0.5));
            }

            // Create gradient brush
            var gradientBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(50, 100, 50), 0.0),    // Deep valleys - dark green
                    new GradientStop(TerrainBaseColor, 0.3),              // Low terrain - green
                    new GradientStop(Color.FromRgb(139, 119, 101), 0.5), // Mid terrain - brown
                    new GradientStop(Color.FromRgb(139, 137, 137), 0.7), // High terrain - gray
                    new GradientStop(TerrainPeakColor, 1.0)              // Peaks - white
                },
                new System.Windows.Point(0, 0.5),
                new System.Windows.Point(1, 0.5));

            material = new DiffuseMaterial(gradientBrush);
        }
        else
        {
            material = new DiffuseMaterial(new SolidColorBrush(TerrainBaseColor));
        }

        var geometryModel = new GeometryModel3D(mesh3D, material);
        geometryModel.BackMaterial = material;
        group.Children.Add(geometryModel);

        // Add grid if enabled
        if (ShowGrid)
        {
            var gridMesh = CreateGridMesh((float)maxBounds.X, (float)maxBounds.Y, 10);
            var gridMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(30, 100, 100, 100)));
            group.Children.Add(new GeometryModel3D(gridMesh, gridMaterial) { Transform = new TranslateTransform3D(0, 0, -0.1) });
        }

        Model3D = group;

        // Update camera to look at center of model
        var center = new Point3D(
            (minBounds.X + maxBounds.X) / 2,
            (minBounds.Y + maxBounds.Y) / 2,
            (minBounds.Z + maxBounds.Z) / 2);

        var size = Math.Max(maxBounds.X - minBounds.X, maxBounds.Y - minBounds.Y);
        CameraPosition = new Point3D(center.X + size, center.Y + size, center.Z + size * 0.8);
        CameraLookDirection = new Vector3D(center.X - CameraPosition.X, center.Y - CameraPosition.Y, center.Z - CameraPosition.Z);
    }

    private MeshGeometry3D CreateGridMesh(float width, float height, int divisions)
    {
        var mesh = new MeshGeometry3D();
        var cellWidth = width / divisions;
        var cellHeight = height / divisions;

        for (int x = 0; x <= divisions; x++)
        {
            for (int y = 0; y <= divisions; y++)
            {
                mesh.Positions.Add(new Point3D(x * cellWidth, y * cellHeight, 0));
            }
        }

        for (int x = 0; x < divisions; x++)
        {
            for (int y = 0; y < divisions; y++)
            {
                var topLeft = x * (divisions + 1) + y;
                var topRight = topLeft + 1;
                var bottomLeft = topLeft + (divisions + 1);
                var bottomRight = bottomLeft + 1;

                mesh.TriangleIndices.Add(topLeft);
                mesh.TriangleIndices.Add(bottomLeft);
                mesh.TriangleIndices.Add(topRight);

                mesh.TriangleIndices.Add(topRight);
                mesh.TriangleIndices.Add(bottomLeft);
                mesh.TriangleIndices.Add(bottomRight);
            }
        }

        return mesh;
    }

    public void ClearMesh()
    {
        CreateDefaultScene();
    }

    [RelayCommand]
    private void ResetCamera()
    {
        CameraPosition = new Point3D(150, 150, 200);
        CameraLookDirection = new Vector3D(-1, -1, -1);
        CameraUpDirection = new Vector3D(0, 0, 1);
        CameraFieldOfView = 45;
    }

    [RelayCommand]
    private void SetTopView()
    {
        CameraPosition = new Point3D(128, 128, 300);
        CameraLookDirection = new Vector3D(0, 0, -1);
        CameraUpDirection = new Vector3D(0, 1, 0);
        ViewMode = "Top";
    }

    [RelayCommand]
    private void SetFrontView()
    {
        CameraPosition = new Point3D(128, -200, 50);
        CameraLookDirection = new Vector3D(0, 1, 0);
        CameraUpDirection = new Vector3D(0, 0, 1);
        ViewMode = "Front";
    }

    [RelayCommand]
    private void SetSideView()
    {
        CameraPosition = new Point3D(-200, 128, 50);
        CameraLookDirection = new Vector3D(1, 0, 0);
        CameraUpDirection = new Vector3D(0, 0, 1);
        ViewMode = "Side";
    }

    [RelayCommand]
    private void SetPerspectiveView()
    {
        CameraPosition = new Point3D(200, 200, 150);
        CameraLookDirection = new Vector3D(-1, -1, -0.5);
        CameraUpDirection = new Vector3D(0, 0, 1);
        ViewMode = "Perspective";
    }
}
