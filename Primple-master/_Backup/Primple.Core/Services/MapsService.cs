using Primple.Core.Models;

namespace Primple.Core.Services;

public class MapsService : IMapsService
{
    public Task<Mesh> GenerateMapAsync(double lat, double lon, double radiusKm, MapConfig config)
    {
        return Task.FromResult(new Mesh { Name = $"Map {lat},{lon}" });
    }
}
