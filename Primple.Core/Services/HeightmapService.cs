using System.Windows.Media.Media3D;
using System.Drawing;
using System.IO;

namespace Primple.Core.Services;

public enum ProjectionShape
{
    Plane,
    Sphere,
    Cube,
    Cylinder
}

public interface IHeightmapService
{
    MeshGeometry3D GenerateHeightmap(string imagePath, double heightScale, int resolution, ProjectionShape shape = ProjectionShape.Plane, bool addBase = true, double baseThickness = 1.0);
}

public class HeightmapService : IHeightmapService
{
    public MeshGeometry3D GenerateHeightmap(string imagePath, double heightScale, int resolution, ProjectionShape shape = ProjectionShape.Plane, bool addBase = true, double baseThickness = 1.0)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Heightmap generation is only supported on Windows.");

        using var bitmap = new Bitmap(imagePath);

        // Always respect aspect ratio to avoid squashing the content
        double aspectRatio = (double)bitmap.Width / bitmap.Height;
        int targetWidth, targetHeight;
        
        if (aspectRatio >= 1.0)
        {
            targetWidth = resolution;
            targetHeight = (int)(resolution / aspectRatio);
        }
        else
        {
            targetHeight = resolution;
            targetWidth = (int)(resolution * aspectRatio);
        }
            
        using var resized = new Bitmap(bitmap, new Size(Math.Max(5, targetWidth), Math.Max(5, targetHeight)));

        var mesh = new MeshGeometry3D();
        int width = resized.Width;
        int height = resized.Height;

        bool[,] isValid = new bool[width, height];

        // Calculate model dimensions based on aspect ratio
        double modelWidth = 10.0;
        double modelHeight = modelWidth / aspectRatio;
        
        // Center offset to position the model on the grid
        double offsetX = -modelWidth / 2;
        double offsetZ = -modelHeight / 2;

        // Generate positions - FIX: center the model on the grid
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = resized.GetPixel(x, y);
                
                if (pixel.A < 20)
                {
                    isValid[x, y] = false;
                    mesh.Positions.Add(new Point3D(0, 0, 0)); 
                    mesh.TextureCoordinates.Add(new System.Windows.Point(0, 0));
                    continue;
                }

                isValid[x, y] = true;
                double brightness = (pixel.R + pixel.G + pixel.B) / (3.0 * 255.0);
                double displacement = brightness * heightScale;
                double u = (double)x / (width - 1);
                double v = (double)y / (height - 1);

                Point3D pos;
                switch (shape)
                {
                    case ProjectionShape.Sphere:
                        // Map to sphere - centered at origin
                        double theta = u * 2 * Math.PI;
                        double phi = v * Math.PI;
                        double r = 5.0 + displacement;
                        pos = new Point3D(
                            r * Math.Sin(phi) * Math.Cos(theta),
                            r * Math.Cos(phi),
                            r * Math.Sin(phi) * Math.Sin(theta)
                        );
                        break;
                    case ProjectionShape.Cylinder:
                        // Map to cylinder - centered at origin
                        double angle = u * 2 * Math.PI;
                        double radius = 4.0 + displacement;
                        pos = new Point3D(
                            radius * Math.Cos(angle),
                            (v - 0.5) * 10.0,
                            radius * Math.Sin(angle)
                        );
                        break;
                    case ProjectionShape.Cube:
                        // Simple box projection with displacement
                        double boxX = (u - 0.5) * 10.0;
                        double boxZ = (v - 0.5) * 10.0;
                        double boxY = displacement;
                        pos = new Point3D(boxX, boxY, boxZ);
                        break;
                    case ProjectionShape.Plane:
                    default:
                        // FIX: Center the plane on the grid (0,0,0)
                        pos = new Point3D(
                            offsetX + u * modelWidth,
                            displacement,
                            offsetZ + v * modelHeight
                        );
                        break;
                }

                mesh.Positions.Add(pos);
                mesh.TextureCoordinates.Add(new System.Windows.Point(u, v));
            }
        }

        // Generate triangles for top surface
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                int i0 = y * width + x;
                int i1 = i0 + 1;
                int i2 = (y + 1) * width + x;
                int i3 = i2 + 1;

                if (isValid[x, y] && isValid[x + 1, y] && isValid[x, y + 1])
                {
                    mesh.TriangleIndices.Add(i0);
                    mesh.TriangleIndices.Add(i2);
                    mesh.TriangleIndices.Add(i1);
                }

                if (isValid[x + 1, y] && isValid[x, y + 1] && isValid[x + 1, y + 1])
                {
                    mesh.TriangleIndices.Add(i1);
                    mesh.TriangleIndices.Add(i2);
                    mesh.TriangleIndices.Add(i3);
                }
            }
        }

        // FIX: Add base to complete the solid shape for 3D printing
        if (addBase && (shape == ProjectionShape.Plane || shape == ProjectionShape.Cube))
        {
            AddSolidBase(mesh, width, height, isValid, offsetX, offsetZ, modelWidth, modelHeight, baseThickness);
        }

        return mesh;
    }

    /// <summary>
    /// Adds a solid base and walls to make the heightmap printable
    /// </summary>
    private void AddSolidBase(MeshGeometry3D mesh, int width, int height, bool[,] isValid, 
        double offsetX, double offsetZ, double modelWidth, double modelHeight, double baseThickness)
    {
        double baseY = -baseThickness;
        int baseStartIndex = mesh.Positions.Count;
        
        // Add base vertices (flat bottom)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double u = (double)x / (width - 1);
                double v = (double)y / (height - 1);
                
                mesh.Positions.Add(new Point3D(
                    offsetX + u * modelWidth,
                    baseY,
                    offsetZ + v * modelHeight
                ));
            }
        }
        
        // Add base triangles (bottom face) - reversed winding for correct normals
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                int i0 = baseStartIndex + y * width + x;
                int i1 = i0 + 1;
                int i2 = baseStartIndex + (y + 1) * width + x;
                int i3 = i2 + 1;

                // Reversed winding for bottom face
                mesh.TriangleIndices.Add(i0);
                mesh.TriangleIndices.Add(i1);
                mesh.TriangleIndices.Add(i2);

                mesh.TriangleIndices.Add(i1);
                mesh.TriangleIndices.Add(i3);
                mesh.TriangleIndices.Add(i2);
            }
        }
        
        // Add perimeter walls connecting top surface to base
        // Front edge (y = 0)
        for (int x = 0; x < width - 1; x++)
        {
            if (!isValid[x, 0] && !isValid[x + 1, 0]) continue;
            
            int topIdx = x;
            int topIdxNext = x + 1;
            int baseIdx = baseStartIndex + x;
            int baseIdxNext = baseStartIndex + x + 1;
            
            var topPos = mesh.Positions[topIdx];
            var topPosNext = mesh.Positions[topIdxNext];
            var basePos = mesh.Positions[baseIdx];
            var basePosNext = mesh.Positions[baseIdxNext];
            
            AddQuad(mesh, topPos, topPosNext, basePosNext, basePos);
        }
        
        // Back edge (y = height - 1)
        for (int x = 0; x < width - 1; x++)
        {
            if (!isValid[x, height - 1] && !isValid[x + 1, height - 1]) continue;
            
            int topIdx = (height - 1) * width + x;
            int topIdxNext = topIdx + 1;
            int baseIdx = baseStartIndex + (height - 1) * width + x;
            int baseIdxNext = baseIdx + 1;
            
            var topPos = mesh.Positions[topIdx];
            var topPosNext = mesh.Positions[topIdxNext];
            var basePos = mesh.Positions[baseIdx];
            var basePosNext = mesh.Positions[baseIdxNext];
            
            AddQuad(mesh, topPosNext, topPos, basePos, basePosNext);
        }
        
        // Left edge (x = 0)
        for (int y = 0; y < height - 1; y++)
        {
            if (!isValid[0, y] && !isValid[0, y + 1]) continue;
            
            int topIdx = y * width;
            int topIdxNext = (y + 1) * width;
            int baseIdx = baseStartIndex + y * width;
            int baseIdxNext = baseStartIndex + (y + 1) * width;
            
            var topPos = mesh.Positions[topIdx];
            var topPosNext = mesh.Positions[topIdxNext];
            var basePos = mesh.Positions[baseIdx];
            var basePosNext = mesh.Positions[baseIdxNext];
            
            AddQuad(mesh, topPosNext, topPos, basePos, basePosNext);
        }
        
        // Right edge (x = width - 1)
        for (int y = 0; y < height - 1; y++)
        {
            if (!isValid[width - 1, y] && !isValid[width - 1, y + 1]) continue;
            
            int topIdx = y * width + (width - 1);
            int topIdxNext = (y + 1) * width + (width - 1);
            int baseIdx = baseStartIndex + y * width + (width - 1);
            int baseIdxNext = baseStartIndex + (y + 1) * width + (width - 1);
            
            var topPos = mesh.Positions[topIdx];
            var topPosNext = mesh.Positions[topIdxNext];
            var basePos = mesh.Positions[baseIdx];
            var basePosNext = mesh.Positions[baseIdxNext];
            
            AddQuad(mesh, topPos, topPosNext, basePosNext, basePos);
        }
    }

    private void AddQuad(MeshGeometry3D mesh, Point3D p0, Point3D p1, Point3D p2, Point3D p3)
    {
        int idx = mesh.Positions.Count;
        mesh.Positions.Add(p0);
        mesh.Positions.Add(p1);
        mesh.Positions.Add(p2);
        mesh.Positions.Add(p3);
        
        mesh.TriangleIndices.Add(idx);
        mesh.TriangleIndices.Add(idx + 1);
        mesh.TriangleIndices.Add(idx + 2);
        
        mesh.TriangleIndices.Add(idx);
        mesh.TriangleIndices.Add(idx + 2);
        mesh.TriangleIndices.Add(idx + 3);
    }
}
