namespace Primple.Core.Enums;

/// <summary>
/// Supported Bambu Labs printer models.
/// </summary>
public enum PrinterModel
{
    X1Carbon,
    X1E,
    P1P,
    P1S,
    P2S,
    A1,
    A1Mini
}

/// <summary>
/// Extension methods for PrinterModel.
/// </summary>
public static class PrinterModelExtensions
{
    /// <summary>
    /// Gets the display name for the printer model.
    /// </summary>
    public static string GetDisplayName(this PrinterModel model) => model switch
    {
        PrinterModel.X1Carbon => "X1 Carbon",
        PrinterModel.X1E => "X1E",
        PrinterModel.P1P => "P1P",
        PrinterModel.P1S => "P1S",
        PrinterModel.P2S => "P2S",
        PrinterModel.A1 => "A1",
        PrinterModel.A1Mini => "A1 Mini",
        _ => model.ToString()
    };

    /// <summary>
    /// Gets the build volume dimensions in millimeters (X, Y, Z).
    /// </summary>
    public static (double X, double Y, double Z) GetBuildVolume(this PrinterModel model) => model switch
    {
        PrinterModel.X1Carbon => (256, 256, 256),
        PrinterModel.X1E => (256, 256, 256),
        PrinterModel.P1P => (256, 256, 256),
        PrinterModel.P1S => (256, 256, 256),
        PrinterModel.P2S => (256, 256, 256),
        PrinterModel.A1 => (256, 256, 256),
        PrinterModel.A1Mini => (180, 180, 180),
        _ => (256, 256, 256)
    };

    /// <summary>
    /// Gets the number of AMS (Automatic Material System) slots supported.
    /// </summary>
    public static int GetAmsSlots(this PrinterModel model) => model switch
    {
        PrinterModel.X1Carbon => 4,
        PrinterModel.X1E => 4,
        PrinterModel.P1P => 4,
        PrinterModel.P1S => 4,
        PrinterModel.P2S => 4,
        PrinterModel.A1 => 4,
        PrinterModel.A1Mini => 4,
        _ => 4
    };

    /// <summary>
    /// Indicates whether the printer supports multi-color printing.
    /// </summary>
    public static bool SupportsMultiColor(this PrinterModel model) => model switch
    {
        PrinterModel.X1Carbon => true,
        PrinterModel.X1E => true,
        PrinterModel.P1P => true,
        PrinterModel.P1S => true,
        PrinterModel.P2S => true,
        PrinterModel.A1 => true,
        PrinterModel.A1Mini => true,
        _ => true
    };
}
