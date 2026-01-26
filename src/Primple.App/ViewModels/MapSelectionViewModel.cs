using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Primple.Core.Interfaces;
using Primple.Core.Models;
using System.Collections.ObjectModel;

namespace Primple.App.ViewModels;

/// <summary>
/// View model for map selection panel.
/// </summary>
public partial class MapSelectionViewModel : ObservableObject
{
    private readonly IGeocodingService _geocodingService;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private ObservableCollection<LocationResult> _searchResults;

    [ObservableProperty]
    private LocationResult? _selectedLocation;

    [ObservableProperty]
    private GeographicBounds? _selectedBounds;

    [ObservableProperty]
    private double _centerLatitude = 47.6062;

    [ObservableProperty]
    private double _centerLongitude = -122.3321;

    [ObservableProperty]
    private double _zoomLevel = 10;

    [ObservableProperty]
    private double _selectionSizeKm = 10;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _coordinateDisplay = "";

    [ObservableProperty]
    private ObservableCollection<LocationResult> _recentLocations;

    public MapSelectionViewModel(IGeocodingService geocodingService)
    {
        _geocodingService = geocodingService;
        _searchResults = new ObservableCollection<LocationResult>();
        _recentLocations = new ObservableCollection<LocationResult>();

        // Initialize with default selection (Seattle area for demo)
        UpdateBoundsFromCenter();
    }

    partial void OnCenterLatitudeChanged(double value) => UpdateBoundsFromCenter();
    partial void OnCenterLongitudeChanged(double value) => UpdateBoundsFromCenter();
    partial void OnSelectionSizeKmChanged(double value) => UpdateBoundsFromCenter();

    private void UpdateBoundsFromCenter()
    {
        // Convert km to degrees (approximate)
        var latOffset = SelectionSizeKm / 111.0; // ~111 km per degree latitude
        var lonOffset = SelectionSizeKm / (111.0 * Math.Cos(CenterLatitude * Math.PI / 180));

        SelectedBounds = new GeographicBounds(
            CenterLatitude + latOffset / 2,
            CenterLatitude - latOffset / 2,
            CenterLongitude + lonOffset / 2,
            CenterLongitude - lonOffset / 2);

        CoordinateDisplay = $"{CenterLatitude:F4}°, {CenterLongitude:F4}°";
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        try
        {
            IsSearching = true;
            SearchResults.Clear();

            var results = await _geocodingService.SearchAsync(SearchQuery);
            foreach (var result in results)
            {
                SearchResults.Add(result);
            }
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private void SelectLocation(LocationResult? location)
    {
        if (location == null) return;

        SelectedLocation = location;
        CenterLatitude = location.Latitude;
        CenterLongitude = location.Longitude;

        if (location.Bounds != null)
        {
            SelectedBounds = location.Bounds;
            // Calculate zoom level based on bounds
            var size = Math.Max(location.Bounds.Width, location.Bounds.Height);
            ZoomLevel = Math.Max(1, 15 - Math.Log2(size * 111));
        }

        // Add to recent locations
        if (!RecentLocations.Any(l => l.Latitude == location.Latitude && l.Longitude == location.Longitude))
        {
            RecentLocations.Insert(0, location);
            if (RecentLocations.Count > 10)
                RecentLocations.RemoveAt(RecentLocations.Count - 1);
        }

        CoordinateDisplay = $"{location.Latitude:F4}°, {location.Longitude:F4}° - {location.Name}";
    }

    [RelayCommand]
    private void SetCoordinates(string? coords)
    {
        if (string.IsNullOrWhiteSpace(coords)) return;

        var parts = coords.Split(',');
        if (parts.Length == 2 &&
            double.TryParse(parts[0].Trim(), out var lat) &&
            double.TryParse(parts[1].Trim(), out var lon))
        {
            CenterLatitude = lat;
            CenterLongitude = lon;
            UpdateBoundsFromCenter();
        }
    }

    public void ClearSelection()
    {
        SelectedLocation = null;
        SelectedBounds = null;
        SearchQuery = "";
        SearchResults.Clear();
    }
}
