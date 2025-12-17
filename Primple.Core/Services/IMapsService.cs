using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Windows.Media;

namespace Primple.Core.Services;

public interface IMapsService
{
    Task<(double lat, double lon, string name)> SearchLocation(string query);
    Task<Model3DGroup> GenerateMapModel(MapGenerationOptions options);
}


