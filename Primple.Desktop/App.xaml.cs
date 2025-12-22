using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;

namespace Primple.Desktop;

public partial class App : Application
{
    public static IHost? AppHost { get; private set; }

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<MainWindow>();
                
                // Core Services
                services.AddSingleton<Primple.Core.Services.IStlService, Primple.Core.Services.StlService>();
                services.AddSingleton<Primple.Core.Services.IMapsService, Primple.Desktop.Services.MapsService>();
                services.AddSingleton<Primple.Core.Services.IImageService, Primple.Core.Services.ImageService>();
                services.AddSingleton<Primple.Core.Services.IHeightmapService, Primple.Core.Services.HeightmapService>();
                services.AddSingleton<Primple.Desktop.Services.ITemplateService, Primple.Desktop.Services.TemplateService>();
                services.AddSingleton<Primple.Desktop.Services.IFigService, Primple.Desktop.Services.FigService>();
                services.AddSingleton<Primple.Core.Services.IProjectState, Primple.Core.Services.ProjectState>();
                services.AddSingleton<Primple.Core.Services.IAppSettings, Primple.Core.Services.AppSettings>();
                services.AddSingleton<Primple.Core.Services.ILogService, Primple.Core.Services.LogService>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            await AppHost!.StartAsync();

            // Load settings
            var settings = AppHost.Services.GetService<Primple.Core.Services.IAppSettings>();
            settings?.Load();

            // Setup log service based on debug mode setting
            var logService = AppHost.Services.GetService<Primple.Core.Services.ILogService>();
            if (logService != null && settings != null)
            {
                logService.IsEnabled = settings.DebugMode;
                logService.Log("Application started", "App", "INFO");
            }

            var startupForm = AppHost.Services.GetRequiredService<MainWindow>();
            
            // Apply startup window state from settings
            if (settings != null && !settings.StartMaximized)
            {
                startupForm.WindowState = WindowState.Normal;
            }
            
            startupForm.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup Error: {ex.Message}", "Primple Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // Log shutdown
        var logService = AppHost?.Services.GetService<Primple.Core.Services.ILogService>();
        logService?.Log("Application shutting down", "App", "INFO");

        if (AppHost != null)
        {
            await AppHost.StopAsync();
            AppHost.Dispose();
        }
        base.OnExit(e);
    }
}
