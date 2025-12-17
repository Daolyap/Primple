using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;

namespace Primple.Desktop;

#pragma warning disable CA1515 // Application entry point is effectively public
public sealed partial class App : Application
#pragma warning restore CA1515
{
    public static IHost? AppHost { get; private set; }

    public App()
    {
        // Global Exception Handling
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            LogFatalException(e.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException");

        DispatcherUnhandledException += (s, e) =>
        {
            LogFatalException(e.Exception, "DispatcherUnhandledException");
            e.Handled = true; // Prevent immediate crash if possible, though usually we should exit
            Shutdown();
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LogFatalException(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };

        AppHost = Host.CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
            await AppHost!.StartAsync();
#pragma warning restore CA2007

            var startupForm = AppHost.Services.GetRequiredService<MainWindow>();
            startupForm.Show();
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogFatalException(ex, "OnStartup");
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (AppHost != null)
        {
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
            await AppHost.StopAsync();
#pragma warning restore CA2007
            AppHost.Dispose();
        }
        base.OnExit(e);
    }

    private static void LogFatalException(Exception? ex, string source)
    {
        // Log detailed error information to file for debugging
        string detailedMessage = $"A fatal error occurred ({source}): {ex?.Message}\n{ex?.StackTrace}";
        LogToFile(detailedMessage);
        
        // Show user-friendly message without sensitive details
        string userMessage = "An unexpected error occurred and the application needs to close. " +
                           "Error details have been logged for troubleshooting.";
        MessageBox.Show(userMessage, "Fatal Error - Primple", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void LogToFile(string message)
    {
        try
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Primple",
                "Logs");
            
            Directory.CreateDirectory(logDir);
            
            string logFile = Path.Combine(logDir, $"error_{DateTime.UtcNow:yyyyMMdd}.log");
            string logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}] {message}\n\n";
            
            File.AppendAllText(logFile, logEntry);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception logEx)
#pragma warning restore CA1031
        {
            // Fallback logging to prevent silent failures, especially useful in debug builds
            #if DEBUG
            System.Diagnostics.Debug.WriteLine($"Failed to write to log file: {logEx.Message}");
            System.Diagnostics.Debug.WriteLine($"Original error: {message}");
            #endif
            
            // In production, silently fail to prevent recursive errors
            // Consider logging to Windows Event Log as a future enhancement
        }
    }
}
