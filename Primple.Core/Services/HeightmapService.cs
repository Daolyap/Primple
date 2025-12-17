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
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Heightmap generation is only supported on Windows.");

        using var bitmap = new Bitmap(imagePath);
        using var resized = new Bitmap(bitmap, new Size(resolution, resolution));

        var mesh = new MeshGeometry3D();
        int width = resized.Width;
        int height = resized.Height;

        bool[,] isValid = new bool[width, height];

        // Generate positions
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = resized.GetPixel(x, y);
                
                // Transparency Check (Alpha)
                if (pixel.A < 20)
                {
                    isValid[x, y] = false;
                    // Add dummy point to keep indexing simple, or handle re-indexing.
                    // Easiest is to add point but not use it in indices.
                    mesh.Positions.Add(new Point3D(0, 0, 0)); 
                    mesh.TextureCoordinates.Add(new System.Windows.Point(0, 0));
                    continue;
                }

                isValid[x, y] = true;

                double brightness = (pixel.R + pixel.G + pixel.B) / (3.0 * 255.0);
                
                double xPos = (double)x / width * 10.0;
                double zPos = (double)y / height * 10.0;
                double yHeight = brightness * heightScale;

                mesh.Positions.Add(new Point3D(xPos, yHeight, zPos));
                mesh.TextureCoordinates.Add(new System.Windows.Point((double)x / width, (double)y / height));
            }
        }

        // Generate triangles
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                // Check if the quad is valid (all 4 corners must be non-transparent to form a smooth quad)
                // Or at least checks for individual triangles.
                
                int i0 = y * width + x;
                int i1 = i0 + 1;
                int i2 = (y + 1) * width + x;
                int i3 = i2 + 1;

                bool v0 = isValid[x, y];
                bool v1 = isValid[x + 1, y];
                bool v2 = isValid[x, y + 1];
                bool v3 = isValid[x + 1, y + 1];

                // Triangle 1: (x,y), (x,y+1), (x+1,y)  -> i0, i2, i1
                if (v0 && v2 && v1)
                {
                    mesh.TriangleIndices.Add(i0);
                    mesh.TriangleIndices.Add(i2);
                    mesh.TriangleIndices.Add(i1);
                }

                // Triangle 2: (x+1,y), (x,y+1), (x+1,y+1) -> i1, i2, i3
                if (v1 && v2 && v3)
                {
                    mesh.TriangleIndices.Add(i1);
                    mesh.TriangleIndices.Add(i2);
                    mesh.TriangleIndices.Add(i3);
                }
            }
        }

        return mesh;
    }
}
