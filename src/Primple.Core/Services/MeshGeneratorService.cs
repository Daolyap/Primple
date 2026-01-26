using System.Numerics;
using MathNet.Numerics.LinearAlgebra;
using Primple.Core.Enums;
using Primple.Core.Interfaces;
using Primple.Core.Models;

namespace Primple.Core.Services;

/// <summary>
/// Service for generating 3D terrain meshes from elevation data.
/// </summary>
public class MeshGeneratorService : IMeshGeneratorService
{
    /// <summary>
    /// Generates a terrain mesh from elevation data.
    /// </summary>
    public TerrainMesh GenerateMesh(ElevationData elevationData, TerrainSettings settings, IProgress<double>? progress = null)
    {
        var mesh = new TerrainMesh();

        // Apply smoothing if enabled
        var processedData = settings.SmoothingMethod != SmoothingMethod.None
            ? ApplySmoothing(elevationData, settings)
            : elevationData;

        // Get actual print dimensions
        var (printWidth, printLength) = settings.GetActualSize();

        // Calculate scaling factors
        var elevationRange = processedData.MaxElevation - processedData.MinElevation;
        var baseElevation = settings.RemoveBaseElevation ? processedData.MinElevation : 0;

        // Determine mesh resolution
        var gridWidth = Math.Min(settings.MeshResolution, processedData.Width);
        var gridHeight = Math.Min(settings.MeshResolution, processedData.Height);

        // Calculate aspect ratio if maintaining
        if (settings.MaintainAspectRatio)
        {
            var dataAspect = (double)processedData.Width / processedData.Height;
            if (dataAspect > 1)
            {
                printLength = printWidth / dataAspect;
            }
            else
            {
                printWidth = printLength * dataAspect;
            }
        }

        // Calculate cell size in mm
        var cellWidth = printWidth / (gridWidth - 1);
        var cellLength = printLength / (gridHeight - 1);

        // Calculate height scaling (vertical exaggeration applied)
        var maxPrintHeight = settings.MaxHeight - settings.BaseThickness;
        var heightScale = elevationRange > 0 ? (maxPrintHeight / elevationRange) * settings.VerticalExaggeration : 1;

        // Clamp height scale if needed to fit
        if (elevationRange * heightScale > maxPrintHeight)
        {
            heightScale = maxPrintHeight / elevationRange;
        }

        // Generate top surface vertices
        progress?.Report(0.1);
        var topSurfaceStart = 0;

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                // Sample elevation data
                var dataX = (int)((double)x / (gridWidth - 1) * (processedData.Width - 1));
                var dataY = (int)((double)y / (gridHeight - 1) * (processedData.Height - 1));
                dataX = Math.Clamp(dataX, 0, processedData.Width - 1);
                dataY = Math.Clamp(dataY, 0, processedData.Height - 1);

                var elevation = processedData.Data[dataY, dataX];

                // Handle no-data values
                if (Math.Abs(elevation - processedData.NoDataValue) < 0.01)
                {
                    elevation = baseElevation;
                }

                // Clip elevation if needed
                elevation = Math.Clamp(elevation, settings.MinElevationClip, settings.MaxElevationClip);

                // Calculate vertex position
                var posX = (float)(x * cellWidth);
                var posY = (float)(y * cellLength);
                var posZ = (float)((elevation - baseElevation) * heightScale + settings.BaseThickness);

                mesh.Vertices.Add(new Vector3(posX, posY, posZ));
            }

            progress?.Report(0.1 + 0.3 * y / gridHeight);
        }

        // Generate top surface triangles
        for (int y = 0; y < gridHeight - 1; y++)
        {
            for (int x = 0; x < gridWidth - 1; x++)
            {
                var topLeft = topSurfaceStart + y * gridWidth + x;
                var topRight = topLeft + 1;
                var bottomLeft = topLeft + gridWidth;
                var bottomRight = bottomLeft + 1;

                // Two triangles per quad
                mesh.Indices.Add(topLeft);
                mesh.Indices.Add(bottomLeft);
                mesh.Indices.Add(topRight);

                mesh.Indices.Add(topRight);
                mesh.Indices.Add(bottomLeft);
                mesh.Indices.Add(bottomRight);
            }
        }

        progress?.Report(0.5);

        // Generate bottom surface (flat base)
        var bottomSurfaceStart = mesh.Vertices.Count;
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                var posX = (float)(x * cellWidth);
                var posY = (float)(y * cellLength);
                mesh.Vertices.Add(new Vector3(posX, posY, 0));
            }
        }

        // Generate bottom surface triangles (reversed winding)
        for (int y = 0; y < gridHeight - 1; y++)
        {
            for (int x = 0; x < gridWidth - 1; x++)
            {
                var topLeft = bottomSurfaceStart + y * gridWidth + x;
                var topRight = topLeft + 1;
                var bottomLeft = topLeft + gridWidth;
                var bottomRight = bottomLeft + 1;

                mesh.Indices.Add(topLeft);
                mesh.Indices.Add(topRight);
                mesh.Indices.Add(bottomLeft);

                mesh.Indices.Add(topRight);
                mesh.Indices.Add(bottomRight);
                mesh.Indices.Add(bottomLeft);
            }
        }

        progress?.Report(0.7);

        // Generate side walls
        GenerateSideWalls(mesh, gridWidth, gridHeight, topSurfaceStart, bottomSurfaceStart, settings);

        progress?.Report(0.9);

        // Calculate normals
        mesh.CalculateNormals();

        progress?.Report(1.0);

        return mesh;
    }

    private void GenerateSideWalls(TerrainMesh mesh, int gridWidth, int gridHeight, int topStart, int bottomStart, TerrainSettings settings)
    {
        // Front edge (y = 0)
        for (int x = 0; x < gridWidth - 1; x++)
        {
            var topLeft = topStart + x;
            var topRight = topStart + x + 1;
            var bottomLeft = bottomStart + x;
            var bottomRight = bottomStart + x + 1;

            mesh.Indices.Add(topLeft);
            mesh.Indices.Add(topRight);
            mesh.Indices.Add(bottomLeft);

            mesh.Indices.Add(topRight);
            mesh.Indices.Add(bottomRight);
            mesh.Indices.Add(bottomLeft);
        }

        // Back edge (y = gridHeight - 1)
        for (int x = 0; x < gridWidth - 1; x++)
        {
            var topLeft = topStart + (gridHeight - 1) * gridWidth + x;
            var topRight = topStart + (gridHeight - 1) * gridWidth + x + 1;
            var bottomLeft = bottomStart + (gridHeight - 1) * gridWidth + x;
            var bottomRight = bottomStart + (gridHeight - 1) * gridWidth + x + 1;

            mesh.Indices.Add(topLeft);
            mesh.Indices.Add(bottomLeft);
            mesh.Indices.Add(topRight);

            mesh.Indices.Add(topRight);
            mesh.Indices.Add(bottomLeft);
            mesh.Indices.Add(bottomRight);
        }

        // Left edge (x = 0)
        for (int y = 0; y < gridHeight - 1; y++)
        {
            var topTop = topStart + y * gridWidth;
            var topBottom = topStart + (y + 1) * gridWidth;
            var bottomTop = bottomStart + y * gridWidth;
            var bottomBottom = bottomStart + (y + 1) * gridWidth;

            mesh.Indices.Add(topTop);
            mesh.Indices.Add(bottomTop);
            mesh.Indices.Add(topBottom);

            mesh.Indices.Add(topBottom);
            mesh.Indices.Add(bottomTop);
            mesh.Indices.Add(bottomBottom);
        }

        // Right edge (x = gridWidth - 1)
        for (int y = 0; y < gridHeight - 1; y++)
        {
            var topTop = topStart + y * gridWidth + (gridWidth - 1);
            var topBottom = topStart + (y + 1) * gridWidth + (gridWidth - 1);
            var bottomTop = bottomStart + y * gridWidth + (gridWidth - 1);
            var bottomBottom = bottomStart + (y + 1) * gridWidth + (gridWidth - 1);

            mesh.Indices.Add(topTop);
            mesh.Indices.Add(topBottom);
            mesh.Indices.Add(bottomTop);

            mesh.Indices.Add(topBottom);
            mesh.Indices.Add(bottomBottom);
            mesh.Indices.Add(bottomTop);
        }
    }

    /// <summary>
    /// Applies smoothing to elevation data.
    /// </summary>
    public ElevationData ApplySmoothing(ElevationData data, TerrainSettings settings)
    {
        var smoothed = new ElevationData
        {
            Data = new double[data.Height, data.Width],
            Bounds = data.Bounds,
            Source = data.Source,
            Resolution = data.Resolution,
            NoDataValue = data.NoDataValue
        };

        var radius = (int)Math.Ceiling(settings.SmoothingRadius);

        switch (settings.SmoothingMethod)
        {
            case SmoothingMethod.Gaussian:
                ApplyGaussianSmoothing(data, smoothed, radius);
                break;
            case SmoothingMethod.Median:
                ApplyMedianSmoothing(data, smoothed, radius);
                break;
            case SmoothingMethod.Adaptive:
                ApplyAdaptiveSmoothing(data, smoothed, radius);
                break;
            default:
                Array.Copy(data.Data, smoothed.Data, data.Data.Length);
                break;
        }

        smoothed.CalculateStatistics();
        return smoothed;
    }

    private void ApplyGaussianSmoothing(ElevationData input, ElevationData output, int radius)
    {
        var kernel = CreateGaussianKernel(radius);
        var kernelSize = kernel.GetLength(0);
        var halfKernel = kernelSize / 2;

        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                double sum = 0;
                double weightSum = 0;

                for (int ky = -halfKernel; ky <= halfKernel; ky++)
                {
                    for (int kx = -halfKernel; kx <= halfKernel; kx++)
                    {
                        var sampleX = Math.Clamp(x + kx, 0, input.Width - 1);
                        var sampleY = Math.Clamp(y + ky, 0, input.Height - 1);
                        var value = input.Data[sampleY, sampleX];

                        if (Math.Abs(value - input.NoDataValue) > 0.01)
                        {
                            var weight = kernel[ky + halfKernel, kx + halfKernel];
                            sum += value * weight;
                            weightSum += weight;
                        }
                    }
                }

                output.Data[y, x] = weightSum > 0 ? sum / weightSum : input.Data[y, x];
            }
        }
    }

    private void ApplyMedianSmoothing(ElevationData input, ElevationData output, int radius)
    {
        var values = new List<double>();

        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                values.Clear();

                for (int ky = -radius; ky <= radius; ky++)
                {
                    for (int kx = -radius; kx <= radius; kx++)
                    {
                        var sampleX = Math.Clamp(x + kx, 0, input.Width - 1);
                        var sampleY = Math.Clamp(y + ky, 0, input.Height - 1);
                        var value = input.Data[sampleY, sampleX];

                        if (Math.Abs(value - input.NoDataValue) > 0.01)
                        {
                            values.Add(value);
                        }
                    }
                }

                if (values.Count > 0)
                {
                    values.Sort();
                    output.Data[y, x] = values[values.Count / 2];
                }
                else
                {
                    output.Data[y, x] = input.Data[y, x];
                }
            }
        }
    }

    private void ApplyAdaptiveSmoothing(ElevationData input, ElevationData output, int radius)
    {
        // Calculate local variance first
        var variance = new double[input.Height, input.Width];
        var maxVariance = 0.0;

        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                var values = new List<double>();

                for (int ky = -radius; ky <= radius; ky++)
                {
                    for (int kx = -radius; kx <= radius; kx++)
                    {
                        var sampleX = Math.Clamp(x + kx, 0, input.Width - 1);
                        var sampleY = Math.Clamp(y + ky, 0, input.Height - 1);
                        values.Add(input.Data[sampleY, sampleX]);
                    }
                }

                var mean = values.Average();
                variance[y, x] = values.Select(v => (v - mean) * (v - mean)).Average();
                maxVariance = Math.Max(maxVariance, variance[y, x]);
            }
        }

        // Apply adaptive smoothing
        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                // High variance = feature, less smoothing
                var adaptiveFactor = 1.0 - (variance[y, x] / (maxVariance + 0.001));
                var adaptiveRadius = (int)(radius * adaptiveFactor);

                if (adaptiveRadius == 0)
                {
                    output.Data[y, x] = input.Data[y, x];
                    continue;
                }

                double sum = 0;
                int count = 0;

                for (int ky = -adaptiveRadius; ky <= adaptiveRadius; ky++)
                {
                    for (int kx = -adaptiveRadius; kx <= adaptiveRadius; kx++)
                    {
                        var sampleX = Math.Clamp(x + kx, 0, input.Width - 1);
                        var sampleY = Math.Clamp(y + ky, 0, input.Height - 1);
                        var value = input.Data[sampleY, sampleX];

                        if (Math.Abs(value - input.NoDataValue) > 0.01)
                        {
                            sum += value;
                            count++;
                        }
                    }
                }

                output.Data[y, x] = count > 0 ? sum / count : input.Data[y, x];
            }
        }
    }

    private double[,] CreateGaussianKernel(int radius)
    {
        var size = radius * 2 + 1;
        var kernel = new double[size, size];
        var sigma = radius / 3.0;
        var sum = 0.0;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var dx = x - radius;
                var dy = y - radius;
                kernel[y, x] = Math.Exp(-(dx * dx + dy * dy) / (2 * sigma * sigma));
                sum += kernel[y, x];
            }
        }

        // Normalize
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                kernel[y, x] /= sum;
            }
        }

        return kernel;
    }

    /// <summary>
    /// Optimizes mesh by reducing polygon count.
    /// </summary>
    public TerrainMesh OptimizeMesh(TerrainMesh mesh, int targetTriangles)
    {
        if (mesh.TriangleCount <= targetTriangles)
            return mesh;

        // Simple decimation - for a proper implementation, use a library like MeshDecimator
        // This is a placeholder that returns the original mesh
        // In production, you would use edge collapse or quadric error metrics
        return mesh;
    }
}
