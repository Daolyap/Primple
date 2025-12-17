using Primple.Core.Models;

namespace Primple.Core.Services;

public class ImageService : IImageService
{
    public Task<Mesh> ConvertImageToMeshAsync(string imagePath, ImageConversionMode mode, double height)
    {
        return Task.FromResult(new Mesh { Name = $"Image {Path.GetFileName(imagePath)}" });
    }
}
