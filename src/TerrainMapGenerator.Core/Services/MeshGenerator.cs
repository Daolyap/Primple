using TerrainMapGenerator.Core.Enums;
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Services;

/// <summary>
/// Service for generating terrain meshes from elevation data.
/// </summary>
public class MeshGenerator : IMeshGenerator
{
    public async Task<TerrainMesh> GenerateAsync(ElevationData elevationData, MapConfiguration configuration)
    {
        return await Task.Run(() => Generate(elevationData, configuration));
    }

    public TerrainMesh Generate(ElevationData elevationData, MapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(elevationData);
        ArgumentNullException.ThrowIfNull(configuration);

        // Apply smoothing if configured
        var processedData = configuration.ApplySmoothing
            ? ApplyGaussianSmoothing(elevationData, configuration.SmoothingRadius)
            : elevationData;

        // Calculate scaling factors
        var (scaleX, scaleY, scaleZ, baseOffset) = CalculateScaling(processedData, configuration);

        // Generate the mesh
        var mesh = new TerrainMesh();

        // Downsample if resolution is less than 1.0
        int stepX = Math.Max(1, (int)(1.0 / configuration.MeshResolution));
        int stepY = Math.Max(1, (int)(1.0 / configuration.MeshResolution));

        int gridWidth = (processedData.Width - 1) / stepX + 1;
        int gridHeight = (processedData.Height - 1) / stepY + 1;

        // Create vertex grid for top surface
        var vertexIndices = new int[gridHeight, gridWidth];

        for (int row = 0; row < gridHeight; row++)
        {
            for (int col = 0; col < gridWidth; col++)
            {
                int srcRow = Math.Min(row * stepY, processedData.Height - 1);
                int srcCol = Math.Min(col * stepX, processedData.Width - 1);

                float elevation = processedData.GetValue(srcRow, srcCol);

                // Handle no-data values
                if (Math.Abs(elevation - processedData.NoDataValue) < 0.001f)
                {
                    elevation = processedData.MinElevation;
                }

                // Normalize elevation if configured
                float normalizedElevation = configuration.NormalizeElevation
                    ? elevation - processedData.MinElevation
                    : elevation;

                float x = (float)(col * scaleX);
                float y = (float)(row * scaleY);
                float z = (float)(normalizedElevation * scaleZ + baseOffset);

                vertexIndices[row, col] = mesh.AddVertex(x, y, z);
            }
        }

        // Create triangles for top surface
        for (int row = 0; row < gridHeight - 1; row++)
        {
            for (int col = 0; col < gridWidth - 1; col++)
            {
                int v00 = vertexIndices[row, col];
                int v10 = vertexIndices[row + 1, col];
                int v01 = vertexIndices[row, col + 1];
                int v11 = vertexIndices[row + 1, col + 1];

                // Two triangles per grid cell (counter-clockwise winding for upward normals)
                mesh.AddTriangle(v00, v10, v01);
                mesh.AddTriangle(v01, v10, v11);
            }
        }

        // Add base and sides based on configuration
        if (configuration.BaseType != BaseType.None)
        {
            AddBase(mesh, vertexIndices, processedData, configuration, scaleX, scaleY, scaleZ, baseOffset, gridWidth, gridHeight);
        }

        // Calculate normals and update bounds
        mesh.CalculateNormals();
        mesh.UpdateBounds();
        mesh.Validate();

        return mesh;
    }

    private static void AddBase(
        TerrainMesh mesh,
        int[,] topVertices,
        ElevationData data,
        MapConfiguration config,
        double scaleX,
        double scaleY,
        double scaleZ,
        double baseOffset,
        int gridWidth,
        int gridHeight)
    {
        float baseZ;

        switch (config.BaseType)
        {
            case BaseType.Flat:
                baseZ = 0;
                AddFlatBase(mesh, topVertices, baseZ, scaleX, scaleY, gridWidth, gridHeight);
                break;

            case BaseType.Contoured:
                // For contoured base, we create a surface that follows the terrain at an offset
                AddContouredBase(mesh, topVertices, data, config, scaleX, scaleY, scaleZ, baseOffset, gridWidth, gridHeight);
                break;

            case BaseType.Tapered:
                AddTaperedBase(mesh, topVertices, config, scaleX, scaleY, gridWidth, gridHeight);
                break;

            case BaseType.Minimal:
                baseZ = (float)(config.BaseThicknessMm * 0.5);
                AddFlatBase(mesh, topVertices, baseZ, scaleX, scaleY, gridWidth, gridHeight);
                break;
        }
    }

    private static void AddFlatBase(
        TerrainMesh mesh,
        int[,] topVertices,
        float baseZ,
        double scaleX,
        double scaleY,
        int gridWidth,
        int gridHeight)
    {
        // Create bottom vertices at baseZ
        var bottomVertices = new int[gridHeight, gridWidth];

        for (int row = 0; row < gridHeight; row++)
        {
            for (int col = 0; col < gridWidth; col++)
            {
                float x = (float)(col * scaleX);
                float y = (float)(row * scaleY);
                bottomVertices[row, col] = mesh.AddVertex(x, y, baseZ);
            }
        }

        // Create bottom surface triangles (reversed winding for downward normals)
        for (int row = 0; row < gridHeight - 1; row++)
        {
            for (int col = 0; col < gridWidth - 1; col++)
            {
                int v00 = bottomVertices[row, col];
                int v10 = bottomVertices[row + 1, col];
                int v01 = bottomVertices[row, col + 1];
                int v11 = bottomVertices[row + 1, col + 1];

                // Reversed winding for bottom face
                mesh.AddTriangle(v00, v01, v10);
                mesh.AddTriangle(v01, v11, v10);
            }
        }

        // Create side walls
        AddSideWalls(mesh, topVertices, bottomVertices, gridWidth, gridHeight);
    }

    private static void AddContouredBase(
        TerrainMesh mesh,
        int[,] topVertices,
        ElevationData data,
        MapConfiguration config,
        double scaleX,
        double scaleY,
        double scaleZ,
        double baseOffset,
        int gridWidth,
        int gridHeight)
    {
        // Create bottom vertices following the terrain with an offset
        var bottomVertices = new int[gridHeight, gridWidth];
        float offsetZ = (float)config.BaseThicknessMm;

        int stepX = Math.Max(1, (int)(1.0 / config.MeshResolution));
        int stepY = Math.Max(1, (int)(1.0 / config.MeshResolution));

        for (int row = 0; row < gridHeight; row++)
        {
            for (int col = 0; col < gridWidth; col++)
            {
                int srcRow = Math.Min(row * stepY, data.Height - 1);
                int srcCol = Math.Min(col * stepX, data.Width - 1);

                float elevation = data.GetValue(srcRow, srcCol);
                if (Math.Abs(elevation - data.NoDataValue) < 0.001f)
                    elevation = data.MinElevation;

                float normalizedElevation = config.NormalizeElevation
                    ? elevation - data.MinElevation
                    : elevation;

                float x = (float)(col * scaleX);
                float y = (float)(row * scaleY);
                float z = (float)(normalizedElevation * scaleZ + baseOffset - offsetZ);

                // Ensure z doesn't go below 0
                z = Math.Max(0, z);

                bottomVertices[row, col] = mesh.AddVertex(x, y, z);
            }
        }

        // Create bottom surface triangles
        for (int row = 0; row < gridHeight - 1; row++)
        {
            for (int col = 0; col < gridWidth - 1; col++)
            {
                int v00 = bottomVertices[row, col];
                int v10 = bottomVertices[row + 1, col];
                int v01 = bottomVertices[row, col + 1];
                int v11 = bottomVertices[row + 1, col + 1];

                mesh.AddTriangle(v00, v01, v10);
                mesh.AddTriangle(v01, v11, v10);
            }
        }

        AddSideWalls(mesh, topVertices, bottomVertices, gridWidth, gridHeight);
    }

    private static void AddTaperedBase(
        TerrainMesh mesh,
        int[,] topVertices,
        MapConfiguration config,
        double scaleX,
        double scaleY,
        int gridWidth,
        int gridHeight)
    {
        // Tapered base: thicker at edges, thinner at center
        var bottomVertices = new int[gridHeight, gridWidth];
        float maxThickness = (float)config.BaseThicknessMm;
        float minThickness = maxThickness * 0.3f;

        float centerX = (gridWidth - 1) * 0.5f;
        float centerY = (gridHeight - 1) * 0.5f;
        float maxDist = MathF.Sqrt(centerX * centerX + centerY * centerY);

        for (int row = 0; row < gridHeight; row++)
        {
            for (int col = 0; col < gridWidth; col++)
            {
                float dx = col - centerX;
                float dy = row - centerY;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float t = dist / maxDist; // 0 at center, 1 at corners

                float thickness = minThickness + (maxThickness - minThickness) * t;

                float x = (float)(col * scaleX);
                float y = (float)(row * scaleY);
                float z = -thickness;

                bottomVertices[row, col] = mesh.AddVertex(x, y, z);
            }
        }

        // Create bottom surface triangles
        for (int row = 0; row < gridHeight - 1; row++)
        {
            for (int col = 0; col < gridWidth - 1; col++)
            {
                int v00 = bottomVertices[row, col];
                int v10 = bottomVertices[row + 1, col];
                int v01 = bottomVertices[row, col + 1];
                int v11 = bottomVertices[row + 1, col + 1];

                mesh.AddTriangle(v00, v01, v10);
                mesh.AddTriangle(v01, v11, v10);
            }
        }

        AddSideWalls(mesh, topVertices, bottomVertices, gridWidth, gridHeight);
    }

    private static void AddSideWalls(
        TerrainMesh mesh,
        int[,] topVertices,
        int[,] bottomVertices,
        int gridWidth,
        int gridHeight)
    {
        // Front edge (row = 0)
        for (int col = 0; col < gridWidth - 1; col++)
        {
            int topLeft = topVertices[0, col];
            int topRight = topVertices[0, col + 1];
            int bottomLeft = bottomVertices[0, col];
            int bottomRight = bottomVertices[0, col + 1];

            mesh.AddTriangle(topLeft, bottomLeft, topRight);
            mesh.AddTriangle(topRight, bottomLeft, bottomRight);
        }

        // Back edge (row = gridHeight - 1)
        for (int col = 0; col < gridWidth - 1; col++)
        {
            int topLeft = topVertices[gridHeight - 1, col];
            int topRight = topVertices[gridHeight - 1, col + 1];
            int bottomLeft = bottomVertices[gridHeight - 1, col];
            int bottomRight = bottomVertices[gridHeight - 1, col + 1];

            mesh.AddTriangle(topLeft, topRight, bottomLeft);
            mesh.AddTriangle(topRight, bottomRight, bottomLeft);
        }

        // Left edge (col = 0)
        for (int row = 0; row < gridHeight - 1; row++)
        {
            int topTop = topVertices[row, 0];
            int topBottom = topVertices[row + 1, 0];
            int bottomTop = bottomVertices[row, 0];
            int bottomBottom = bottomVertices[row + 1, 0];

            mesh.AddTriangle(topTop, topBottom, bottomTop);
            mesh.AddTriangle(topBottom, bottomBottom, bottomTop);
        }

        // Right edge (col = gridWidth - 1)
        for (int row = 0; row < gridHeight - 1; row++)
        {
            int topTop = topVertices[row, gridWidth - 1];
            int topBottom = topVertices[row + 1, gridWidth - 1];
            int bottomTop = bottomVertices[row, gridWidth - 1];
            int bottomBottom = bottomVertices[row + 1, gridWidth - 1];

            mesh.AddTriangle(topTop, bottomTop, topBottom);
            mesh.AddTriangle(topBottom, bottomTop, bottomBottom);
        }
    }

    private static (double scaleX, double scaleY, double scaleZ, double baseOffset) CalculateScaling(
        ElevationData data,
        MapConfiguration config)
    {
        // Calculate scale to fit output dimensions
        double scaleX = config.OutputWidthMm / (data.Width - 1);
        double scaleY = config.OutputHeightMm / (data.Height - 1);

        // Calculate vertical scale
        double elevationRange = data.ElevationRange;
        if (elevationRange < 0.001)
            elevationRange = 1.0; // Prevent division by zero for flat terrain

        double baseThickness = config.BaseType != BaseType.None ? config.BaseThicknessMm : 0;
        double availableHeight = config.MaxPrintHeightMm - baseThickness;

        double scaleZ = (availableHeight / elevationRange) * config.VerticalExaggeration;

        // Ensure minimum feature height
        if (scaleZ * elevationRange < config.MinWallThicknessMm && elevationRange > 0)
        {
            scaleZ = config.MinWallThicknessMm / elevationRange;
        }

        double baseOffset = baseThickness;

        return (scaleX, scaleY, scaleZ, baseOffset);
    }

    private static ElevationData ApplyGaussianSmoothing(ElevationData data, int radius)
    {
        if (radius <= 0) return data;

        var smoothed = new ElevationData(data.Width, data.Height, data.CellSizeX, data.CellSizeY)
        {
            OriginX = data.OriginX,
            OriginY = data.OriginY,
            NoDataValue = data.NoDataValue,
            SourceFormat = data.SourceFormat
        };

        // Create Gaussian kernel
        int kernelSize = radius * 2 + 1;
        var kernel = new float[kernelSize, kernelSize];
        float sigma = radius / 2.0f;
        float sum = 0;

        for (int ky = -radius; ky <= radius; ky++)
        {
            for (int kx = -radius; kx <= radius; kx++)
            {
                float value = MathF.Exp(-(kx * kx + ky * ky) / (2 * sigma * sigma));
                kernel[ky + radius, kx + radius] = value;
                sum += value;
            }
        }

        // Normalize kernel
        for (int ky = 0; ky < kernelSize; ky++)
            for (int kx = 0; kx < kernelSize; kx++)
                kernel[ky, kx] /= sum;

        // Apply convolution
        for (int row = 0; row < data.Height; row++)
        {
            for (int col = 0; col < data.Width; col++)
            {
                float centerValue = data.GetValue(row, col);

                if (Math.Abs(centerValue - data.NoDataValue) < 0.001f)
                {
                    smoothed.SetValue(row, col, data.NoDataValue);
                    continue;
                }

                float smoothedValue = 0;
                float weightSum = 0;

                for (int ky = -radius; ky <= radius; ky++)
                {
                    for (int kx = -radius; kx <= radius; kx++)
                    {
                        int sampleRow = Math.Clamp(row + ky, 0, data.Height - 1);
                        int sampleCol = Math.Clamp(col + kx, 0, data.Width - 1);
                        float sampleValue = data.GetValue(sampleRow, sampleCol);

                        if (Math.Abs(sampleValue - data.NoDataValue) > 0.001f)
                        {
                            float weight = kernel[ky + radius, kx + radius];
                            smoothedValue += sampleValue * weight;
                            weightSum += weight;
                        }
                    }
                }

                smoothed.SetValue(row, col, weightSum > 0 ? smoothedValue / weightSum : centerValue);
            }
        }

        smoothed.RecalculateMinMax();
        return smoothed;
    }
}
