using Microsoft.Extensions.DependencyInjection;
using Primple.App.Services;
using Primple.App.ViewModels;
using Primple.Core.Interfaces;
using Primple.Core.Services;
using System.Windows;

namespace Primple.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IElevationDataService, ElevationDataService>();
        services.AddSingleton<IMeshGeneratorService, MeshGeneratorService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IGeocodingService, GeocodingService>();

        // App services
        services.AddSingleton<IDialogService, DialogService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<MapSelectionViewModel>();
        services.AddTransient<TerrainPreviewViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}

