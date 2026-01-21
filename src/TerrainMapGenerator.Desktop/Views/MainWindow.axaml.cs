using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TerrainMapGenerator.Desktop.ViewModels;

namespace TerrainMapGenerator.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        var browseInputButton = this.FindControl<Button>("BrowseInputButton");
        var browseOutputButton = this.FindControl<Button>("BrowseOutputButton");

        if (browseInputButton != null)
        {
            browseInputButton.Click += async (_, _) =>
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Elevation Data File",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Elevation Data")
                        {
                            Patterns = new[] { "*.asc", "*.grd", "*.tif", "*.tiff", "*.hgt", "*.png", "*.xyz" }
                        },
                        FilePickerFileTypes.All
                    }
                });

                if (files.Count > 0 && DataContext is MainWindowViewModel vm)
                {
                    vm.InputFilePath = files[0].Path.LocalPath;
                }
            };
        }

        if (browseOutputButton != null)
        {
            browseOutputButton.Click += async (_, _) =>
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Terrain Map",
                    SuggestedFileName = "terrain",
                    DefaultExtension = "stl",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("STL File") { Patterns = new[] { "*.stl" } },
                        new FilePickerFileType("OBJ File") { Patterns = new[] { "*.obj" } },
                        new FilePickerFileType("3MF File") { Patterns = new[] { "*.3mf" } }
                    }
                });

                if (file != null && DataContext is MainWindowViewModel vm)
                {
                    vm.OutputFilePath = file.Path.LocalPath;
                }
            };
        }
    }
}
