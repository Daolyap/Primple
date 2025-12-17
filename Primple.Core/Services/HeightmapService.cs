using System.Windows.Media.Media3D;
using System.Drawing;
using System.IO;

namespace Primple.Core.Services;

public interface IHeightmapService
{
    MeshGeometry3D GenerateHeightmap(string imagePath, double heightScale, int resolution);
}

public class HeightmapService : IHeightmapService
{
    public MeshGeometry3D GenerateHeightmap(string imagePath, double heightScale, int resolution)
    {
        // Require System.Drawing.Common for Bitmap processing
        // Note: In .NET 6+, typical use requires [SupportedOSPlatform("windows")]
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Heightmap generation is only supported on Windows.");

        using var bitmap = new Bitmap(imagePath);
        
        // Resize for performance/resolution
        using var resized = new Bitmap(bitmap, new Size(resolution, resolution));

        var mesh = new MeshGeometry3D();
        int width = resized.Width;
        int height = resized.Height;

        // Generate positions
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = resized.GetPixel(x, y);
                // Simple brightness calculation: (R+G+B)/3 / 255
                double brightness = (pixel.R + pixel.G + pixel.B) / (3.0 * 255.0);
                
                // Map to 3D space: X and Y are normalized to roughly 0-10 or 0-resolution range
                // Let's normalize X/Y to 0..1 then scale by arbitrary size (e.g. 10 units)
                double xPos = (double)x / width * 10.0;
                double yPos = (double)y / height * 10.0; // Actually Z in WPF 3D usually, or Y is up. 
                                                         // In standard 3D printers, Z is up. 
                                                         // Let's use X/Z plane as ground, Y as height.
                
                double zHeight = brightness * heightScale;

                mesh.Positions.Add(new Point3D(xPos, zHeight, (double)y / height * 10.0));
                
                // Texture coordinates (UV)
                mesh.TextureCoordinates.Add(new System.Windows.Point((double)x / width, (double)y / height));
            }
        }

        // Generate triangles
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                int i0 = y * width + x;
                int i1 = i0 + 1;
                int i2 = (y + 1) * width + x;
                int i3 = i2 + 1;

                // Triangle 1
                mesh.TriangleIndices.Add(i0);
                mesh.TriangleIndices.Add(i2);
                mesh.TriangleIndices.Add(i1);

                // Triangle 2
                mesh.TriangleIndices.Add(i1);
                mesh.TriangleIndices.Add(i2);
                mesh.TriangleIndices.Add(i3);
            }
        }

        return mesh;
    }
}
