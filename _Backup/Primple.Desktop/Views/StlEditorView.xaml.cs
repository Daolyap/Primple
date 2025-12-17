using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using HelixToolkit.Wpf;

namespace Primple.Desktop.Views;

public partial class StlEditorView : UserControl
{
    public StlEditorView()
    {
        InitializeComponent();
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

    private void LoadStl(string filePath)
    {
        try
        {
            var importer = new ModelImporter();
            var modelGroup = importer.Load(filePath);
            
            var modelVisual = new ModelVisual3D();
            modelVisual.Content = modelGroup;

            // Clear previous models if any (optional, assuming single model editor for now)
            // modelContainer is defined in XAML
            modelContainer.Children.Clear();
            modelContainer.Children.Add(modelVisual);

            // Reset camera to fit view
            viewport.ZoomExtents();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading STL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
