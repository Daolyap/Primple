using System.Globalization;
using System.Text.RegularExpressions;
using Flurl.Http;
using Primple.Core.Interfaces;
using Primple.Core.Models;

namespace Primple.Core.Services;

/// <summary>
/// Service for geocoding and location search using Nominatim (OpenStreetMap).
/// </summary>
public class GeocodingService : IGeocodingService
{
    private const string NominatimSearchUrl = "https://nominatim.openstreetmap.org/search";
    private const string NominatimReverseUrl = "https://nominatim.openstreetmap.org/reverse";
    private const string UserAgent = "Primple/1.0 (Terrain Map Generator)";

    /// <summary>
    /// Search for locations by name or address.
    /// </summary>
    public async Task<IEnumerable<LocationResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<LocationResult>();

        // Check if query is coordinates
        var coordResult = TryParseCoordinates(query);
        if (coordResult != null)
        {
            return new[] { coordResult };
        }

        try
        {
            var response = await NominatimSearchUrl
                .WithHeader("User-Agent", UserAgent)
                .SetQueryParams(new
                {
                    q = query,
                    format = "json",
                    addressdetails = 1,
                    limit = 10,
                    polygon_geojson = 0
                })
                .GetJsonAsync<List<NominatimSearchResult>>(cancellationToken: cancellationToken);

            return response.Select(r => new LocationResult
            {
                Name = r.Name ?? r.DisplayName?.Split(',').FirstOrDefault() ?? "Unknown",
                DisplayName = r.DisplayName ?? "",
                Latitude = double.Parse(r.Lat, CultureInfo.InvariantCulture),
                Longitude = double.Parse(r.Lon, CultureInfo.InvariantCulture),
                Type = r.Type ?? r.Class ?? "place",
                Bounds = r.BoundingBox != null && r.BoundingBox.Length == 4
                    ? new GeographicBounds(
                        double.Parse(r.BoundingBox[1], CultureInfo.InvariantCulture),
                        double.Parse(r.BoundingBox[0], CultureInfo.InvariantCulture),
                        double.Parse(r.BoundingBox[3], CultureInfo.InvariantCulture),
                        double.Parse(r.BoundingBox[2], CultureInfo.InvariantCulture))
                    : null
            });
        }
        catch (FlurlHttpException)
        {
            // Return empty on network error
            return Enumerable.Empty<LocationResult>();
        }
    }

    /// <summary>
    /// Reverse geocode coordinates to get place name.
    /// </summary>
    public async Task<string?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await NominatimReverseUrl
                .WithHeader("User-Agent", UserAgent)
                .SetQueryParams(new
                {
                    lat = latitude,
                    lon = longitude,
                    format = "json",
                    zoom = 10
                })
                .GetJsonAsync<NominatimSearchResult>(cancellationToken: cancellationToken);

            return response.DisplayName;
        }
        catch (FlurlHttpException)
        {
            return null;
        }
    }

    /// <summary>
    /// Try to parse coordinate strings in various formats.
    /// </summary>
    private LocationResult? TryParseCoordinates(string input)
    {
        // Decimal degrees: 47.6062, -122.3321
        var decimalMatch = Regex.Match(input, @"^\s*(-?\d+\.?\d*)\s*[,\s]+\s*(-?\d+\.?\d*)\s*$");
        if (decimalMatch.Success)
        {
            var lat = double.Parse(decimalMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var lon = double.Parse(decimalMatch.Groups[2].Value, CultureInfo.InvariantCulture);

            if (lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180)
            {
                return new LocationResult
                {
                    Name = $"{lat:F4}°, {lon:F4}°",
                    DisplayName = $"Coordinates: {lat:F6}, {lon:F6}",
                    Latitude = lat,
                    Longitude = lon,
                    Type = "coordinate"
                };
            }
        }

        // DMS: 47°36'22"N 122°19'55"W
        var dmsMatch = Regex.Match(input,
            @"(\d+)°\s*(\d+)[''′]\s*(\d+(?:\.\d+)?)[""″]?\s*([NS])\s*,?\s*(\d+)°\s*(\d+)[''′]\s*(\d+(?:\.\d+)?)[""″]?\s*([EW])",
            RegexOptions.IgnoreCase);
        if (dmsMatch.Success)
        {
            var latDeg = int.Parse(dmsMatch.Groups[1].Value);
            var latMin = int.Parse(dmsMatch.Groups[2].Value);
            var latSec = double.Parse(dmsMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            var latDir = dmsMatch.Groups[4].Value.ToUpper();

            var lonDeg = int.Parse(dmsMatch.Groups[5].Value);
            var lonMin = int.Parse(dmsMatch.Groups[6].Value);
            var lonSec = double.Parse(dmsMatch.Groups[7].Value, CultureInfo.InvariantCulture);
            var lonDir = dmsMatch.Groups[8].Value.ToUpper();

            var lat = latDeg + latMin / 60.0 + latSec / 3600.0;
            var lon = lonDeg + lonMin / 60.0 + lonSec / 3600.0;

            if (latDir == "S") lat = -lat;
            if (lonDir == "W") lon = -lon;

            return new LocationResult
            {
                Name = $"{Math.Abs(lat):F4}°{(lat >= 0 ? "N" : "S")}, {Math.Abs(lon):F4}°{(lon >= 0 ? "E" : "W")}",
                DisplayName = $"Coordinates: {lat:F6}, {lon:F6}",
                Latitude = lat,
                Longitude = lon,
                Type = "coordinate"
            };
        }

        return null;
    }

    private class NominatimSearchResult
    {
        public string? PlaceId { get; set; }
        public string? Licence { get; set; }
        public string? OsmType { get; set; }
        public string? OsmId { get; set; }
        public string[]? BoundingBox { get; set; }
        public string? Lat { get; set; }
        public string? Lon { get; set; }
        public string? DisplayName { get; set; }
        public string? Class { get; set; }
        public string? Type { get; set; }
        public string? Name { get; set; }
        public double? Importance { get; set; }
    }
}
