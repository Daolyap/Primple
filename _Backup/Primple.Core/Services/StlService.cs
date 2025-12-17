using Primple.Core.Models;

namespace Primple.Core.Services;

public class StlService : IStlService
{
    public Task<Mesh> LoadStlAsync(string filePath)
    {
        // Mock implementation
        return Task.FromResult(new Mesh { Name = Path.GetFileName(filePath) });
    }

    public Task SaveStlAsync(Mesh mesh, string filePath)
    {
        return Task.CompletedTask;
    }

    public Mesh Repair(Mesh mesh)
    {
        return mesh; // returns same mesh
    }
}
