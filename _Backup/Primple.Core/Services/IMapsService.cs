using Primple.Core.Models;

namespace Primple.Core.Services;

public class MapConfig
{
    public bool Enable3D { get; set; }
    public double HeightScale { get; set; } = 1.0;
    public string BaseColor { get; set; } = "#FFFFFF";
    public string FeatureColor { get; set; } = "#000000";
}

public interface IMapsService
{
    Task<Mesh> GenerateMapAsync(double lat, double lon, double radiusKm, MapConfig config);
}
