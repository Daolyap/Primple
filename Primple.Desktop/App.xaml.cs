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
                services.AddSingleton<Primple.Core.Services.IMapsService, Primple.Core.Services.MapsService>();
                services.AddSingleton<Primple.Core.Services.IImageService, Primple.Core.Services.ImageService>();
                services.AddSingleton<Primple.Core.Services.IHeightmapService, Primple.Core.Services.HeightmapService>();
                services.AddSingleton<Primple.Desktop.Services.ITemplateService, Primple.Desktop.Services.TemplateService>();
                services.AddSingleton<Primple.Desktop.Services.IFigService, Primple.Desktop.Services.FigService>();
                services.AddSingleton<Primple.Core.Services.IProjectState, Primple.Core.Services.ProjectState>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            await AppHost!.StartAsync();

            var startupForm = AppHost.Services.GetRequiredService<MainWindow>();
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
        if (AppHost != null)
        {
            await AppHost.StopAsync();
            AppHost.Dispose();
        }
        base.OnExit(e);
    }
}
