using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Services;

/// <summary>
/// Service for generating contour lines from elevation data.
/// Uses the Marching Squares algorithm for contour extraction.
/// </summary>
public class ContourGenerator : IContourGenerator
{
    private readonly ILogger<ContourGenerator> _logger;

    public ContourGenerator() : this(NullLogger<ContourGenerator>.Instance) { }

    public ContourGenerator(ILogger<ContourGenerator> logger)
    {
        _logger = logger;
    }

    public async Task<ContourResult> GenerateContoursAsync(ElevationData elevationData, ContourOptions options)
    {
        return await Task.Run(() => GenerateContours(elevationData, options));
    }

    public ContourResult GenerateContours(ElevationData elevationData, ContourOptions options)
    {
        ArgumentNullException.ThrowIfNull(elevationData);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Interval <= 0)
            throw new ArgumentException("Contour interval must be positive.", nameof(options));

        var result = new ContourResult();

        float minElev = options.MinElevation ?? elevationData.MinElevation;
        float maxElev = options.MaxElevation ?? elevationData.MaxElevation;

        _logger.LogInformation("Generating contours from {MinElev}m to {MaxElev}m with interval {Interval}m",
            minElev, maxElev, options.Interval);

        // Generate contour levels
        var levels = new List<float>();
        for (float level = (float)Math.Ceiling(minElev / options.Interval) * options.Interval;
             level <= maxElev;
             level += options.Interval)
        {
            levels.Add(level);
        }

        _logger.LogDebug("Processing {Count} contour levels", levels.Count);

        foreach (var level in levels)
        {
            var contourLines = ExtractContourLines(elevationData, level, options);

            foreach (var line in contourLines)
            {
                // Filter short segments
                if (CalculateLineLength(line) < options.MinSegmentLength)
                    continue;

                // Apply smoothing if requested
                if (options.SmoothContours && line.Points.Count > 2)
                {
                    SmoothContourLine(line, options.SmoothingIterations);
                }

                // Check if this is a major contour
                bool isMajor = Math.Abs(level % options.MajorInterval) < 0.01f;
                line.IsMajor = isMajor;

                if (isMajor)
                {
                    result.MajorContours.Add(line);
                }
                else
                {
                    result.Contours.Add(line);
                }
            }
        }

        _logger.LogInformation("Generated {Count} contour lines ({MajorCount} major)",
            result.Contours.Count + result.MajorContours.Count, result.MajorContours.Count);

        return result;
    }

    public TerrainMesh ApplyContoursToMesh(TerrainMesh mesh, ContourResult contours, float depth, bool emboss = false)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(contours);

        _logger.LogInformation("Applying {Count} contour lines to mesh (depth: {Depth}mm, emboss: {Emboss})",
            contours.TotalSegments, depth, emboss);

        // For now, return the mesh unchanged - full implementation would modify vertices
        // along contour lines to create embossed/debossed effect
        // This is a placeholder for the full implementation
        
        return mesh;
    }

    /// <summary>
    /// Extracts contour lines at a specific elevation using Marching Squares algorithm.
    /// </summary>
    private List<ContourLine> ExtractContourLines(ElevationData data, float level, ContourOptions options)
    {
        var segments = new List<(ContourPoint Start, ContourPoint End)>();
        float cellSizeX = (float)data.CellSizeX;
        float cellSizeY = (float)data.CellSizeY;

        // Marching Squares: Process each cell
        for (int row = 0; row < data.Height - 1; row++)
        {
            for (int col = 0; col < data.Width - 1; col++)
            {
                // Get corner values
                float v00 = data.Values[row, col];
                float v10 = data.Values[row, col + 1];
                float v01 = data.Values[row + 1, col];
                float v11 = data.Values[row + 1, col + 1];

                // Skip if any corner is nodata
                if (IsNoData(v00, data.NoDataValue) || IsNoData(v10, data.NoDataValue) ||
                    IsNoData(v01, data.NoDataValue) || IsNoData(v11, data.NoDataValue))
                    continue;

                // Calculate cell case (4-bit binary)
                int cellCase = 0;
                if (v00 >= level) cellCase |= 1;
                if (v10 >= level) cellCase |= 2;
                if (v11 >= level) cellCase |= 4;
                if (v01 >= level) cellCase |= 8;

                // Skip if all corners are above or below
                if (cellCase == 0 || cellCase == 15)
                    continue;

                // Calculate cell coordinates
                float x0 = col * cellSizeX;
                float y0 = row * cellSizeY;
                float x1 = (col + 1) * cellSizeX;
                float y1 = (row + 1) * cellSizeY;

                // Get interpolated edge points
                var edgePoints = GetEdgePoints(v00, v10, v01, v11, level, x0, y0, x1, y1);

                // Generate segments based on case
                var cellSegments = GetSegmentsForCase(cellCase, edgePoints);
                segments.AddRange(cellSegments);
            }
        }

        // Connect segments into contour lines
        return ConnectSegments(segments, level);
    }

    private static Dictionary<string, ContourPoint> GetEdgePoints(
        float v00, float v10, float v01, float v11,
        float level, float x0, float y0, float x1, float y1)
    {
        var points = new Dictionary<string, ContourPoint>();

        // Bottom edge (between v00 and v10)
        if ((v00 < level) != (v10 < level))
        {
            float t = (level - v00) / (v10 - v00);
            points["bottom"] = new ContourPoint(x0 + t * (x1 - x0), y0);
        }

        // Top edge (between v01 and v11)
        if ((v01 < level) != (v11 < level))
        {
            float t = (level - v01) / (v11 - v01);
            points["top"] = new ContourPoint(x0 + t * (x1 - x0), y1);
        }

        // Left edge (between v00 and v01)
        if ((v00 < level) != (v01 < level))
        {
            float t = (level - v00) / (v01 - v00);
            points["left"] = new ContourPoint(x0, y0 + t * (y1 - y0));
        }

        // Right edge (between v10 and v11)
        if ((v10 < level) != (v11 < level))
        {
            float t = (level - v10) / (v11 - v10);
            points["right"] = new ContourPoint(x1, y0 + t * (y1 - y0));
        }

        return points;
    }

    private static List<(ContourPoint Start, ContourPoint End)> GetSegmentsForCase(
        int cellCase, Dictionary<string, ContourPoint> edges)
    {
        var segments = new List<(ContourPoint, ContourPoint)>();

        // Marching Squares lookup table
        switch (cellCase)
        {
            case 1:
            case 14:
                if (edges.TryGetValue("bottom", out var b1) && edges.TryGetValue("left", out var l1))
                    segments.Add((b1, l1));
                break;
            case 2:
            case 13:
                if (edges.TryGetValue("bottom", out var b2) && edges.TryGetValue("right", out var r2))
                    segments.Add((b2, r2));
                break;
            case 3:
            case 12:
                if (edges.TryGetValue("left", out var l3) && edges.TryGetValue("right", out var r3))
                    segments.Add((l3, r3));
                break;
            case 4:
            case 11:
                if (edges.TryGetValue("top", out var t4) && edges.TryGetValue("right", out var r4))
                    segments.Add((t4, r4));
                break;
            case 5:
                // Saddle point - two segments
                if (edges.TryGetValue("bottom", out var b5) && edges.TryGetValue("left", out var l5))
                    segments.Add((b5, l5));
                if (edges.TryGetValue("top", out var t5) && edges.TryGetValue("right", out var r5))
                    segments.Add((t5, r5));
                break;
            case 6:
            case 9:
                if (edges.TryGetValue("bottom", out var b6) && edges.TryGetValue("top", out var t6))
                    segments.Add((b6, t6));
                break;
            case 7:
            case 8:
                if (edges.TryGetValue("top", out var t7) && edges.TryGetValue("left", out var l7))
                    segments.Add((t7, l7));
                break;
            case 10:
                // Saddle point - two segments
                if (edges.TryGetValue("bottom", out var b10) && edges.TryGetValue("right", out var r10))
                    segments.Add((b10, r10));
                if (edges.TryGetValue("top", out var t10) && edges.TryGetValue("left", out var l10))
                    segments.Add((t10, l10));
                break;
        }

        return segments;
    }

    private static List<ContourLine> ConnectSegments(
        List<(ContourPoint Start, ContourPoint End)> segments, float elevation)
    {
        var lines = new List<ContourLine>();
        var usedSegments = new bool[segments.Count];
        const float tolerance = 0.001f;

        for (int i = 0; i < segments.Count; i++)
        {
            if (usedSegments[i]) continue;

            var line = new ContourLine { Elevation = elevation };
            line.Points.Add(segments[i].Start);
            line.Points.Add(segments[i].End);
            usedSegments[i] = true;

            // Try to extend the line by connecting adjacent segments
            bool extended;
            do
            {
                extended = false;
                var lastPoint = line.Points[^1];
                var firstPoint = line.Points[0];

                for (int j = 0; j < segments.Count; j++)
                {
                    if (usedSegments[j]) continue;

                    var seg = segments[j];

                    // Check if segment connects to end
                    if (PointsClose(lastPoint, seg.Start, tolerance))
                    {
                        line.Points.Add(seg.End);
                        usedSegments[j] = true;
                        extended = true;
                    }
                    else if (PointsClose(lastPoint, seg.End, tolerance))
                    {
                        line.Points.Add(seg.Start);
                        usedSegments[j] = true;
                        extended = true;
                    }
                    // Check if segment connects to start
                    else if (PointsClose(firstPoint, seg.End, tolerance))
                    {
                        line.Points.Insert(0, seg.Start);
                        usedSegments[j] = true;
                        extended = true;
                    }
                    else if (PointsClose(firstPoint, seg.Start, tolerance))
                    {
                        line.Points.Insert(0, seg.End);
                        usedSegments[j] = true;
                        extended = true;
                    }
                }
            } while (extended);

            // Check if line is closed
            if (line.Points.Count > 2 && 
                PointsClose(line.Points[0], line.Points[^1], tolerance))
            {
                line.IsClosed = true;
            }

            if (line.Points.Count >= 2)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private static bool PointsClose(ContourPoint a, ContourPoint b, float tolerance)
    {
        return Math.Abs(a.X - b.X) < tolerance && Math.Abs(a.Y - b.Y) < tolerance;
    }

    private static float CalculateLineLength(ContourLine line)
    {
        float length = 0;
        for (int i = 1; i < line.Points.Count; i++)
        {
            var p0 = line.Points[i - 1];
            var p1 = line.Points[i];
            length += MathF.Sqrt((p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y));
        }
        return length;
    }

    private static void SmoothContourLine(ContourLine line, int iterations)
    {
        for (int iter = 0; iter < iterations; iter++)
        {
            var smoothed = new List<ContourPoint> { line.Points[0] };

            for (int i = 1; i < line.Points.Count - 1; i++)
            {
                var prev = line.Points[i - 1];
                var curr = line.Points[i];
                var next = line.Points[i + 1];

                // Simple averaging
                float x = (prev.X + curr.X + next.X) / 3;
                float y = (prev.Y + curr.Y + next.Y) / 3;
                smoothed.Add(new ContourPoint(x, y));
            }

            smoothed.Add(line.Points[^1]);
            line.Points = smoothed;
        }
    }

    private static bool IsNoData(float value, float noDataValue)
    {
        return Math.Abs(value - noDataValue) < 0.001f;
    }
}
