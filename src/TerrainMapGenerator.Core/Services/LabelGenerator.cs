using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;

namespace TerrainMapGenerator.Core.Services;

/// <summary>
/// Service for generating text labels on terrain meshes.
/// </summary>
public class LabelGenerator : ILabelGenerator
{
    private readonly ILogger<LabelGenerator> _logger;

    // Simple glyph definitions for basic ASCII characters
    // Each glyph is defined as a list of line segments (start and end points normalized to 0-1)
    private static readonly Dictionary<char, List<(float x1, float y1, float x2, float y2)>> GlyphData = InitializeGlyphs();

    public LabelGenerator() : this(NullLogger<LabelGenerator>.Instance) { }

    public LabelGenerator(ILogger<LabelGenerator> logger)
    {
        _logger = logger;
    }

    public async Task<LabelResult> GeneratePeakLabelsAsync(ElevationData elevationData, LabelOptions options)
    {
        return await Task.Run(() => GeneratePeakLabels(elevationData, options));
    }

    public LabelResult GeneratePeakLabels(ElevationData elevationData, LabelOptions options)
    {
        ArgumentNullException.ThrowIfNull(elevationData);
        ArgumentNullException.ThrowIfNull(options);

        _logger.LogInformation("Generating peak labels (max: {MaxLabels}, min prominence: {MinProminence}m)",
            options.MaxPeakLabels, options.MinProminence);

        var result = new LabelResult();

        // Find local maxima (peaks)
        var peaks = FindPeaks(elevationData, options.MinProminence);

        // Sort by elevation (highest first)
        peaks.Sort((a, b) => b.Elevation.CompareTo(a.Elevation));

        // Filter to maintain minimum spacing and limit count
        var selectedPeaks = new List<(float X, float Y, float Elevation)>();
        foreach (var peak in peaks)
        {
            if (selectedPeaks.Count >= options.MaxPeakLabels)
                break;

            bool tooClose = selectedPeaks.Any(p =>
            {
                float dx = p.X - peak.X;
                float dy = p.Y - peak.Y;
                return MathF.Sqrt(dx * dx + dy * dy) < options.MinLabelSpacing;
            });

            if (!tooClose)
            {
                selectedPeaks.Add(peak);
            }
        }

        // Create labels for selected peaks
        foreach (var peak in selectedPeaks)
        {
            string text = options.IncludeElevation
                ? string.Format(options.ElevationFormat, peak.Elevation)
                : "▲";

            var label = new TerrainLabel
            {
                Text = text,
                Position = (peak.X, peak.Y),
                Elevation = peak.Elevation,
                FontSize = options.FontSize,
                Depth = options.TextDepth,
                Rotation = options.Rotation,
                Type = LabelType.Peak,
                Alignment = options.Alignment
            };

            result.Labels.Add(label);
        }

        _logger.LogInformation("Generated {Count} peak labels", result.Labels.Count);
        return result;
    }

    public TerrainLabel CreateLabel(string text, (float X, float Y) position, LabelOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(options);

        return new TerrainLabel
        {
            Text = text,
            Position = position,
            FontSize = options.FontSize,
            Depth = options.TextDepth,
            Rotation = options.Rotation,
            Type = LabelType.Custom,
            Alignment = options.Alignment
        };
    }

    public TerrainMesh ApplyLabelsToMesh(TerrainMesh mesh, LabelResult labels, bool emboss = true)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(labels);

        _logger.LogInformation("Applying {Count} labels to mesh (emboss: {Emboss})",
            labels.Count, emboss);

        foreach (var label in labels.Labels)
        {
            mesh = ApplyLabelToMesh(mesh, label, emboss);
        }

        return mesh;
    }

    public TerrainMesh ApplyLabelToMesh(TerrainMesh mesh, TerrainLabel label, bool emboss = true)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(label);

        // This is a simplified implementation that would need to be expanded
        // for full text rendering support. For now, return the mesh unchanged.
        // A complete implementation would:
        // 1. Convert text to vector paths using glyph data
        // 2. Offset vertices along normals to create emboss/deboss effect

        _logger.LogDebug("Applied label '{Text}' at ({X}, {Y})", label.Text, label.Position.X, label.Position.Y);
        
        return mesh;
    }

    private List<(float X, float Y, float Elevation)> FindPeaks(ElevationData data, float minProminence)
    {
        var peaks = new List<(float X, float Y, float Elevation)>();

        // Simple peak detection: find local maxima in a 3x3 neighborhood
        for (int row = 1; row < data.Height - 1; row++)
        {
            for (int col = 1; col < data.Width - 1; col++)
            {
                float centerValue = data.Values[row, col];

                // Skip nodata
                if (Math.Abs(centerValue - data.NoDataValue) < 0.001f)
                    continue;

                // Check if higher than all neighbors
                bool isPeak = true;
                float maxNeighbor = float.MinValue;

                for (int dr = -1; dr <= 1 && isPeak; dr++)
                {
                    for (int dc = -1; dc <= 1 && isPeak; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;

                        float neighbor = data.Values[row + dr, col + dc];
                        if (Math.Abs(neighbor - data.NoDataValue) < 0.001f) continue;

                        if (neighbor >= centerValue)
                        {
                            isPeak = false;
                        }
                        maxNeighbor = Math.Max(maxNeighbor, neighbor);
                    }
                }

                // Check prominence (difference from highest neighbor)
                float prominence = centerValue - maxNeighbor;
                if (isPeak && prominence >= minProminence)
                {
                    float x = (float)(col * data.CellSizeX);
                    float y = (float)(row * data.CellSizeY);
                    peaks.Add((x, y, centerValue));
                }
            }
        }

        return peaks;
    }

    private static Dictionary<char, List<(float, float, float, float)>> InitializeGlyphs()
    {
        // Simple line-based glyph definitions
        // These define characters as line segments for generating mesh geometry
        return new Dictionary<char, List<(float, float, float, float)>>
        {
            ['0'] = new() { (0, 0, 1, 0), (1, 0, 1, 1), (1, 1, 0, 1), (0, 1, 0, 0) },
            ['1'] = new() { (0.5f, 0, 0.5f, 1), (0.5f, 1, 0.2f, 0.8f) },
            ['2'] = new() { (0, 1, 1, 1), (1, 1, 1, 0.5f), (1, 0.5f, 0, 0.5f), (0, 0.5f, 0, 0), (0, 0, 1, 0) },
            ['3'] = new() { (0, 1, 1, 1), (1, 1, 1, 0), (1, 0, 0, 0), (0.2f, 0.5f, 1, 0.5f) },
            ['4'] = new() { (0, 1, 0, 0.5f), (0, 0.5f, 1, 0.5f), (1, 1, 1, 0) },
            ['5'] = new() { (1, 1, 0, 1), (0, 1, 0, 0.5f), (0, 0.5f, 1, 0.5f), (1, 0.5f, 1, 0), (1, 0, 0, 0) },
            ['6'] = new() { (1, 1, 0, 1), (0, 1, 0, 0), (0, 0, 1, 0), (1, 0, 1, 0.5f), (1, 0.5f, 0, 0.5f) },
            ['7'] = new() { (0, 1, 1, 1), (1, 1, 0.5f, 0) },
            ['8'] = new() { (0, 0, 1, 0), (1, 0, 1, 1), (1, 1, 0, 1), (0, 1, 0, 0), (0, 0.5f, 1, 0.5f) },
            ['9'] = new() { (1, 0, 1, 1), (1, 1, 0, 1), (0, 1, 0, 0.5f), (0, 0.5f, 1, 0.5f) },
            ['m'] = new() { (0, 0, 0, 0.7f), (0, 0.7f, 0.3f, 0.7f), (0.3f, 0.7f, 0.3f, 0), (0.3f, 0.7f, 0.6f, 0.7f), (0.6f, 0.7f, 0.6f, 0) },
            ['.'] = new() { (0.4f, 0, 0.6f, 0), (0.6f, 0, 0.6f, 0.1f), (0.6f, 0.1f, 0.4f, 0.1f), (0.4f, 0.1f, 0.4f, 0) },
            ['-'] = new() { (0.2f, 0.5f, 0.8f, 0.5f) },
            [' '] = new(),
            ['A'] = new() { (0, 0, 0.5f, 1), (0.5f, 1, 1, 0), (0.25f, 0.5f, 0.75f, 0.5f) },
            ['M'] = new() { (0, 0, 0, 1), (0, 1, 0.5f, 0.5f), (0.5f, 0.5f, 1, 1), (1, 1, 1, 0) },
            ['T'] = new() { (0, 1, 1, 1), (0.5f, 1, 0.5f, 0) },
            ['E'] = new() { (1, 1, 0, 1), (0, 1, 0, 0), (0, 0, 1, 0), (0, 0.5f, 0.7f, 0.5f) },
            ['R'] = new() { (0, 0, 0, 1), (0, 1, 1, 1), (1, 1, 1, 0.5f), (1, 0.5f, 0, 0.5f), (0.5f, 0.5f, 1, 0) },
            ['N'] = new() { (0, 0, 0, 1), (0, 1, 1, 0), (1, 0, 1, 1) },
            ['▲'] = new() { (0, 0, 0.5f, 1), (0.5f, 1, 1, 0), (1, 0, 0, 0) }
        };
    }
}
