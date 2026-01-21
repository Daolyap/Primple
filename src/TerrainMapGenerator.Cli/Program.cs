using TerrainMapGenerator.Core.Enums;
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Models;
using TerrainMapGenerator.Core.Services;

namespace TerrainMapGenerator.Cli;

/// <summary>
/// Command-line interface for the Terrain Map Generator.
/// </summary>
public class Program
{
    private static readonly IElevationDataService ElevationService = new ElevationDataService();
    private static readonly IMeshGenerator MeshGenerator = new MeshGenerator();
    private static readonly IStlExporter StlExporter = new StlExporter();
    private static readonly IPrinterProfileService PrinterService = new PrinterProfileService();

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var options = ParseArguments(args);

            if (options.ListPrinters)
            {
                ListPrinters();
                return 0;
            }

            if (string.IsNullOrEmpty(options.InputFile))
            {
                Console.Error.WriteLine("Error: Input file is required.");
                return 1;
            }

            if (string.IsNullOrEmpty(options.OutputFile))
            {
                options.OutputFile = Path.ChangeExtension(options.InputFile, ".stl");
            }

            return await GenerateTerrainMap(options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> GenerateTerrainMap(CliOptions options)
    {
        Console.WriteLine($"Loading elevation data from: {options.InputFile}");

        if (!ElevationService.IsFormatSupported(options.InputFile))
        {
            Console.Error.WriteLine($"Error: Unsupported file format. Supported formats: {string.Join(", ", ElevationService.SupportedExtensions)}");
            return 1;
        }

        var elevationData = await ElevationService.LoadFromFileAsync(options.InputFile);
        Console.WriteLine($"  Loaded: {elevationData.Width}x{elevationData.Height} grid");
        Console.WriteLine($"  Elevation range: {elevationData.MinElevation:F1}m to {elevationData.MaxElevation:F1}m ({elevationData.ElevationRange:F1}m)");

        // Create configuration
        var config = new MapConfiguration
        {
            OutputWidthMm = options.Width,
            OutputHeightMm = options.Height,
            MaxPrintHeightMm = options.MaxHeight,
            VerticalExaggeration = options.Exaggeration,
            BaseType = options.BaseType,
            BaseThicknessMm = options.BaseThickness,
            MeshResolution = options.Resolution,
            ApplySmoothing = options.Smooth,
            SmoothingRadius = options.SmoothRadius
        };

        // Apply printer constraints if specified
        if (!string.IsNullOrEmpty(options.PrinterProfile))
        {
            var printer = PrinterService.GetProfileByName(options.PrinterProfile);
            if (printer == null)
            {
                Console.Error.WriteLine($"Error: Unknown printer profile '{options.PrinterProfile}'");
                Console.Error.WriteLine("Use --list-printers to see available profiles.");
                return 1;
            }

            Console.WriteLine($"Using printer profile: {printer.Name}");
            config = printer.CreateConstrainedConfiguration(config);

            var validation = printer.ValidateConfiguration(config);
            foreach (var warning in validation.Warnings)
            {
                Console.WriteLine($"  Warning: {warning}");
            }
        }

        // Validate configuration
        var configValidation = config.Validate();
        if (!configValidation.IsValid)
        {
            foreach (var error in configValidation.Errors)
            {
                Console.Error.WriteLine($"Configuration error: {error}");
            }
            return 1;
        }

        Console.WriteLine("\nGenerating mesh...");
        Console.WriteLine($"  Output size: {config.OutputWidthMm}mm x {config.OutputHeightMm}mm x {config.MaxPrintHeightMm}mm");
        Console.WriteLine($"  Vertical exaggeration: {config.VerticalExaggeration}x");
        Console.WriteLine($"  Base type: {config.BaseType}");

        var mesh = await MeshGenerator.GenerateAsync(elevationData, config);
        Console.WriteLine($"  Generated: {mesh.VertexCount:N0} vertices, {mesh.TriangleCount:N0} triangles");

        // Validate mesh
        var meshValidation = mesh.Validate();
        if (meshValidation.HasErrors)
        {
            Console.Error.WriteLine("Mesh validation errors:");
            foreach (var error in meshValidation.Errors)
            {
                Console.Error.WriteLine($"  {error}");
            }
        }
        foreach (var warning in meshValidation.Warnings)
        {
            Console.WriteLine($"  Warning: {warning}");
        }
        Console.WriteLine($"  Watertight: {meshValidation.IsWatertight}");
        Console.WriteLine($"  Manifold: {meshValidation.IsManifold}");

        // Export
        Console.WriteLine($"\nExporting to: {options.OutputFile}");

        if (options.AsciiStl)
        {
            await StlExporter.ExportAsciiAsync(mesh, options.OutputFile);
        }
        else
        {
            await StlExporter.ExportBinaryAsync(mesh, options.OutputFile);
        }

        var fileInfo = new FileInfo(options.OutputFile);
        Console.WriteLine($"  File size: {fileInfo.Length / 1024.0:F1} KB");
        Console.WriteLine("\nDone!");

        return 0;
    }

    private static void ListPrinters()
    {
        Console.WriteLine("Available Bambu Labs printer profiles:\n");

        foreach (var printer in PrinterService.GetAllProfiles())
        {
            Console.WriteLine($"  {printer.Model,-10} - {printer.Name}");
            Console.WriteLine($"               Build volume: {printer.BuildVolumeX}x{printer.BuildVolumeY}x{printer.BuildVolumeZ}mm");
            Console.WriteLine($"               Multi-material: {(printer.SupportsMultiMaterial ? $"Yes ({printer.MaxMaterialSlots} slots)" : "No")}");
            Console.WriteLine();
        }
    }

    private static CliOptions ParseArguments(string[] args)
    {
        var options = new CliOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg.ToLowerInvariant())
            {
                case "-i":
                case "--input":
                    options.InputFile = GetNextArg(args, ref i, arg);
                    break;

                case "-o":
                case "--output":
                    options.OutputFile = GetNextArg(args, ref i, arg);
                    break;

                case "-w":
                case "--width":
                    options.Width = double.Parse(GetNextArg(args, ref i, arg));
                    break;

                case "-h":
                case "--height":
                    options.Height = double.Parse(GetNextArg(args, ref i, arg));
                    break;

                case "-z":
                case "--max-height":
                    options.MaxHeight = double.Parse(GetNextArg(args, ref i, arg));
                    break;

                case "-e":
                case "--exaggeration":
                    options.Exaggeration = double.Parse(GetNextArg(args, ref i, arg));
                    break;

                case "-b":
                case "--base":
                    options.BaseType = Enum.Parse<BaseType>(GetNextArg(args, ref i, arg), ignoreCase: true);
                    break;

                case "-t":
                case "--base-thickness":
                    options.BaseThickness = double.Parse(GetNextArg(args, ref i, arg));
                    break;

                case "-r":
                case "--resolution":
                    options.Resolution = double.Parse(GetNextArg(args, ref i, arg));
                    break;

                case "-p":
                case "--printer":
                    options.PrinterProfile = GetNextArg(args, ref i, arg);
                    break;

                case "--smooth":
                    options.Smooth = true;
                    break;

                case "--smooth-radius":
                    options.SmoothRadius = int.Parse(GetNextArg(args, ref i, arg));
                    options.Smooth = true;
                    break;

                case "--ascii":
                    options.AsciiStl = true;
                    break;

                case "--list-printers":
                    options.ListPrinters = true;
                    break;

                default:
                    if (!arg.StartsWith("-"))
                    {
                        // Positional argument - treat as input file if not set
                        if (string.IsNullOrEmpty(options.InputFile))
                            options.InputFile = arg;
                        else if (string.IsNullOrEmpty(options.OutputFile))
                            options.OutputFile = arg;
                    }
                    break;
            }
        }

        return options;
    }

    private static string GetNextArg(string[] args, ref int i, string currentArg)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Option {currentArg} requires a value.");
        return args[++i];
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"Terrain Map Generator - 3D Printable Terrain Maps

Usage: TerrainMapGenerator.Cli [options] <input-file> [output-file]

Options:
  -i, --input <file>         Input elevation data file (.asc, .tif, .xyz)
  -o, --output <file>        Output STL file (default: input name with .stl)
  -w, --width <mm>           Output width in mm (default: 150)
  -h, --height <mm>          Output height/depth in mm (default: 150)
  -z, --max-height <mm>      Maximum print height in mm (default: 30)
  -e, --exaggeration <x>     Vertical exaggeration factor (default: 1.5)
  -b, --base <type>          Base type: flat, tapered, contoured, minimal, none (default: flat)
  -t, --base-thickness <mm>  Base thickness in mm (default: 3)
  -r, --resolution <factor>  Mesh resolution factor 0-2 (default: 1)
  -p, --printer <name>       Printer profile: X1C, X1E, P1P, P1S, A1, A1Mini
  --smooth                   Apply Gaussian smoothing
  --smooth-radius <cells>    Smoothing radius in grid cells (default: 1)
  --ascii                    Export ASCII STL instead of binary
  --list-printers            List available printer profiles
  --help                     Show this help message

Examples:
  TerrainMapGenerator.Cli elevation.asc terrain.stl
  TerrainMapGenerator.Cli -i data.tif -o model.stl -w 200 -e 2.0 -p X1C
  TerrainMapGenerator.Cli terrain.asc --smooth --base tapered -z 50

Supported file formats:
  - ASCII Grid (.asc, .grd)
  - GeoTIFF (.tif, .tiff)
  - XYZ Point Cloud (.xyz)
");
    }
}

/// <summary>
/// Command-line options.
/// </summary>
internal class CliOptions
{
    public string InputFile { get; set; } = string.Empty;
    public string OutputFile { get; set; } = string.Empty;
    public double Width { get; set; } = 150;
    public double Height { get; set; } = 150;
    public double MaxHeight { get; set; } = 30;
    public double Exaggeration { get; set; } = 1.5;
    public BaseType BaseType { get; set; } = BaseType.Flat;
    public double BaseThickness { get; set; } = 3;
    public double Resolution { get; set; } = 1.0;
    public string PrinterProfile { get; set; } = string.Empty;
    public bool Smooth { get; set; } = false;
    public int SmoothRadius { get; set; } = 1;
    public bool AsciiStl { get; set; } = false;
    public bool ListPrinters { get; set; } = false;
}
