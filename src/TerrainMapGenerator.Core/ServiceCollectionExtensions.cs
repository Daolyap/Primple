using Microsoft.Extensions.DependencyInjection;
using TerrainMapGenerator.Core.Interfaces;
using TerrainMapGenerator.Core.Services;

namespace TerrainMapGenerator.Core;

/// <summary>
/// Extension methods for configuring Terrain Map Generator services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Terrain Map Generator services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTerrainMapGenerator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Core services
        services.AddSingleton<IElevationDataService, ElevationDataService>();
        services.AddSingleton<IMeshGenerator, MeshGenerator>();
        services.AddSingleton<IPrinterProfileService, PrinterProfileService>();

        // Export services
        services.AddSingleton<IStlExporter, StlExporter>();
        services.AddSingleton<I3MfExporter, ThreeMfExporter>();
        services.AddSingleton<IObjExporter, ObjExporter>();

        // Analysis services
        services.AddSingleton<IContourGenerator, ContourGenerator>();
        services.AddSingleton<IHydrographyService, HydrographyService>();
        services.AddSingleton<ILabelGenerator, LabelGenerator>();

        return services;
    }

    /// <summary>
    /// Adds Terrain Map Generator services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Action to configure options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTerrainMapGenerator(
        this IServiceCollection services,
        Action<TerrainMapGeneratorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new TerrainMapGeneratorOptions();
        configure(options);

        // Register options
        services.AddSingleton(options);

        // Register services based on options
        services.AddTerrainMapGenerator();

        return services;
    }
}

/// <summary>
/// Options for configuring Terrain Map Generator services.
/// </summary>
public class TerrainMapGeneratorOptions
{
    /// <summary>
    /// Whether to enable caching of elevation data.
    /// </summary>
    public bool EnableCaching { get; set; } = false;

    /// <summary>
    /// Maximum cache size in megabytes.
    /// </summary>
    public int MaxCacheSizeMb { get; set; } = 100;

    /// <summary>
    /// Default output format for mesh export.
    /// </summary>
    public string DefaultExportFormat { get; set; } = "stl";

    /// <summary>
    /// Whether to validate meshes after generation.
    /// </summary>
    public bool ValidateMeshes { get; set; } = true;

    /// <summary>
    /// Default vertical exaggeration factor.
    /// </summary>
    public double DefaultVerticalExaggeration { get; set; } = 1.5;
}
