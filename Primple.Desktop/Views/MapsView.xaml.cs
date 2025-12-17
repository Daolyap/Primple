using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using Primple.Core.Services;
using Microsoft.Extensions.DependencyInjection; // Ensure using for GetService/GetRequiredService

namespace Primple.Desktop.Views;

public partial class MapsView : UserControl
{
    private double _currentLat;
    private double _currentLon;
    private bool _hasLocation;
    private Model3DGroup? _currentModel;

    public MapsView()
    {
        InitializeComponent();
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await PerformSearch();
    }

    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await PerformSearch();
        }
    }

    private async Task PerformSearch()
    {
        string query = SearchBox.Text;
        if (string.IsNullOrWhiteSpace(query)) return;

        SetLoading(true, "Searching...");
        
        try
        {
            if (App.AppHost != null)
            {
                var mapsService = App.AppHost.Services.GetRequiredService<IMapsService>();
                var (lat, lon, name) = await mapsService.SearchLocation(query);
                
                if (name != null)
                {
                    _currentLat = lat;
                    _currentLon = lon;
                    _hasLocation = true;
                    LocationText.Text = name;
                    StatusText.Text = "Location found.";
                }
                else
                {
                    StatusText.Text = "Location not found.";
                }
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error: " + ex.Message;
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasLocation)
        {
            MessageBox.Show("Please search for a location first.");
            return;
        }

        SetLoading(true, "Fetching data & Generating...");

        try
        {
            if (App.AppHost != null)
            {
                var mapsService = App.AppHost.Services.GetRequiredService<IMapsService>();
                
                double radius = RadiusSlider.Value;
                bool buildings = CheckBuildings.IsChecked == true;
                bool roads = CheckRoads.IsChecked == true;
                bool is3D = Mode3D.IsChecked == true;

                _currentModel = await mapsService.GenerateMapModel(_currentLat, _currentLon, radius, buildings, roads, is3D);
                
                // Update Preview
                var visual = new ModelVisual3D();
                visual.Content = _currentModel;
                previewVisual.Children.Clear();
                previewVisual.Children.Add(visual);
                
                viewport.ZoomExtents();
                StatusText.Text = "Model Generated.";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Generation Error: " + ex.Message;
            MessageBox.Show(ex.ToString());
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SendToEditor_Click(object sender, RoutedEventArgs e)
    {
        if (_currentModel == null)
        {
            MessageBox.Show("Generate a model first.");
            return;
        }

        if (App.AppHost != null)
        {
            var projectState = App.AppHost.Services.GetRequiredService<IProjectState>();
            projectState.UpdateModel(_currentModel);
            projectState.CurrentProjectName = "Map_" + SearchBox.Text;
            
            // Should navigate to Editor or notify
            StatusText.Text = "Sent to Editor!";
            MessageBox.Show("Model sent to STL Editor.", "Success");
        }
    }

    private void SetLoading(bool isLoading, string msg = "")
    {
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        if (isLoading && !string.IsNullOrEmpty(msg)) StatusText.Text = msg;
    }
}
