using Primple.Core.Models;

namespace Primple.Core.Services;

public enum ImageConversionMode
{
    Lithophane,
    Relief,
    Extrusion
}

public interface IImageService
{
    Task<Mesh> ConvertImageToMeshAsync(string imagePath, ImageConversionMode mode, double height);
}
