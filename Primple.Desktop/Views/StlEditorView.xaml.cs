using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using HelixToolkit.Wpf;
using System.IO;
using Primple.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Primple.Desktop.Views;

public partial class StlEditorView : UserControl
{
    private const string DefaultStatus = "Ready to print simple.";
    private readonly IProjectState? _projectState;

    public StlEditorView()
    {
        InitializeComponent();
        StatusText.Text = DefaultStatus;

        if (App.AppHost != null)
        {
            _projectState = App.AppHost.Services.GetService<IProjectState>();
            if (_projectState != null)
            {
                _projectState.PropertyChanged += OnProjectStateChanged;
                // Load initial
                if (_projectState.CurrentModel.Children.Count > 0)
                {
                    LoadModelGroup(_projectState.CurrentModel);
                }
            }
        }
    }

    private void OnProjectStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IProjectState.CurrentModel) && _projectState != null)
        {
            LoadModelGroup(_projectState.CurrentModel);
        }
    }

    private void LoadModelGroup(Model3DGroup group)
    {
        var visual = new ModelVisual3D();
        visual.Content = group;
        modelContainer.Children.Clear();
        modelContainer.Children.Add(visual);
        viewport.ZoomExtents();
        StatusText.Text = "Model Loaded from Project";
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "STL Files (*.stl)|*.stl|All Files (*.*)|*.*",
            Title = "Import STL Model"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LoadStl(openFileDialog.FileName);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        modelContainer.Children.Clear();
        StatusText.Text = DefaultStatus;
    }

    private void LoadStl(string filePath)
    {
        try
        {
            StatusText.Text = $"Loading {Path.GetFileName(filePath)}...";
            
            var importer = new ModelImporter();
            var modelGroup = importer.Load(filePath);
            
            if (modelGroup != null)
            {
                ApplyMaterial(modelGroup);
                
                if (_projectState != null)
                    _projectState.UpdateModel(modelGroup);
                else
                    LoadModelGroup(modelGroup);
                
               StatusText.Text = $"Loaded: {Path.GetFileName(filePath)}";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading STL: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Import Failed";
        }
    }

    private void ApplyMaterial(Model3DGroup group)
    {
        foreach (var child in group.Children)
        {
            if (child is GeometryModel3D gm)
            {
                var materialGroup = new MaterialGroup();
                materialGroup.Children.Add(new DiffuseMaterial(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204))));
                materialGroup.Children.Add(new SpecularMaterial(System.Windows.Media.Brushes.White, 100));
                gm.Material = materialGroup;
                gm.BackMaterial = materialGroup;
            }
        }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        if (modelContainer.Children.Count == 0 || (modelContainer.Content as Model3DGroup)?.Children.Count == 0)
        {
            MessageBox.Show("No model to save.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var saveDialog = new SaveFileDialog
        {
            Filter = "Primple Project (*.fig)|*.fig",
            Title = "Save Project",
            FileName = _projectState?.CurrentProjectName ?? "MyProject"
        };
        
        if (saveDialog.ShowDialog() == true && App.AppHost != null)
        {
             try
             {
                 var figService = App.AppHost.Services.GetRequiredService<Primple.Desktop.Services.IFigService>();
                 // We need to grab the current model from state or viewport. 
                 // Best to use ProjectState, but if modified locally... assuming strict MVVM or Sync.
                 // Let's grab from Viewport container content to be WYSIWYG
                 if (modelContainer.Content is Model3DGroup group)
                 {
                     figService.SaveProject(saveDialog.FileName, group, Path.GetFileNameWithoutExtension(saveDialog.FileName));
                     StatusText.Text = "Project Saved!";
                 }
             }
             catch(Exception ex)
             {
                 MessageBox.Show($"Save Failed: {ex.Message}");
             }
        }
    }

    private void LoadProject_Click(object sender, RoutedEventArgs e)
    {
        var openDialog = new OpenFileDialog
        {
            Filter = "Primple Project (*.fig)|*.fig",
            Title = "Load Project"
        };
        
        if (openDialog.ShowDialog() == true && App.AppHost != null)
        {
            try
            {
                var figService = App.AppHost.Services.GetRequiredService<Primple.Desktop.Services.IFigService>();
                var model = figService.LoadProject(openDialog.FileName);
                
                if (_projectState != null)
                {
                    _projectState.UpdateModel(model);
                    _projectState.CurrentProjectName = Path.GetFileNameWithoutExtension(openDialog.FileName);
                }
                else
                {
                     LoadModelGroup(model);
                }
                StatusText.Text = "Project Loaded!";
            }
             catch(Exception ex)
             {
                 MessageBox.Show($"Load Failed: {ex.Message}");
             }
        }
    }
}
