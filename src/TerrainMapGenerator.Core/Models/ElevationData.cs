using TerrainMapGenerator.Core.Enums;

namespace TerrainMapGenerator.Core.Models;

/// <summary>
/// Represents elevation data for a terrain region.
/// </summary>
public class ElevationData
{
    /// <summary>
    /// Width of the elevation grid (number of columns).
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height of the elevation grid (number of rows).
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Cell size in the X direction (meters).
    /// </summary>
    public double CellSizeX { get; }

    /// <summary>
    /// Cell size in the Y direction (meters).
    /// </summary>
    public double CellSizeY { get; }

    /// <summary>
    /// Minimum elevation value.
    /// </summary>
    public float MinElevation { get; private set; }

    /// <summary>
    /// Maximum elevation value.
    /// </summary>
    public float MaxElevation { get; private set; }

    /// <summary>
    /// The elevation values grid.
    /// </summary>
    public float[,] Values { get; }

    /// <summary>
    /// The value used to indicate no data.
    /// </summary>
    public float NoDataValue { get; set; } = -9999f;

    /// <summary>
    /// Source format of the elevation data.
    /// </summary>
    public ElevationDataFormat SourceFormat { get; set; }

    /// <summary>
    /// Lower-left X coordinate (longitude or easting).
    /// </summary>
    public double OriginX { get; set; }

    /// <summary>
    /// Lower-left Y coordinate (latitude or northing).
    /// </summary>
    public double OriginY { get; set; }

    public ElevationData(int width, int height, double cellSizeX = 1.0, double cellSizeY = 1.0)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

        Width = width;
        Height = height;
        CellSizeX = cellSizeX;
        CellSizeY = cellSizeY;
        Values = new float[height, width];
        MinElevation = float.MaxValue;
        MaxElevation = float.MinValue;
    }

    /// <summary>
    /// Sets the elevation value at the specified grid position.
    /// </summary>
    public void SetValue(int row, int col, float value)
    {
        if (row < 0 || row >= Height) throw new ArgumentOutOfRangeException(nameof(row));
        if (col < 0 || col >= Width) throw new ArgumentOutOfRangeException(nameof(col));

        Values[row, col] = value;

        if (Math.Abs(value - NoDataValue) > 0.001f)
        {
            MinElevation = Math.Min(MinElevation, value);
            MaxElevation = Math.Max(MaxElevation, value);
        }
    }

    /// <summary>
    /// Gets the elevation value at the specified grid position.
    /// </summary>
    public float GetValue(int row, int col)
    {
        if (row < 0 || row >= Height) throw new ArgumentOutOfRangeException(nameof(row));
        if (col < 0 || col >= Width) throw new ArgumentOutOfRangeException(nameof(col));
        return Values[row, col];
    }

    /// <summary>
    /// Recalculates min/max elevation values.
    /// </summary>
    public void RecalculateMinMax()
    {
        MinElevation = float.MaxValue;
        MaxElevation = float.MinValue;

        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
            {
                var value = Values[row, col];
                if (Math.Abs(value - NoDataValue) > 0.001f)
                {
                    MinElevation = Math.Min(MinElevation, value);
                    MaxElevation = Math.Max(MaxElevation, value);
                }
            }
        }
    }

    /// <summary>
    /// Gets the total elevation range.
    /// </summary>
    public float ElevationRange => MaxElevation - MinElevation;

    /// <summary>
    /// Gets the geographic width in meters.
    /// </summary>
    public double GeographicWidth => Width * CellSizeX;

    /// <summary>
    /// Gets the geographic height in meters.
    /// </summary>
    public double GeographicHeight => Height * CellSizeY;
}
