using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace Primple.Core.Services;

public interface IMapsService
{
    Task<(double lat, double lon, string name)> SearchLocation(string query);
    Task<Model3DGroup> GenerateMapModel(double centerLat, double centerLon, double radiusMeters, bool includeBuildings, bool includeRoads, bool is3DMode);
}


