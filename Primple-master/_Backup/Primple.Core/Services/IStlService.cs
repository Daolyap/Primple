using Primple.Core.Models;

namespace Primple.Core.Services;

public interface IStlService
{
    Task<Mesh> LoadStlAsync(string filePath);
    Task SaveStlAsync(Mesh mesh, string filePath);
    Mesh Repair(Mesh mesh);
}
